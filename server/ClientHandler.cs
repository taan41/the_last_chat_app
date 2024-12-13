using System.Net;
using System.Net.Sockets;
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
            User? user = null, partner = null;

            while(!token.IsCancellationRequested)
            {
                bytesRead = await stream.ReadAsync(memory, token);
                if(bytesRead <= 0)
                    continue;

                receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, bytesRead));

                switch (receivedCmd?.CommandType)
                {
                    case CommandType.Ping:
                        cmdToSend.Set(CommandType.Ping, null);
                        break;

                    case CommandType.CheckUsername:
                        CheckUsername(receivedCmd, ref cmdToSend);
                        break;

                    case CommandType.Register:
                        Register(receivedCmd, ref cmdToSend);
                        break;

                    case CommandType.RequestUserPwd:
                        RequestUserPwd(receivedCmd, ref cmdToSend);
                        break;

                    case CommandType.Login:
                        Login(receivedCmd, ref cmdToSend, out user);
                        break;

                    case CommandType.Logout:
                        cmdToSend.Set(CommandType.Logout, null);
                        LogManager.AddLog($"{endPoint} logged out from {user}");
                        user = null;
                        break;

                    case CommandType.ChangeNickname:
                        ChangeNickname(receivedCmd, ref cmdToSend, ref user);
                        break;

                    case CommandType.ChangePassword:
                        ChangePassword(receivedCmd, ref cmdToSend, ref user);
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

    private bool CheckUsername(Command receivedCmd, ref Command cmdToSend)
    {
        if(DbHelper.CheckUsername(receivedCmd.Payload, out string errorMessage))
        {
            cmdToSend.Set(receivedCmd.CommandType, null);
            return true;
        }
        else
            Helper.ErrorHandler(errorMessage, endPoint, "check username's avaibility", ref cmdToSend);

        return false;
    }

    private bool Register(Command receivedCmd, ref Command cmdToSend)
    {
        User? registeredUser = User.Deserialize(receivedCmd.Payload);

        if(registeredUser == null)
        {
            cmdToSend.SetError("Invalid user data");
            LogManager.AddLog($"Error from {endPoint} registering: Invalid user data");
            return false;
        }

        if(DbHelper.Register(registeredUser, out string errorMessage))
        {
            cmdToSend.Set(receivedCmd.CommandType, null);
            LogManager.AddLog($"{endPoint} registered with username '{registeredUser.Username}'");
            return true;
        }
        else 
            Helper.ErrorHandler(errorMessage, endPoint, "register", ref cmdToSend);

        return false;
    }

    private bool RequestUserPwd(Command receivedCmd, ref Command cmdToSend)
    {
        if(DbHelper.GetUserPwd(receivedCmd.Payload, out PasswordSet? pwdSet, out string errorMessage))
        {
            if(pwdSet != null)
            {
                cmdToSend.Set(receivedCmd.CommandType, PasswordSet.Serialize(pwdSet));
                return true;
            }
            else
            {
                cmdToSend.SetError("Server-side error");
                LogManager.AddLog($"Error from {endPoint} trying to request password: Returned PasswordSet is null");
            }
        }
        else 
            Helper.ErrorHandler(errorMessage, endPoint, "request password", ref cmdToSend);

        return false;
    }

    private bool Login(Command receivedCmd, ref Command cmdToSend, out User? loggedInUser)
    {
        if(DbHelper.Login(receivedCmd.Payload, out loggedInUser, out string errorMessage))
        {
            if(loggedInUser != null)
            {
                cmdToSend.Set(receivedCmd.CommandType, User.Serialize(loggedInUser));
                LogManager.AddLog($"{endPoint} logged in as '{loggedInUser}'");
                return true;
            }
            else
            {
                cmdToSend.SetError("Server-side error");
                LogManager.AddLog($"Error from {endPoint} trying to log in: Returned User is null");
            }
        }
        else
            Helper.ErrorHandler(errorMessage, endPoint, "log in", ref cmdToSend);

        return false;
    }

    private bool ChangeNickname(Command receivedCmd, ref Command cmdToSend, ref User? user)
    {
        if(user == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to change nickname: Null User");
        }
        else
        {
            string oldNickname = user.Nickname;

            if(DbHelper.ChangeNickname(user, receivedCmd.Payload, out string errorMessage))
            {
                user.Nickname = receivedCmd.Payload;
                cmdToSend.Set(receivedCmd.CommandType, null);
                LogManager.AddLog($"{endPoint} updated '{user}': New nickname ('{oldNickname}' -> '{user.Nickname}')");
                return true;
            }
            else
                Helper.ErrorHandler(errorMessage, endPoint, "change nickname", ref cmdToSend);
        }

        return false;
    }

    private bool ChangePassword(Command receivedCmd, ref Command cmdToSend, ref User? user)
    {
        PasswordSet? receivedPwdSet = PasswordSet.Deserialize(receivedCmd.Payload);
        if(user == null || receivedCmd.Payload == null || receivedPwdSet == null)
        {
            cmdToSend.SetError("Server-side error");
            LogManager.AddLog($"Error from {endPoint} trying to change password: Null User/Payload/PasswordSet");
        }
        else
        {
            if(DbHelper.ChangePassword(user, receivedPwdSet, out string errorMessage))
            {
                cmdToSend.Set(receivedCmd.CommandType, null);
                LogManager.AddLog($"{endPoint} updated '{user}': New password");
                return true;
            }
            else
                Helper.ErrorHandler(errorMessage, endPoint, "change password", ref cmdToSend);
        }

        return false;
    }


    private class Helper
    {
        public static void ErrorHandler(string errorMessage, EndPoint endPoint, string action, ref Command cmdToSend)
        {
            if(errorMessage.Length > 0)
            {
                cmdToSend.SetError(errorMessage);
                LogManager.AddLog($"DB error from {endPoint} trying to {action}: {errorMessage}");
            }
            else
            {
                cmdToSend.SetError($"Server-side error trying to {action}");
                LogManager.AddLog($"{endPoint} failed to {action}");
            }
        }
    }

}