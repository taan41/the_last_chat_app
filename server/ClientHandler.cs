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
    ChatGroup? chatGroup;

    public ClientHandler(TcpClient _client)
    {
        client = _client;
        stream = _client.GetStream();
        endPoint = _client.Client.RemoteEndPoint!;

        LogManager.AddLog($"Client connected: {endPoint}");
    }

    public async Task Run(CancellationToken token)
    {
        byte[] buffer = new byte[MagicNumbers.bufferSize];
        Memory<byte> memory = new(buffer);
        int bytesRead;

        Command? receivedCmd;
        Command cmdToSend = new();

        try
        {
            while((bytesRead = await stream.ReadAsync(memory, token)) > 0)
            {
                if(token.IsCancellationRequested)
                    break;

                receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, bytesRead));
                if(receivedCmd == null)
                {
                    LogManager.AddLog("Receive invalid command while handling client");
                    return;
                }

                string? errorMessage;
                switch (receivedCmd.CommandType)
                {
                    case CommandType.Register:
                        if(Register(receivedCmd.Payload, out string registeredUsername, out errorMessage))
                            LogManager.AddLog($"{endPoint} registered with username '{registeredUsername}'");
                        break;

                    case CommandType.Login:
                        if((user = Login(receivedCmd.Payload, out errorMessage)) != null)
                            LogManager.AddLog($"{endPoint} logged in as '{user.Username}'");
                        break;

                    default:
                        errorMessage = "Unknown command";
                        break;
                }

                if(!string.IsNullOrEmpty(errorMessage))
                {
                    LogManager.AddLog(errorMessage);
                    cmdToSend = new(CommandType.Error, errorMessage);
                }
                else
                    cmdToSend.CommandType = receivedCmd.CommandType;

                await stream.WriteAsync(EncodeString(Command.Serialize(cmdToSend)), token);
            }
        }
        catch(Exception)
        {}
    }

    private static bool Register(string data, out string username, out string errorMessage)
    {
        username = "";

        string[] parts = data.Split('|');
        if(parts.Length != 3)
        {
            errorMessage = "Invalid data while registtering";
            return false;
        }

        return DbHelper.Register(username = parts[0], parts[1], parts[2], out errorMessage);
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