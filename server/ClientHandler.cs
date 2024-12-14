using System.Net;
using System.Net.Sockets;
using System.Text.Json;

using static Utilities;

class ClientHandler
{
    readonly TcpClient client;
    readonly NetworkStream stream;
    readonly EndPoint endPoint;

    ChatGroupHandler? groupHandler;
    User? mainUser = null;

    public EndPoint EndPoint => endPoint;
    public User? User => mainUser;

    public ClientHandler(TcpClient _client)
    {
        client = _client;
        stream = _client.GetStream();
        endPoint = _client.Client.RemoteEndPoint!;

        LogManager.AddLog($"Client connected: {endPoint}");
    }

    public async Task HandlingClientAsync(CancellationToken token)
    {
        try
        {
            byte[] buffer = new byte[MagicNumbers.bufferSize];
            Memory<byte> memory = new(buffer, 0, buffer.Length);
            int bytesRead;

            Command? receivedCmd;
            Command cmdToSend = new();

            User? partner = null, tempUser = null;

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
                        Login(receivedCmd, ref cmdToSend, ref mainUser, tempUser);
                        break;

                    case CommandType.Logout:
                        cmdToSend.Set(CommandType.Logout, null);
                        LogManager.AddLog($"{endPoint} logged out from {mainUser}");
                        mainUser = null;
                        break;

                    case CommandType.ChangeNickname:
                        (cmdToSend, mainUser) = await ChangeNickname(receivedCmd, mainUser);
                        break;

                    case CommandType.ChangePassword:
                        (cmdToSend, mainUser) = await ChangePassword(receivedCmd, mainUser);
                        break;
                        
                    // GetUserList, UserAsPartner, GetPartnerHistory, DeletePartner,
                    case CommandType.GetUserList:
                        cmdToSend = await GetUserList(receivedCmd);
                        break;

                    case CommandType.SetPartner:
                        (cmdToSend, partner) = await SetPartner(receivedCmd, mainUser);
                        break;

                    case CommandType.GetPartnerHistory:
                        cmdToSend = await GetPartnerHistory(receivedCmd, mainUser, partner);
                        break;

                    case CommandType.RemovePartner:
                        cmdToSend = RemovePartner(receivedCmd);
                        partner = null;
                        break;

                    case CommandType.GetCreatedGroups:
                        cmdToSend = await GetCreatedGroups(receivedCmd);
                        break;

                    case CommandType.CreateGroup:
                        cmdToSend = await CreateGroup(receivedCmd);
                        break;

                    case CommandType.DeleteGroup:
                        if(mainUser != null && mainUser.UID != -1)
                            cmdToSend = await DeleteGroup(receivedCmd, mainUser.UID);
                        break;

                    case CommandType.GetGroupList:
                        cmdToSend = await GetAllGroupList(receivedCmd);
                        break;

                    case CommandType.GetGroupInfo:
                        cmdToSend = await GetGroupInfo(receivedCmd);
                        break;

                    case CommandType.GetGroupHistory:
                        cmdToSend = await GetGroupHistory(receivedCmd);
                        break;

                    case CommandType.JoinGroup:
                        cmdToSend = await JoinGroup(receivedCmd);
                        break;

                    case CommandType.LeaveGroup:
                        cmdToSend = LeaveGroup(receivedCmd);
                        break;

                    case CommandType.Message:
                        cmdToSend = await ProcessMessage(receivedCmd);
                        break;

                    case CommandType.Disconnect:
                        LogManager.AddLog($"Client disconnected: {endPoint}");
                        return;

                    default:
                        cmdToSend.SetError("Server received invalid command");
                        LogManager.AddLog($"Error from {endPoint} sending command: Invalid command");
                        break;
                }

                if (cmdToSend.CommandType != CommandType.Empty)
                {
                    await stream.WriteAsync(EncodeString(Command.Serialize(cmdToSend)), token);
                    cmdToSend.Set(CommandType.Empty, null);
                }
            }
        }
        catch(OperationCanceledException)
        {
            LogManager.AddLog($"Forced disconnect: {endPoint}");
        }
        catch(Exception ex)
        {
            LogManager.AddLog($"Error from {endPoint} while handling client: {ex.Message}");
        }
        finally
        {
            stream.Close();
            client.Close();
        }
    }

    public void SetUpGroupHandler(ChatGroupHandler? _groupHandler)
    {
        groupHandler = _groupHandler;
    }

    public async Task EchoMessage(Message message)
    {
        await stream.WriteAsync(EncodeString(Command.Serialize(new(CommandType.MessageEcho, Message.Serialize(message)))));
    }

    private async Task<Command> CheckUsername(Command receivedCmd)
    {
        Command cmdToSend = new();

        (bool success, string errorMessage) = await DbHelper.CheckUsername(receivedCmd.Payload);

        if(success)
        {
            cmdToSend.Set(receivedCmd.CommandType, null);
        }
        else
            Helper.DBErrorHandler(errorMessage, endPoint, "check username's avaibility", ref cmdToSend);

        return cmdToSend;
    }

    private async Task<Command> Register(Command receivedCmd)
    {
        Command cmdToSend = new();

        User? registeredUser = User.Deserialize(receivedCmd.Payload);
        if(registeredUser == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to register: Invalid user data");
            return cmdToSend;
        }

        (bool success, string errorMessage) = await DbHelper.AddUser(registeredUser);

        if(success)
        {
            (registeredUser, _) = await DbHelper.GetUser(registeredUser!.Username, false);
            cmdToSend.Set(receivedCmd.CommandType, null);
            LogManager.AddLog($"{endPoint} registered '{registeredUser}'");
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "register", ref cmdToSend);

        return cmdToSend;
    }

    private async Task<(Command cmdToSend, User? requestedUser)> GetUserPwd(Command receivedCmd)
    {
        Command cmdToSend = new();

        (User? requestedUser, string errorMessage) = await DbHelper.GetUser(receivedCmd.Payload, true);

        if(requestedUser != null)
        {
            if(requestedUser != null && requestedUser.PwdSet != null)
            {
                cmdToSend.Set(receivedCmd.CommandType, PasswordSet.Serialize(requestedUser.PwdSet));
                return (cmdToSend, requestedUser);
            }
            else
            {
                cmdToSend.SetError("Server-side error");
                LogManager.AddLog($"Error from {endPoint} trying to request password: Null returned User/PasswordSet");
            }
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "request password", ref cmdToSend);

        return (cmdToSend, null);
    }

    private bool Login(Command receivedCmd, ref Command cmdToSend, ref User? user, User? tempUser)
    {
        if(user == null && tempUser != null)
        {
            user = tempUser;
            cmdToSend.Set(receivedCmd.CommandType, User.Serialize(user));
            LogManager.AddLog($"{endPoint} logged in as '{user}'");
            return true;
        }
        else
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to log in: Invalid user/tempUser");
            return false;
        }
    }

    private async Task<(Command cmdToSend, User? newUser)> ChangeNickname(Command receivedCmd, User? oldUser)
    {
        Command cmdToSend = new();

        if(oldUser == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to change nickname: Null User");
            return (cmdToSend, null);
        }
        else
        {
            User updatedUser = new(oldUser)
            {
                Nickname = receivedCmd.Payload
            };

            (bool success, string errorMessage) = await DbHelper.UpdateUser(updatedUser);

            if(success)
            {
                cmdToSend.Set(receivedCmd.CommandType, null);
                LogManager.AddLog($"{endPoint} updated '{updatedUser}': New nickname ('{oldUser.Nickname}' -> '{updatedUser.Nickname}')");
                return (cmdToSend, updatedUser);
            }
            else
            {
                Helper.DBErrorHandler(errorMessage, endPoint, "change nickname", ref cmdToSend);
                return (cmdToSend, oldUser);
            }
        }
    }

    private async Task<(Command cmdToSend, User? newUser)> ChangePassword(Command receivedCmd, User? oldUser)
    {
        Command cmdToSend = new();

        if(oldUser == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to change password: Null User");
            return (cmdToSend, null);
        }
        else
        {
            User updatedUser = new(oldUser)
            {
                PwdSet = PasswordSet.Deserialize(receivedCmd.Payload)
            };

            (bool success, string errorMessage) = await DbHelper.UpdateUser(updatedUser);

            if(success)
            {
                cmdToSend.Set(receivedCmd.CommandType, null);
                LogManager.AddLog($"{endPoint} updated '{updatedUser}': New password");
                return (cmdToSend, updatedUser);
            }
            else
            {
                Helper.DBErrorHandler(errorMessage, endPoint, "change password", ref cmdToSend);
                return (cmdToSend, oldUser);
            }
        }
    }

    private async Task<Command> GetUserList(Command receivedCmd)
    {
        Command cmdToSend = new();

        (List<User>? users, string errorMessage) = await DbHelper.GetAllUser(false);

        if(users != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, JsonSerializer.Serialize(users));
            return cmdToSend;
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "request user list", ref cmdToSend);

        return cmdToSend;
    }

    private async Task<(Command, User?)> SetPartner(Command receivedCmd, User? mainUser)
    {
        Command cmdToSend = new();
        User? partner = null;

        if(mainUser == null || mainUser.UID == -1)
        {
            cmdToSend.SetError("Invalid main user data");
            return (cmdToSend, partner);
        }

        int partnerID = Convert.ToInt32(receivedCmd.Payload);

        (partner, string errorMessage) = await DbHelper.GetUser(partnerID, false);

        if(partner == null)
        {
            Helper.DBErrorHandler(errorMessage, endPoint, "set partner", ref cmdToSend);
        }
        else
        {
            Server.JoinPrivate(this, mainUser.UID, partnerID);
            cmdToSend.Set(receivedCmd.CommandType, User.Serialize(partner));
        }

        return (cmdToSend, partner);
    }

    private async Task<Command> GetPartnerHistory(Command receivedCmd, User? mainUser, User? partner)
    {
        Command cmdToSend = new();

        if(mainUser == null || partner == null)
        {
            cmdToSend.SetError("Invalid user data");
            return cmdToSend;
        }

        (List<Message>? messages, string errorMessage) = await DbHelper.GetPrivateMessageHistory(mainUser.UID, partner.UID);

        if (messages != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, JsonSerializer.Serialize(messages));
        }
        else
        {
            Helper.DBErrorHandler(errorMessage, endPoint, $"request '{mainUser.UID}' & '{partner.UID}' history", ref cmdToSend);
        }

        return cmdToSend;
    }

    private Command RemovePartner(Command receivedCmd)
    {
        Command cmdToSend = new(receivedCmd.CommandType, null);

        groupHandler?.RemoveClient(this);
        groupHandler = null;

        return cmdToSend;
    }

    private async Task<Command> GetCreatedGroups(Command receivedCmd)
    {
        Command cmdToSend = new();

        (List<ChatGroup>? groups, string errorMessage) = await DbHelper.GetChatGroupByCreator(Convert.ToInt32(receivedCmd.Payload));

        if(groups != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, JsonSerializer.Serialize(groups));
            return cmdToSend;
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "request created group list", ref cmdToSend);

        return cmdToSend;
    }

    private async Task<Command> CreateGroup(Command receivedCmd)
    {
        Command cmdToSend = new();

        ChatGroup? chatGroup = ChatGroup.Deserialize(receivedCmd.Payload);
        if(chatGroup == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to create chat group: Invalid chat group data");
            return cmdToSend;
        }

        (bool success, string errorMessage) = await DbHelper.AddChatGroup(chatGroup);

        if(success)
        {
            cmdToSend.Set(receivedCmd.CommandType, null);
            LogManager.AddLog($"{endPoint} created chat group with name '{chatGroup.GroupName}'");
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "create chat group", ref cmdToSend);

        return cmdToSend;
    }

    private async Task<Command> DeleteGroup(Command receivedCmd, int userID)
    {
        Command cmdToSend = new();
        int groupID = Convert.ToInt32(receivedCmd.Payload);

        (ChatGroup? groupToDel, string _) = await DbHelper.GetChatGroup(groupID);

        if(groupToDel == null || groupToDel.CreatorID != userID)
        {
            cmdToSend.SetError("Invalid chat group ID");
            return cmdToSend;
        }

        (bool success, string errorMessage) = await DbHelper.DeleteChatGroup(groupID);

        if(success)
        {
            Server.DisposeChatGroup(groupID);
            cmdToSend.Set(receivedCmd.CommandType, null);
            LogManager.AddLog($"{endPoint} deleted chat group {groupToDel.ToString(false)}");
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "delete chat group", ref cmdToSend);

        return cmdToSend;
    }

    private async Task<Command> GetAllGroupList(Command receivedCmd)
    {
        Command cmdToSend = new();

        (List<ChatGroup>? groups, string errorMessage) = await DbHelper.GetAllChatGroup();

        if(groups != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, JsonSerializer.Serialize(groups));
            return cmdToSend;
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "request group list", ref cmdToSend);

        return cmdToSend;
    }

    private async Task<Command> JoinGroup(Command receivedCmd)
    {
        Command cmdToSend = new();
        int groupID = Convert.ToInt32(receivedCmd.Payload);

        (ChatGroup? groupToJoin, string errorMessage) = await DbHelper.GetChatGroup(groupID);

        if (groupToJoin != null)
        {
            Server.JoinChatGroup(groupToJoin, this);
            cmdToSend.Set(receivedCmd.CommandType, ChatGroup.Serialize(groupToJoin));
        }
        else
        {
            Helper.DBErrorHandler(errorMessage, endPoint, "join chat group", ref cmdToSend);
        }

        return cmdToSend;
    }

    private async Task<Command> GetGroupInfo(Command receivedCmd)
    {
        Command cmdToSend = new();
        int groupID = Convert.ToInt32(receivedCmd.Payload);

        (ChatGroup? requestedGroup, string errorMessage) = await DbHelper.GetChatGroup(groupID);

        if (requestedGroup != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, ChatGroup.Serialize(requestedGroup));
        }
        else
        {
            Helper.DBErrorHandler(errorMessage, endPoint, "request group info", ref cmdToSend);
        }

        return cmdToSend;
    }

    private async Task<Command> GetGroupHistory(Command receivedCmd)
    {
        Command cmdToSend = new();
        int groupID = Convert.ToInt32(receivedCmd.Payload);

        (List<Message>? messages, string errorMessage) = await DbHelper.GetGroupHistory(groupID);

        if (messages != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, JsonSerializer.Serialize(messages));
        }
        else
        {
            Helper.DBErrorHandler(errorMessage, endPoint, "request group history", ref cmdToSend);
        }

        return cmdToSend;
    }

    private Command LeaveGroup(Command receivedCmd)
    {
        Command cmdToSend = new(receivedCmd.CommandType, null);

        groupHandler?.RemoveClient(this);
        groupHandler = null;

        return cmdToSend;
    }

    private async Task<Command> ProcessMessage(Command receivedCmd)
    {
        Command cmdToSend = new();
        
        if (groupHandler != null)
        {
            Message? receivedMsg = Message.Deserialize(receivedCmd.Payload);

            if (receivedMsg == null)
            {
                cmdToSend.SetError("Invalid message data");
                LogManager.AddLog($" Error from {endPoint} trying to send msg: Invalid msg data");
                return cmdToSend;
            }

            await groupHandler.EchoMessage(receivedMsg, this);
        }
        else
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($" Error from {endPoint} trying to send msg: Null groupHandler");
        }

        return cmdToSend;
    }


    private class Helper
    {
        /// <summary>
        /// Set error command & log error based on 'errorMessage'
        /// </summary>
        public static void DBErrorHandler(string errorMessage, EndPoint endPoint, string action, ref Command cmdToSend)
        {
            if(errorMessage.Length > 0)
            {
                cmdToSend.SetError(errorMessage);
                LogManager.AddLog($"DB error from {endPoint} trying to {action}: {errorMessage}");
            }
            else
            {
                cmdToSend.SetError($"Server-side error");
                LogManager.AddLog($"{endPoint} failed to {action}");
            }
        }
    }

}