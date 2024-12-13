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
    User? user, partner;
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
        byte[] buffer = new byte[MagicNumbers.bufferSize];
        Memory<byte> memory = new(buffer);
        int bytesRead;

        Command? receivedCmd, cmdToSend = new();

        try
        {
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
                        user = null;
                        cmdToSend.Set(CommandType.Logout, null);
                        break;

                    case CommandType.SetNickname:
                        SetNickname(receivedCmd, ref cmdToSend, ref user);
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
        }
    }

    private bool CheckUsername(Command receivedCmd, ref Command cmdToSend)
    {
        if(DbHelper.CheckUsername(receivedCmd.Payload, out string errorMessage))
        {
            cmdToSend.Set(receivedCmd.CommandType, null);
            return true;
        }
        else if(errorMessage.Length > 0)
        {
            cmdToSend.SetError("Database error while checking username");
            LogManager.AddLog($"DB error from {endPoint} checking username: {errorMessage}");
        }
        else
            cmdToSend.SetError("Unavailable username");

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
        else if(errorMessage.Length > 0)
        {
            cmdToSend.SetError("Database error while registering");
            LogManager.AddLog($"DB error from {endPoint} registering: {errorMessage}");
        }
        else
            cmdToSend.SetError("Registered unsuccessfully");

        return false;
    }

    private bool RequestUserPwd(Command receivedCmd, ref Command cmdToSend)
    {
        PasswordSet? pwdSet = DbHelper.GetUserPwd(receivedCmd.Payload, out string errorMessage);

        if(pwdSet != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, PasswordSet.Serialize(pwdSet));
            return true;
        }
        else if(errorMessage.Length > 0)
        {
            cmdToSend.SetError("Database error while getting password");
            LogManager.AddLog($"DB error from {endPoint} getting pwd for '{receivedCmd.Payload}': {errorMessage}");
        }
        else
            cmdToSend.SetError("No user found");

        return false;
    }

    private bool Login(Command receivedCmd, ref Command cmdToSend, out User? loggedInUser)
    {
        if((loggedInUser = DbHelper.Login(receivedCmd.Payload, out string errorMessage)) != null)
        {
            cmdToSend.Set(receivedCmd.CommandType, User.Serialize(loggedInUser));
            LogManager.AddLog($"{endPoint} logged in as '{loggedInUser}'");
            return true;
        }
        else if(errorMessage.Length > 0)
        {
            cmdToSend.SetError("Database error while trying to login");
            LogManager.AddLog($"DB error from {endPoint} trying to login as '{receivedCmd.Payload}': {errorMessage}");
        }
        else
        {
            cmdToSend.SetError("Logged in unsuccessfully");
            LogManager.AddLog($"{endPoint} failed to login as '{receivedCmd.Payload}'");
        }

        return false;
    }

    private bool SetNickname(Command receivedCmd, ref Command cmdToSend, ref User? user)
    {
        if(user == null)
        {
            cmdToSend.SetError("Server-side error while changing nickname");
            LogManager.AddLog($"Error from {endPoint} changing nickname: Null user");
        }
        else
        {
            string oldNickname = user.Nickname;

            if(DbHelper.SetNickname(user, receivedCmd.Payload, out string errorMessage))
            {
                user.Nickname = receivedCmd.Payload;
                cmdToSend.Set(receivedCmd.CommandType, null);
                LogManager.AddLog($"{endPoint} changed nickname of '{user.Username}' from '{oldNickname}' to '{user.Nickname}");
                return true;
            }
            else if(errorMessage.Length > 0)
            {
                cmdToSend.SetError("Database error while changing nickname");
                LogManager.AddLog($"DB error from {endPoint} changing nickname: {errorMessage}");
            }
            else
            {
                cmdToSend.SetError("Logged in unsuccessfully");
                LogManager.AddLog($"{endPoint} failed to change nickname of '{user.Username}");
            }
        }

        return false;
    }

}