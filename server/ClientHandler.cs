using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using static Utilities;

class ClientHandler
{
    readonly TcpClient client;
    readonly NetworkStream stream;
    readonly EndPoint endPoint;

    bool loggedIn = false;
    int uid;
    string? nickname;
    ChatGroupHandler? chatGroup;

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

            Command? receivedCmd, cmdToSend = new();
            User? user = null, partner = null, tempUser = null;

            while((bytesRead = await stream.ReadAsync(memory, token)) > 0)
            {
                receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, bytesRead));

                switch (receivedCmd?.CommandType)
                {
                    case CommandType.Ping:
                        cmdToSend.Set(CommandType.Ping, null);
                        break;

                    case CommandType.CheckUsername:
                        (_, cmdToSend) = await CheckUsername(receivedCmd);
                        break;

                    case CommandType.Register:
                        (_, cmdToSend) = await Register(receivedCmd);
                        break;

                    case CommandType.RequestUserPwd:
                        (_, cmdToSend, tempUser) = await RequestUserPwd(receivedCmd);
                        break;

                    case CommandType.Login:
                        Login(receivedCmd, ref cmdToSend, ref user, tempUser);
                        break;

                    case CommandType.Logout:
                        cmdToSend.Set(CommandType.Logout, null);
                        LogManager.AddLog($"{endPoint} logged out from {user}");
                        user = null;
                        break;

                    case CommandType.ChangeNickname:
                        (_, cmdToSend, user) = await ChangeNickname(receivedCmd, user);
                        break;

                    case CommandType.ChangePassword:
                        (_, cmdToSend, user) = await ChangePassword(receivedCmd, user);
                        break;

                    case CommandType.RequestCreatedGroups:
                        (_, cmdToSend) = await RequestCreatedGroups(receivedCmd);
                        break;

                    case CommandType.CreateGroup:
                        (_, cmdToSend) = await CreateGroup(receivedCmd);
                        break;

                    case CommandType.RequestGroupList:
                        (_, cmdToSend) = await RequestAllGroupList(receivedCmd);
                        break;

                    case CommandType.Disconnect:
                        stream.Close();
                        client.Close();
                        LogManager.AddLog($"Client disconnected: {endPoint}");
                        return;

                    default:
                        cmdToSend.SetError("Server received invalid command");
                        LogManager.AddLog($"Error from {endPoint}: Invalid command");
                        break;
                }

                await stream.WriteAsync(EncodeString(Command.Serialize(cmdToSend)), token);
            }
        }
        catch(OperationCanceledException) {}
        catch(Exception ex)
        {
            LogManager.AddLog($"Error while handling client: {ex.Message}");
            stream.Close();
            client.Close();
            return;
        }
    }

    private async Task<(bool success, Command cmdToSend)> CheckUsername(Command receivedCmd)
    {
        Command cmdToSend = new();

        (bool success, string errorMessage) = await DbHelper.CheckUsername(receivedCmd.Payload);

        if(success)
        {
            cmdToSend.Set(receivedCmd.CommandType, null);
        }
        else
            Helper.DBErrorHandler(errorMessage, endPoint, "check username's avaibility", ref cmdToSend);

        return (success, cmdToSend);
    }

    private async Task<(bool success, Command cmdToSend)> Register(Command receivedCmd)
    {
        Command cmdToSend = new();

        User? registeredUser = User.Deserialize(receivedCmd.Payload);
        if(registeredUser == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to register: Invalid user data");
            return (false, cmdToSend);
        }

        (bool success, string errorMessage) = await DbHelper.AddUser(registeredUser);

        if(success)
        {
            cmdToSend.Set(receivedCmd.CommandType, null);
            LogManager.AddLog($"{endPoint} registered '{registeredUser}'");
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "register", ref cmdToSend);

        return (success, cmdToSend);
    }

    private async Task<(bool success, Command cmdToSend, User? requestedUser)> RequestUserPwd(Command receivedCmd)
    {
        Command cmdToSend = new();

        (bool success, string errorMessage, User? requestedUser) = await DbHelper.GetUser(receivedCmd.Payload, true);

        if(success)
        {
            if(requestedUser != null && requestedUser.PwdSet != null)
            {
                cmdToSend.Set(receivedCmd.CommandType, PasswordSet.Serialize(requestedUser.PwdSet));
                return (success, cmdToSend, requestedUser);
            }
            else
            {
                cmdToSend.SetError("Server-side error");
                LogManager.AddLog($"Error from {endPoint} trying to request password: Null returned User/PasswordSet");
            }
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "request password", ref cmdToSend);

        return (success, cmdToSend, null);
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

    private async Task<(bool success, Command cmdToSend, User? newUser)> ChangeNickname(Command receivedCmd, User? oldUser)
    {
        Command cmdToSend = new();

        if(oldUser == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to change nickname: Null User");
            return (false, cmdToSend, null);
        }
        else
        {
            User? updatedUser = new(oldUser)
            {
                Nickname = receivedCmd.Payload
            };

            (bool success, string errorMessage) = await DbHelper.UpdateUser(updatedUser);

            if(success)
            {
                cmdToSend.Set(receivedCmd.CommandType, null);
                LogManager.AddLog($"{endPoint} updated '{oldUser}': New nickname ('{oldUser.Nickname}' -> '{updatedUser.Nickname}')");
                return (success, cmdToSend, updatedUser);
            }
            else
            {
                Helper.DBErrorHandler(errorMessage, endPoint, "change nickname", ref cmdToSend);
                return (success, cmdToSend, oldUser);
            }
        }
    }

    private async Task<(bool success, Command cmdToSend, User? newUser)> ChangePassword(Command receivedCmd, User? oldUser)
    {
        Command cmdToSend = new();

        if(oldUser == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to change password: Null User");
            return (false, cmdToSend, null);
        }
        else
        {
            User? updatedUser = new(oldUser)
            {
                PwdSet = PasswordSet.Deserialize(receivedCmd.Payload)
            };

            (bool success, string errorMessage) = await DbHelper.UpdateUser(updatedUser);

            if(success)
            {
                cmdToSend.Set(receivedCmd.CommandType, null);
                LogManager.AddLog($"{endPoint} updated '{oldUser}': New password");
                return (success, cmdToSend, updatedUser);
            }
            else
            {
                Helper.DBErrorHandler(errorMessage, endPoint, "change password", ref cmdToSend);
                return (success, cmdToSend, oldUser);
            }
        }
    }

    private async Task<(bool success, Command cmdToSend)> RequestCreatedGroups(Command receivedCmd)
    {
        Command cmdToSend = new();

        (List<ChatGroup>? groups, string errorMessage) = await DbHelper.GetChatGroupByCreator(Convert.ToInt32(receivedCmd.Payload));

        if(groups != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, JsonSerializer.Serialize(groups));
            return (true, cmdToSend);
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "request created group list", ref cmdToSend);

        return (false, cmdToSend);
    }

    private async Task<(bool success, Command cmdToSend)> CreateGroup(Command receivedCmd)
    {
        Command cmdToSend = new();

        ChatGroup? chatGroup = ChatGroup.Deserialize(receivedCmd.Payload);
        if(chatGroup == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to create chat group: Invalid chat group data");
            return (false, cmdToSend);
        }

        (bool success, string errorMessage) = await DbHelper.AddChatGroup(chatGroup);

        if(success)
        {
            cmdToSend.Set(receivedCmd.CommandType, null);
            LogManager.AddLog($"{endPoint} created chat group '{chatGroup.ToString(false)}'");
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "create chat group", ref cmdToSend);

        return (success, cmdToSend);
    }

    private async Task<(bool success, Command cmdToSend)> RequestAllGroupList(Command receivedCmd)
    {
        Command cmdToSend = new();

        (List<ChatGroup>? groups, string errorMessage) = await DbHelper.GetAllChatGroup();

        if(groups != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, JsonSerializer.Serialize(groups));
            return (true, cmdToSend);
        }
        else 
            Helper.DBErrorHandler(errorMessage, endPoint, "request group list", ref cmdToSend);

        return (false, cmdToSend);
    }


    private class Helper
    {
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