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

        Command? receivedCmd;
        Command cmdToSend = new();

        try
        {
            while(!token.IsCancellationRequested)
            {
                bytesRead = await stream.ReadAsync(memory, token);
                if(bytesRead <= 0)
                    continue;

                receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, bytesRead));
                if(receivedCmd == null)
                {
                    LogManager.AddLog("Receive invalid command while handling client");
                    return;
                }

                string? errorMessage = string.Empty;
                switch (receivedCmd.CommandType)
                {
                    case CommandType.CheckUsername:
                        if(!DbHelper.CheckUsername(receivedCmd.Payload, out errorMessage))
                        {
                            if(errorMessage.Length > 0)
                                LogManager.AddLog($"Error while checking username: {errorMessage}");
                            else
                                errorMessage = "Unavailable username";
                        }
                        break;

                    case CommandType.Register:
                        if(Register(receivedCmd.Payload, out string registeredUsername, out errorMessage))
                            LogManager.AddLog($"{endPoint} registered with username '{registeredUsername}'");
                        break;

                    case CommandType.RequestUserPwd:
                        byte[] pwdHash = new byte[MagicNumbers.pwdHashLen];
                        byte[] salt = new byte[MagicNumbers.pwdSaltLen];

                        if(DbHelper.GetUserPwd(receivedCmd.Payload, ref pwdHash, ref salt, out errorMessage))
                            cmdToSend.Payload = $"{DecodeBytes(pwdHash)}|{DecodeBytes(salt)}";
                        break;

                    case CommandType.Login:
                        if((user = Login(receivedCmd.Payload, out errorMessage)) != null)
                            LogManager.AddLog($"{endPoint} logged in as '{user.Username}'");
                        break;

                    default:
                        errorMessage = "Unknown command";
                        break;
                }

                if(!string.IsNullOrWhiteSpace(errorMessage))
                {
                    LogManager.AddLog(errorMessage);
                    cmdToSend = new(CommandType.Error, errorMessage);
                }
                else
                    cmdToSend.CommandType = receivedCmd.CommandType;

                await stream.WriteAsync(EncodeString(Command.Serialize(cmdToSend)), token);
            }
        }
        catch(OperationCanceledException) {}
        catch(Exception ex)
        {
            LogManager.AddLog($"Error while handling client: {ex.Message}");
        }
    }

    private static bool Register(string data, out string username, out string errorMessage)
    {
        username = "";

        string[] parts = data.Split('|');
        if(parts.Length != 4)
        {
            errorMessage = "Invalid data while registering";
            return false;
        }

        return DbHelper.Register(username = parts[0], parts[1], EncodeString(parts[2]), EncodeString(parts[3]), out errorMessage);
    }

    private static User? Login(string data, out string errorMessage)
    {
        string[] parts = data.Split('|');
        if(parts.Length != 2)
        {
            errorMessage = "Invalid data while logging in";
            return null;
        }

        return DbHelper.Login(parts[0], parts[1], out errorMessage);
    }
}