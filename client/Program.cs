using System.Net;
using System.Net.Sockets;
using Org.BouncyCastle.Crypto.Parameters;
using static System.Console;
using static Utilities;

class Client
{
    public static bool UseEncryption = false;

    // const string defaultIP = "26.244.97.115";
    // const string defaultIP = "192.168.0.105";
    const string defaultIP = "127.0.0.1";
    const int defaultPort = 5000;

    static bool clientRunning = true, serverRunning = false;

    public static void Main()
    {
        string serverIP = defaultIP;
        int port = defaultPort;
        bool stopProgram = false;

        while(true)
        {
            try
            {
                ConnectServer(ref serverIP, ref port, ref stopProgram);
                if(stopProgram)
                    return;

                using TcpClient client = new(serverIP, port);
                using NetworkStream stream = client.GetStream();

                WriteLine(" Connected to server successfully.");
                ReadKey(true);

                while(true)
                {
                    WelcomeMenu(stream, ref stopProgram);
                    if(stopProgram)
                        return;
                }
            }
            catch(Exception ex)
            {
                WriteLine($" Error: ({ex.GetType().Name}) {ex.Message}");
                ReadKey(true);
            }
        }
    }

    static void ConnectServer(ref string serverIP, ref int port, ref bool stopProgram)
    {
        string? input;
        while(true)
        {
            ClientHelper.ShowConnectMenu(serverIP, port);

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    return;

                case "2":
                    Write(" Enter IP: ");
                    input = IOHelper.ReadInput(false);

                    if(input != null && ClientHelper.CheckIPv4(input))
                        serverIP = input; 
                    else
                    {
                        serverIP = defaultIP;
                        WriteLine(" Invalid IP.");
                        ReadKey(true);
                    }
                    continue;

                case "3":
                    Write(" Enter port: ");

                    try
                    {
                        port = Convert.ToInt32(ReadLine());
                        if(port < 0 || port > 65535)
                            throw new FormatException();
                    }
                    catch(FormatException)
                    {
                        port = defaultPort;
                        WriteLine(" Invalid port");
                        ReadKey(true);
                    }
                    continue;

                case "0": case null:
                    WriteLine(" Shutting down client...");
                    stopProgram = true;
                    return;

                default: continue;
            }
        }
        
    }

    static void WelcomeMenu(NetworkStream stream, ref bool stopProgram)
    {
        while(true)
        {
            ClientHelper.ShowWelcomeMenu();

            string errorMessage;
            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    if(ClientHelper.Register(stream, out errorMessage))
                    {
                        IOHelper.WriteBorder();
                        WriteLine(" Registered successfully");
                    }
                    else if(errorMessage.Length > 0)
                        WriteLine($" Error: {errorMessage}");

                    ReadKey(true);
                    continue;

                case "2":
                    

                case "0": case null:
                    WriteLine(" Shutting down client...");
                    stopProgram = true;
                    return;

                default: continue;
            }
        }
    }

    /*
    static void UserMenu(NetworkStream stream)
    {
        string? nickname = SetNickname(stream);
        if (nickname == null) return;

        while (clientRunning && serverRunning)
        {
            Clear();
            IOHelper.WriteHeader("Zelo");
            WriteLine($" Zello, {nickname}!");
            IOHelper.WriteBorder();
            WriteLine(" 1. Change nickname");
            WriteLine(" 2. Create chat room");
            WriteLine(" 3. View and join chat room");
            WriteLine(" 0. Disconnect from server");
            IOHelper.WriteBorder();
            Write(" Enter choice: ");

            switch (ReadLine())
            {
                case "1":
                    string? newNickname = SetNickname(stream);
                    if (newNickname != null) nickname = newNickname;
                    continue;

                case "2":
                    CreateChatRoom(stream);
                    continue;

                case "3":
                    string? roomName = JoinChatRoom(stream);
                    if (roomName == null) continue;

                    Chatting(stream, roomName, nickname);

                    // EncryptAndSend(stream, Command.ExitRoom, []);
                    continue;

                case "0":
                    serverRunning = false;
                    continue;

                default:
                    if (!serverRunning)
                    {
                        WriteLine("Server is down. Press any key to return.");
                        ReadKey(true);
                        return;
                    }
                    continue;
            }
        }
    }

    static string? SetNickname(NetworkStream stream)
    {
        string? nickname = "";
        bool nicknameSet = false;

        while (!nicknameSet && serverRunning)
        {
            Clear();
            IOHelper.WriteHeader("Zelo");
            Write(" Enter nickname (15 characters max): ");

            WriteLine(nickname = ReadInput(null, true));

            if (nickname == null) return null;
            if (string.IsNullOrWhiteSpace(nickname) || nickname.Length > 15)
            {
                WriteLine(" Invalid nickname.");
                continue;
            }

            EncryptAndSend(stream, Command.SetNickname, Encode(nickname));
            
            nicknameSet = ReceiveResponse(stream, Command.SetNickname, out string response);
            WriteLine($" {response}");

            ReadKey(true);
        }

        return nickname;
    }

    static void CreateChatRoom(NetworkStream stream)
    {
        bool roomCreated = false;

        while (!roomCreated && serverRunning)
        {
            Clear();
            IOHelper.WriteHeader("Zelo");
            Write(" Enter room name (30 characters max): ");

            string? roomName;
            WriteLine(roomName = ReadInput(null, true));

            if (roomName == null) return;
            if(string.IsNullOrWhiteSpace(roomName) || roomName.Length > 30)
            {
                WriteLine(" Invalid name.");
                continue;
            }

            EncryptAndSend(stream, Command.CreateRoom, Encode(roomName));

            roomCreated = ReceiveResponse(stream, Command.CreateRoom, out string response);
            WriteLine($" {response}");

            ReadKey(true);
            break;
        }
    }

    static string? JoinChatRoom(NetworkStream stream)
    {
        string? roomName = "";
        bool roomJoined = false;

        while(!roomJoined && serverRunning)
        {
            Clear();
            IOHelper.WriteHeader("Zelo");
            WriteLine(" List of rooms:");

            EncryptAndSend(stream, Command.RequestRoom, []);
            
            byte[] buffer = new byte[bufferSize];
            int bytesRead = stream.Read(buffer);

            string[] roomNames = Decode(DecryptAES(buffer.Take(bytesRead).Skip(4).ToArray(), _aes.Key, _aes.IV)).Split('|').OrderBy(name => name).ToArray();
            
            for(int i = 0, count = 1; i < roomNames.Length; i++)
                WriteLine($" {count++:00}. {roomNames[i]}");

            IOHelper.WriteBorder();
            Write(" Enter room name: ");
            WriteLine(roomName = ReadInput(null, true));
            
            if(roomName == null) return null;
            EncryptAndSend(stream, Command.RequestRoom, Encode(roomName));

            roomJoined = ReceiveResponse(stream, Command.RequestRoom, out string response);
            WriteLine($" {response}");

            ReadKey(true);
        }

        return roomName;
    }

    static void Chatting(NetworkStream stream, string roomName, string nickname)
    {
        using CancellationTokenSource exitRoomTokenSrc = new();
        string inputPrompt = $"[{nickname}] > ";
        string? input;

        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" <{roomName}>");
        IOHelper.WriteBorder();

        _ = Task.Run(() => ReceiveMsg(stream, nickname, exitRoomTokenSrc.Token));

        while (true)
        {
            input = ReadInput(inputPrompt, false);

            if (!serverRunning)
            {
                WriteLine(" Server is down.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(input))
            {
                EncryptAndSend(stream, Command.Message, Encode(input));
            }
            else if (input == null)
            {
                exitRoomTokenSrc.Cancel();
                exitRoomTokenSrc.Dispose();
                return;
            }
        }

    }

    static async Task ReceiveMsg(NetworkStream stream, string nickname, CancellationToken exitRoomToken)
    {
        byte[] buffer = new byte[bufferSize];
        Memory<byte> memory = new(buffer);
        int bytesRead;
        string response, inputPrompt = $"[{nickname}] > ";
        Command command;
        
        try
        {
            while(serverRunning && (bytesRead = await stream.ReadAsync(memory, exitRoomToken)) > 0)
            {
                exitRoomToken.ThrowIfCancellationRequested();

                command = CommandConverter(buffer.AsSpan(0, 4));
                response = Decode(DecryptAES(buffer.Take(bytesRead).Skip(4).ToArray(), _aes.Key, _aes.IV));

                switch(command)
                {
                    case Command.Message:
                        WriteMessage(response, inputPrompt);
                        continue;

                    case Command.ExitRoom:
                        return;
                    
                    case Command.Error:
                        WriteMessage($" Error from server: {response}", null);
                        continue;

                    default:
                        WriteMessage($" Unknown command received {(int) command}", null);
                        continue;
                }
            }

            serverRunning = false;
        }
        catch(OperationCanceledException) {}
        catch(Exception ex) when(serverRunning)
        {
            WriteLine($" Error while receiving msg: ({ex.GetType().Name}) {ex.Message}", null);
            ReadKey(true);
        }
    }

    static bool ReceiveResponse(NetworkStream stream, Command command, out string response)
    {
        stream.Flush();
        byte[] buffer = new byte[bufferSize];
        int bytesRead = stream.Read(buffer);

        Command responseCmd = CommandConverter(buffer.AsSpan(0, 4));
        response = Decode(DecryptAES(buffer.Take(bytesRead).Skip(4).ToArray(), _aes.Key, _aes.IV));

        return responseCmd == command;
    }

    public static void EncryptAndSend(NetworkStream stream, Command command, byte[] data)
    {
        try
        {
            byte[] dataToSend = [.. CommandConverter(command), .. EncryptAES(data, _aes.Key, _aes.IV)];
            stream.Flush();
            stream.Write(dataToSend);
        }
        catch (Exception ex)
        {
            WriteLine($" Error while sending data: {ex.Message}");
        }
    }
    */

    private class ClientHelper
    {
        public static bool CheckIPv4(string? ipAddress)
        {
            if(!IPAddress.TryParse(ipAddress, out _))
                return false;

            string[] parts = ipAddress.Split('.');
            if(parts.Length != 4) return false;

            foreach(string part in parts)
            {
                if (!int.TryParse(part, out int number))
                    return false;

                if (number < 0 || number > 255)
                    return false;

                if (part.Length > 1 && part[0] == '0')
                    return false;
            }

            return true;
        }

        public static bool Register(NetworkStream stream, out string errorMessage)
        {
            errorMessage = "";
            byte[] buffer = new byte[MagicNumbers.bufferSize];
            int bytesRead;
            string? username = null, pwd = null, confirmPwd = null;
            Command? receivedCmd, cmdToSend;

            while(true)
            {
                ShowWelcomeMenu();
                WriteLine("1");
                WriteLine();

                Write(" Enter username: ");
                if(username != null)
                    WriteLine(username);
                else
                    username = IOHelper.ReadInput(MagicNumbers.usernameMax, false);

                if(username == null)
                    return false;
                if(username.Length < MagicNumbers.usernameMin)
                {
                    WriteLine($" Error: Username must have at least {MagicNumbers.usernameMin} characters");
                    username = null;
                    ReadKey(true);
                    continue;
                }

                cmdToSend = new(CommandType.CheckUsername, username);
                stream.Write(EncodeString(Command.Serialize(cmdToSend)));

                bytesRead = stream.Read(buffer);
                receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, bytesRead));
                if(receivedCmd == null)
                {
                    errorMessage = "Received invalid command";
                    return false;
                }
                if(receivedCmd.CommandType == CommandType.Error)
                {
                    errorMessage = receivedCmd.Payload;
                    return false;
                }

                Write(" Enter password: ");
                if(pwd != null)
                    WriteLine(new string('*', pwd.Length));
                else
                    pwd = IOHelper.ReadInput(MagicNumbers.passwordMax, true);

                if(pwd == null)
                    return false;
                if(pwd.Length < MagicNumbers.passwordMin)
                {
                    WriteLine($" Error: Password must have at least {MagicNumbers.passwordMin} characters");
                    pwd = null;
                    ReadKey(true);
                    continue;
                }

                Write(" Confirm password: ");
                if(confirmPwd != null)
                    WriteLine(new string('*', confirmPwd.Length));
                else
                    confirmPwd = IOHelper.ReadInput(MagicNumbers.passwordMax, true);

                if(confirmPwd == null)
                    return false;
                if(!confirmPwd.Equals(pwd))
                {
                    WriteLine(" Error: Password confirmation failed");
                    pwd = null;
                    confirmPwd = null;
                    ReadKey(true);
                    continue;
                }

                Write(" Enter nickname (can be changed later): ");
                string? nickname = IOHelper.ReadInput(MagicNumbers.nicknameMax, false);

                if(nickname == null)
                    return false;
                if(nickname.Length < MagicNumbers.nicknameMin)
                {
                    WriteLine($" Error: Nickname must have at least {MagicNumbers.nicknameMin} characters");
                    ReadKey(true);
                    continue;
                }

                (byte[] pwdHash, byte[] salt) = HashPassword(pwd);
                cmdToSend = new(CommandType.Register, $"{username}|{nickname}|{DecodeBytes(pwdHash)}|{DecodeBytes(salt)}");
                stream.Write(EncodeString(Command.Serialize(cmdToSend)));

                bytesRead = stream.Read(buffer);
                receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, bytesRead));
                if(receivedCmd == null)
                {
                    errorMessage = "Received invalid command";
                    return false;
                }
                if(receivedCmd.CommandType == CommandType.Error)
                {
                    errorMessage = receivedCmd.Payload;
                    return false;
                }

                return true;
            }
        }

        public static void ShowConnectMenu(string serverIP, int port)
        {
            Clear();
            IOHelper.WriteHeader("Zelo");
            WriteLine($" Server's IP: {serverIP}");
            WriteLine($" Server's port: {port}");
            IOHelper.WriteBorder();
            WriteLine(" 1. Connect to server");
            WriteLine(" 2. Change IP");
            WriteLine(" 3. Change port");
            WriteLine(" 0. Shut down client");
            IOHelper.WriteBorder();
            Write(" Enter Choice: ");
        }

        public static void ShowWelcomeMenu()
        {
            Clear();
            IOHelper.WriteHeader("Zelo");
            WriteLine(" 1. Register");
            WriteLine(" 2. Login");
            WriteLine(" 0. Shut down client");
            IOHelper.WriteBorder();
            Write(" Enter Choice: ");
        }
        
        public static void ShowMainMenu()
        {
            Clear();
            IOHelper.WriteHeader("Zelo");
            WriteLine(" 1. Change nickname");
            WriteLine(" 2. Create chat room");
            WriteLine(" 3. View and join chat room");
            WriteLine(" 0. ");
            IOHelper.WriteBorder();
            Write(" Enter Choice: ");
        }
    }
}