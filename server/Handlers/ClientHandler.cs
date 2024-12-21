using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using static Utilities;

class ClientHandler
{
    readonly TcpClient client;
    readonly NetworkStream stream;
    readonly EndPoint endPoint;

    ChatHandler? groupHandler;
    User? mainUser = null, partner = null;

    public EndPoint EndPoint => endPoint;
    public User? User => mainUser;

    public ClientHandler(TcpClient _client)
    {
        client = _client;
        stream = _client.GetStream();
        endPoint = _client.Client.RemoteEndPoint!;

        LogManager.AddLog($"Connected to server", this);
    }

    public override string ToString()
        => mainUser?.ToString() ?? endPoint?.ToString() ?? "Null client";

    public async Task HandlingClientAsync(CancellationToken token)
    {
        try
        {
            byte[] buffer = new byte[MagicNum.bufferSize];
            Memory<byte> memory = new(buffer, 0, buffer.Length);
            int bytesRead;

            Command? receivedCmd;
            Command cmdToSend = new();

            User? tempUser = null;

            while((bytesRead = await stream.ReadAsync(memory, token)) > 0)
            {
                receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, bytesRead));

                switch (receivedCmd?.CommandType)
                {
                    case CommandType.Ping:
                        cmdToSend.Set(CommandType.Ping, null);
                        break;

                    case CommandType.CheckUsername:
                        cmdToSend = await CheckUsername(receivedCmd);
                        break;

                    case CommandType.Register:
                        cmdToSend = await Register(receivedCmd);
                        break;

                    case CommandType.GetUserPwd:
                        (cmdToSend, tempUser) = await GetUserPwd(receivedCmd);
                        break;

                    case CommandType.Login:
                        cmdToSend = await Login(receivedCmd, tempUser);
                        break;

                    case CommandType.Logout:
                        cmdToSend = await Logout(receivedCmd);
                        break;

                    case CommandType.ChangeNickname:
                        cmdToSend = await ChangeNickname(receivedCmd);
                        break;

                    case CommandType.ChangePassword:
                        cmdToSend = await ChangePassword(receivedCmd);
                        break;

                    case CommandType.GetFriendList:
                        cmdToSend = await GetFriendList(receivedCmd);
                        break;
                    
                    case CommandType.RemoveFriend:
                    case CommandType.DenyFriendRq:
                    case CommandType.UnblockUser:
                        cmdToSend = await UpdateFriend(receivedCmd, null);
                        break;
                        
                    case CommandType.GetReceivedRq:
                        cmdToSend = await GetPendingRq(receivedCmd);
                        break;
                        
                    case CommandType.AcceptFriendRq:
                        cmdToSend = await UpdateFriend(receivedCmd, FriendStatus.Confirmed);
                        break;

                    case CommandType.AcceptAllRq:
                        cmdToSend = await UpdateAllFriend(receivedCmd, FriendStatus.Pending, FriendStatus.Confirmed);
                        break;

                    case CommandType.DenyAllRq:
                        cmdToSend = await UpdateAllFriend(receivedCmd, FriendStatus.Pending, null);
                        break;

                    case CommandType.BlockAll:
                        cmdToSend = await UpdateAllFriend(receivedCmd, FriendStatus.Pending, FriendStatus.Blocked);
                        break;
                        
                    case CommandType.GetAllUsers:
                        cmdToSend = await GetUserList(receivedCmd);
                        break;

                    case CommandType.SendFriendRq:
                        cmdToSend = await UpdateFriend(receivedCmd, FriendStatus.Pending);
                        break;

                    case CommandType.BlockUser:
                        cmdToSend = await BlockUser(receivedCmd);
                        break;

                    case CommandType.SetPartner:
                        (cmdToSend, partner) = await SetPartner(receivedCmd);
                        break;

                    case CommandType.GetPartnerHistory:
                        cmdToSend = await GetPartnerHistory(receivedCmd);
                        break;

                    case CommandType.RemovePartner:
                        cmdToSend = RemoveGroupHandler(receivedCmd);
                        partner = null;
                        break;

                    case CommandType.GetCreatedGroups:
                        cmdToSend = await GetCreatedGroups(receivedCmd);
                        break;

                    case CommandType.CreateGroup:
                        cmdToSend = await CreateGroup(receivedCmd);
                        break;

                    case CommandType.ChangeGroupName:
                        cmdToSend = await ChangeGroupName(receivedCmd);
                        break;

                    case CommandType.DeleteGroup:
                        cmdToSend = await DeleteGroup(receivedCmd);
                        break;

                    case CommandType.GetSubcribed:
                        cmdToSend = await GetSubcribedGroups(receivedCmd);
                        break;

                    case CommandType.RemoveSubcribed:
                        cmdToSend = await SubUnsubToGroup(receivedCmd, false);
                        break;

                    case CommandType.GetAllGroups:
                        cmdToSend = await GetAllGroupList(receivedCmd);
                        break;

                    case CommandType.SubcribeToGroup:
                        cmdToSend = await SubUnsubToGroup(receivedCmd, true);
                        break;

                    case CommandType.JoinGroup:
                        cmdToSend = await JoinGroup(receivedCmd);
                        break;

                    case CommandType.GetGroupInfo:
                        cmdToSend = await GetGroupInfo(receivedCmd);
                        break;

                    case CommandType.GetGroupHistory:
                        cmdToSend = await GetGroupHistory(receivedCmd);
                        break;

                    case CommandType.LeaveGroup:
                        cmdToSend = RemoveGroupHandler(receivedCmd);
                        break;

                    case CommandType.Message:
                        cmdToSend = ProcessMessage(receivedCmd);
                        break;

                    case CommandType.Disconnect:
                        LogManager.AddLog($"Client disconnected", this);
                        return;

                    default:
                        cmdToSend = Helper.ErrorCmd(this, new(CommandType.Error, null), "Received invalid cmd");
                        break;
                }

                if (cmdToSend.CommandType != CommandType.Empty)
                {
                    await stream.WriteAsync(EncodeString(cmdToSend.Serialize()), token);
                    cmdToSend.Set(CommandType.Empty, null);
                }
            }
        }
        catch(OperationCanceledException) {}
        catch(Exception ex)
        {
            LogManager.AddLog($"Error while handling client: {ex}", this);
        }
        finally
        {
            Server.DisposeClient(this);
            stream.Close();
            client.Close();
        }
    }

    public void SetUpGroupHandler(ChatHandler? _groupHandler)
    {
        groupHandler = _groupHandler;
    }

    public async void EchoCmd(Command cmd, CancellationToken token)
    {
        await stream.WriteAsync(EncodeString(cmd.Serialize()), token);
    }

    private async Task<Command> CheckUsername(Command cmd)
    {
        var (success, errorMessage) = await DBHelper.UserDB.CheckUsername(cmd.Payload);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        return new(cmd.CommandType, null);
    }

    private async Task<Command> Register(Command cmd)
    {
        User? registeredUser = User.Deserialize(cmd.Payload);

        if(registeredUser == null)
            return Helper.ErrorCmd(this, cmd, "Invalid registering user");

        var (success, errorMessage) = await DBHelper.UserDB.Add(registeredUser);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        LogManager.AddLog($"Registered Username '{registeredUser.Username}'", this);
        return new(cmd.CommandType, null);
    }

    private async Task<(Command cmdToSend, User? requestedUser)> GetUserPwd(Command cmd)
    {
        var (requestedUser, errorMessage) = await DBHelper.UserDB.Get(cmd.Payload, true);

        if(requestedUser == null)
            return (Helper.ErrorCmd(this, cmd, errorMessage), null);

        if(requestedUser.PwdSet == null)
            return (Helper.ErrorCmd(this, cmd, "No password found"), null);

        return (new(cmd.CommandType, requestedUser.PwdSet.Serialize()), requestedUser);
    }

    private async Task<Command> Login(Command cmd, User? tempUser)
    {
        if(mainUser != null || tempUser == null)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser/tempUser");

        var (success, errorMessage) = await DBHelper.UserDB.Update(tempUser.UserID, null, true, null);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        mainUser = tempUser;
        LogManager.AddLog($"Logged in as {mainUser}", endPoint);
        return new(cmd.CommandType, mainUser.Serialize());
    }

    private async Task<Command> Logout(Command cmd)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        var (success, errorMessage) = await DBHelper.UserDB.Update(mainUser.UserID, null, false, null);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        LogManager.AddLog($"Logged out from {mainUser}", endPoint);
        mainUser = null;
        return new(cmd.CommandType, null);
    }

    private async Task<Command> ChangeNickname(Command cmd)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        string newNickname = cmd.Payload;

        var (success, errorMessage) = await DBHelper.UserDB.Update(mainUser.UserID, newNickname, null, null);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        LogManager.AddLog($"Changed nickname of {mainUser} from '{mainUser.Nickname}' to '{newNickname}'", this);
        mainUser.Nickname = newNickname;
        return new(cmd.CommandType, null);
    }

    private async Task<Command> ChangePassword(Command cmd)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        var newPwd = PasswordSet.Deserialize(cmd.Payload);

        var (success, errorMessage) = await DBHelper.UserDB.Update(mainUser.UserID, null, null, newPwd);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        LogManager.AddLog($"Changed password of {mainUser}", this);
        mainUser.PwdSet = newPwd;
        return new(cmd.CommandType, null);
    }

    private async Task<Command> GetFriendList(Command cmd)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        var (friends, errorMessage) = await DBHelper.FriendsDB.GetFriends(mainUser.UserID);

        if(friends == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        return new(cmd.CommandType, JsonSerializer.Serialize(friends));
    }

    private async Task<Command> UpdateFriend(Command cmd, FriendStatus? status)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        int otherID = Convert.ToInt32(cmd.Payload);

        var (success, errorMessage) = status == FriendStatus.Pending ?
            await DBHelper.FriendsDB.Add(mainUser.UserID, otherID, FriendStatus.Pending) :
            await DBHelper.FriendsDB.Update(otherID, mainUser.UserID, status);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        LogManager.AddLog($"{status.ToString() ?? "Denied/removed"} friend (UserID: {otherID})", this);
        return new(cmd.CommandType, null);
    }

    private async Task<Command> UpdateAllFriend(Command cmd, FriendStatus oldStatus, FriendStatus? newStatus)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        var (success, errorMessage) = await DBHelper.FriendsDB.UpdateAll(mainUser.UserID, oldStatus, newStatus);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        LogManager.AddLog($"{newStatus?.ToString() ?? "Denied/removed"} all {oldStatus} friends", this);
        return new(cmd.CommandType, null);
    }

    private async Task<Command> GetPendingRq(Command cmd)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        var (friends, errorMessage) = await DBHelper.FriendsDB.GetPendingRq(mainUser.UserID);

        if(friends == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        return new(cmd.CommandType, JsonSerializer.Serialize(friends));
    }

    private async Task<Command> GetUserList(Command cmd)
    {
        var (users, errorMessage) = await DBHelper.UserDB.GetAll(false);

        if(users == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        return new(cmd.CommandType, JsonSerializer.Serialize(users));
    }

    private async Task<Command> BlockUser(Command cmd)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        int otherID = Convert.ToInt32(cmd.Payload);

        var (success, errorMessage) = await DBHelper.FriendsDB.Block(mainUser.UserID, otherID);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        LogManager.AddLog($"Blocked (UserID: {otherID})", this);
        return new(cmd.CommandType, null);
    }

    private async Task<(Command, User?)> SetPartner(Command cmd)
    {
        if(mainUser == null || mainUser.UserID < 1)
            return (Helper.ErrorCmd(this, cmd, "Invalid mainUser"), null);

        var (partner, errorMessage) = await DBHelper.UserDB.Get(Convert.ToInt32(cmd.Payload), false);

        if(partner == null)
            return (Helper.ErrorCmd(this, cmd, errorMessage), null);

        await DBHelper.MessageDB.SetReadPrivate(mainUser.UserID, partner.UserID);
        LogManager.AddLog($"Messaging {partner}", this);
        Server.JoinPrivate(this, mainUser.UserID, partner.UserID);
        return (new(cmd.CommandType, partner.Serialize()), partner);
    }

    private async Task<Command> GetPartnerHistory(Command cmd)
    {
        if(mainUser == null || partner == null)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser/partner data");

        var (messages, errorMessage) = await DBHelper.MessageDB.GetHistoryPrivate(mainUser.UserID, partner.UserID);

        if (messages == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        return new(cmd.CommandType, JsonSerializer.Serialize(messages));
    }

    private async Task<Command> GetCreatedGroups(Command cmd)
    {
        if(mainUser == null)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser data");

        var (groups, errorMessage) = await DBHelper.ChatGroupDB.GetByCreator(mainUser.UserID);

        if(groups == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        return new(cmd.CommandType, JsonSerializer.Serialize(groups));
    }

    private async Task<Command> CreateGroup(Command cmd)
    {
        ChatGroup? chatGroup = ChatGroup.Deserialize(cmd.Payload);

        if(chatGroup == null)
            return Helper.ErrorCmd(this, cmd, "Null chatGroup");

        var (success, errorMessage) = await DBHelper.ChatGroupDB.Add(chatGroup);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        LogManager.AddLog($"Created group '{chatGroup.GroupName}'", this);
        return new(cmd.CommandType, null);
    }

    private async Task<Command> ChangeGroupName(Command cmd)
    {
        if (mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        ChatGroup? groupToChange = ChatGroup.Deserialize(cmd.Payload);

        if (groupToChange == null)
            return Helper.ErrorCmd(this, cmd, "Invalid group data");

        var (successs, errorMessage) = await DBHelper.ChatGroupDB.Update(groupToChange.GroupID, groupToChange.GroupName, null);

        if(!successs)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        LogManager.AddLog($"Changed name of {groupToChange} to '{groupToChange.GroupName}'", this);
        return new(cmd.CommandType, null);
    }

    private async Task<Command> DeleteGroup(Command cmd)
    {
        if (mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        int groupID = Convert.ToInt32(cmd.Payload);

        var (groupToDel, errorMessage) = await DBHelper.ChatGroupDB.Get(groupID);

        if(groupToDel == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        if(groupToDel.CreatorID != mainUser.UserID)
            return Helper.ErrorCmd(this, cmd, "Invalid ID");

        var (success, errorMessage2) = await DBHelper.ChatGroupDB.Delete(groupID);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage2);

        Server.DisposeChat(groupID);
        LogManager.AddLog($"Deleted group {groupToDel}", this);
        return new(cmd.CommandType, null);
    }

    private async Task<Command> GetSubcribedGroups(Command cmd)
    {
        if (mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        var (subcribedGroups, errorMessage) = await DBHelper.ChatGroupDB.GetSubcribed(mainUser.UserID);

        if(subcribedGroups == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        return new(cmd.CommandType, JsonSerializer.Serialize(subcribedGroups));
    }

    private async Task<Command> SubUnsubToGroup(Command cmd, bool subcribing)
    {
        if (mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");

        int groupID = Convert.ToInt32(cmd.Payload);
            
        var (success, errorMessage) = await DBHelper.ChatGroupDB.SubUnsub(groupID, mainUser.UserID, subcribing);

        if(!success)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        return new(cmd.CommandType, null);
    }

    private async Task<Command> GetAllGroupList(Command cmd)
    {
        var (groups, errorMessage) = await DBHelper.ChatGroupDB.GetAll();

        if(groups == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);

        return new(cmd.CommandType, JsonSerializer.Serialize(groups));
    }

    private async Task<Command> JoinGroup(Command cmd)
    {
        var (groupToJoin, errorMessage) = await DBHelper.ChatGroupDB.Get(Convert.ToInt32(cmd.Payload));

        if (groupToJoin == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        LogManager.AddLog($"Connecting to {groupToJoin}", this);
        Server.JoinChatGroup(groupToJoin, this);
        return new(cmd.CommandType, groupToJoin.Serialize());
    }

    private async Task<Command> GetGroupInfo(Command cmd)
    {
        var (requestedGroup, errorMessage) = await DBHelper.ChatGroupDB.Get(Convert.ToInt32(cmd.Payload));

        if (requestedGroup == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        return new(cmd.CommandType, requestedGroup.Serialize());
    }

    private async Task<Command> GetGroupHistory(Command cmd)
    {
        if (mainUser == null || mainUser.UserID < 1)
            return Helper.ErrorCmd(this, cmd, "Invalid mainUser");
            
        var (messages, errorMessage) = await DBHelper.MessageDB.GetHistoryGroup(Convert.ToInt32(cmd.Payload), mainUser.UserID);

        if (messages == null)
            return Helper.ErrorCmd(this, cmd, errorMessage);
        
        return new(cmd.CommandType, JsonSerializer.Serialize(messages));
    }

    private Command RemoveGroupHandler(Command cmd)
    {
        LogManager.AddLog($"Disconnecting from {groupHandler}", this);

        groupHandler?.RemoveClient(this);
        groupHandler = null;

        return new(cmd.CommandType, null);
    }

    private Command ProcessMessage(Command cmd)
    {
        if (groupHandler == null)
            return Helper.ErrorCmd(this, cmd, "Null groupHandler");

        var message = Message.Deserialize(cmd.Payload);

        if (message == null)
            return Helper.ErrorCmd(this, cmd, "Null message");

        groupHandler.EchoMessage(message, this);
        return new();
    }


    private class Helper
    {
        /// <summary>
        /// Set error command & log error based on 'errorMessage'
        /// </summary>
        public static Command ErrorCmd(ClientHandler client, Command sourceCmd, string errorDetail)
        {
            string logContent = $"Error: (Cmd: {sourceCmd.Name()}) (Detail: {errorDetail})";

            LogManager.AddLog(logContent, client);
            return new(CommandType.Error, errorDetail);
        }
    }

}