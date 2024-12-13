using System.Net;
using System.Net.Sockets;

using static System.Console;
using static Utilities;

class Client
{
    // const string defaultIP = "26.244.97.115";
    // const string defaultIP = "192.168.0.105";
    const string defaultIP = "127.0.0.1";
    const int defaultPort = 5000;

    public static void Main()
    {
        string serverIP = defaultIP;
        int port = defaultPort;
        bool stopProgram = false;

        while(true)
        {
            try
            {
                ConnectServerMenu(ref serverIP, ref port, out stopProgram);
                if(stopProgram)
                    return;

                using TcpClient client = new(serverIP, port);
                using NetworkStream stream = client.GetStream();
                stream.ReadTimeout = MagicNumbers.streamTimeOut;
                stream.WriteTimeout = MagicNumbers.streamTimeOut;

                WriteLine(" Connected to server successfully.");
                ReadKey(true);

                while(true)
                {
                    WelcomeMenu(stream, out User? loggedInUser, out stopProgram);
                    if(stopProgram || loggedInUser == null)
                        return;

                    UserMenu(stream, loggedInUser);
                }
            }
            catch(Exception ex)
            {
                WriteLine($" Error: ({ex.GetType().Name}) {ex.Message}");
                ReadKey(true);
            }
        }
    }

    static void ConnectServerMenu(ref string serverIP, ref int port, out bool stopProgram)
    {
        stopProgram = false;
        string? input;

        while(true)
        {
            Helper.ShowMenu.ConnectMenu(serverIP, port);

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    return;

                case "2":
                    Write(" Enter IP: ");
                    input = IOHelper.ReadInput(false);

                    if(input != null && Helper.Misc.CheckIPv4(input))
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

    static void WelcomeMenu(NetworkStream stream, out User? loggedInUser, out bool stopProgram)
    {
        loggedInUser = null;
        stopProgram = false;

        while(true)
        {
            Helper.ShowMenu.WelcomeMenu();

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    Helper.ClientAction.Register(stream);
                    continue;

                case "2":
                    Helper.ClientAction.Login(stream, out loggedInUser);
                    if(loggedInUser != null)
                        return;
                    continue;

                case "0": case null:
                    WriteLine(" Shutting down client...");
                    Helper.CommandHandler.SendDisconnect(stream);
                    stopProgram = true;
                    return;

                default: continue;
            }
        }
    }

    static void UserMenu(NetworkStream stream, User loggedInUser)
    {
        byte[] buffer = new byte[MagicNumbers.bufferSize];
        string? nickname = loggedInUser.Nickname;
        Command receivedCmd, cmdToSend = new();

        while (true)
        {
            Helper.ShowMenu.UserMenu(nickname);

            switch (ReadLine())
            {
                case "1":
                    Helper.ClientAction.ChangeNickname(stream, ref loggedInUser);
                    continue;

                case "2":
                    Helper.ClientAction.ChangePassword(stream, ref loggedInUser);
                    continue;

                case "3":
                    // string? roomName = JoinChatRoom(stream);
                    // if (roomName == null) continue;

                    // Chatting(stream, roomName, nickname);

                    // EncryptAndSend(stream, Command.ExitRoom, []);
                    continue;

                case "0": case null:
                    WriteLine(" Logging out...");
                    cmdToSend.Set(CommandType.Logout, null);
                    Helper.CommandHandler.Stream(stream, buffer, cmdToSend, out _);
                    return;

                default:
                    continue;
            }
        }
    }

    /*
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

    private static class Helper
    {
        public static class ClientAction
        {
            public static void Register(NetworkStream stream)
            {
                byte[] buffer = new byte[MagicNumbers.bufferSize];
                string? username = null, pwd = null, confirmPwd = null;
                Command cmdToSend = new();

                while(true)
                {
                    ShowMenu.WelcomeMenu();
                    WriteLine("1");
                    IOHelper.WriteBorder();

                    Write(" Enter username   : ");
                    if(username != null)
                        WriteLine(username);
                    else
                        switch(Misc.InputData(ref username, "Username", MagicNumbers.usernameMin, MagicNumbers.usernameMax, false))
                        {
                            case null: return;
                            case true: break;
                            case false:
                                username = null;
                                continue;
                        }

                    // Check availability of username
                    cmdToSend.Set(CommandType.CheckUsername, username);
                    if(!CommandHandler.Stream(stream, buffer, cmdToSend, out _))
                    {
                        username = null;
                        continue;
                    }

                    Write(" Enter password   : ");
                    if(pwd != null)
                        WriteLine(new string('*', pwd.Length));
                    else
                        switch(Misc.InputData(ref pwd, "Password", MagicNumbers.passwordMin, MagicNumbers.passwordMax, true))
                        {
                            case null: return;
                            case true: break;
                            case false:
                                pwd = null;
                                continue;
                        }

                    Write(" Confirm password : ");
                    if(confirmPwd != null)
                        WriteLine(new string('*', confirmPwd.Length));
                    else
                        switch(Misc.InputData(ref confirmPwd, "Password", MagicNumbers.passwordMin, MagicNumbers.passwordMax, true))
                        {
                            case null: return;
                            case true: break;
                            case false:
                                confirmPwd = null;
                                continue;
                        }
                    
                    if(!confirmPwd!.Equals(pwd))
                    {
                        WriteLine(" Error: Mis-match confirm password");
                        pwd = null;
                        confirmPwd = null;
                        ReadKey(true);
                        continue;
                    }

                    Write(" Enter nickname   : ");
                    string? nickname = null;
                    switch(Misc.InputData(ref nickname, "Nickname", MagicNumbers.nicknameMin, MagicNumbers.nicknameMax, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    User registeredUser = new(null, username!, nickname!, HashPassword(pwd));

                    cmdToSend.Set(CommandType.Register, User.Serialize(registeredUser));
                    if(CommandHandler.Stream(stream, buffer, cmdToSend, out _))
                    {
                        IOHelper.WriteBorder();
                        WriteLine(" Registered successfully!");
                    }

                    ReadKey(true);
                    return;
                }
            }

            public static void Login(NetworkStream stream, out User? loggedInUser)
            {
                byte[] buffer = new byte[MagicNumbers.bufferSize];
                Command cmdToSend = new();
                PasswordSet? pwdSet;
                loggedInUser = null;
                
                while(true)
                {
                    ShowMenu.WelcomeMenu();
                    WriteLine("2");
                    IOHelper.WriteBorder();

                    Write(" Enter username: ");
                    string? username = null;
                    switch(Misc.InputData(ref username, "Username", MagicNumbers.nicknameMin, MagicNumbers.nicknameMax, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    cmdToSend.Set(CommandType.RequestUserPwd, username);

                    // Check if username exists and get password hash/salt of that username
                    if(CommandHandler.Stream(stream, buffer, cmdToSend, out Command receivedCmd))
                    {
                        pwdSet = PasswordSet.Deserialize(receivedCmd.Payload);
                        if(pwdSet == null)
                        {
                            WriteLine(" Error: Received invalid password");
                            ReadKey(true);
                            continue;
                        }
                    }
                    else continue;

                    Write(" Enter password: ");
                    string? pwd = null;
                    switch(Misc.InputData(ref pwd, "Password", MagicNumbers.passwordMin, MagicNumbers.passwordMax, true))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    if(VerifyPassword(pwd!, pwdSet))
                    {
                        cmdToSend = new(CommandType.Login, username);

                        if(CommandHandler.Stream(stream, buffer, cmdToSend, out receivedCmd))
                        {
                            loggedInUser = User.Deserialize(receivedCmd.Payload);

                            if(loggedInUser == null)
                            {
                                WriteLine(" Error: Received invalid user data");
                                ReadKey(true);
                                return;
                            }
                            else
                            {
                                loggedInUser.PwdSet = pwdSet;
                                IOHelper.WriteBorder();
                                WriteLine(" Logged in successfully!");
                                ReadKey(true);
                                return;
                            }
                        }
                    }
                    else
                    {
                        WriteLine(" Error: Wrong password");
                        ReadKey(true);
                        continue;
                    }
                }
            }

            public static void ChangeNickname(NetworkStream stream, ref User user)
            {
                byte[] buffer = new byte[MagicNumbers.bufferSize];

                while(true)
                {
                    ShowMenu.UserMenu(user.Nickname);
                    WriteLine("1");
                    IOHelper.WriteBorder();

                    Write(" Enter new nickname: ");
                    string? newNickname = null;
                    switch(Misc.InputData(ref newNickname, "Nickname", MagicNumbers.nicknameMin, MagicNumbers.nicknameMax, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    if(CommandHandler.Stream(stream, buffer, new(CommandType.ChangeNickname, newNickname), out _))
                    {
                        user.Nickname = newNickname!;
                        IOHelper.WriteBorder();
                        WriteLine(" Changed nickname successfully");
                        ReadKey(true);
                        return;
                    }
                    else continue;
                }
            }

            public static void ChangePassword(NetworkStream stream, ref User user)
            {
                if(user.PwdSet == null)
                {
                    WriteLine(" Error: Null PasswordSet");
                    return;
                }

                byte[] buffer = new byte[MagicNumbers.bufferSize];
                string? oldPwd = null, newPwd = null;

                while(true)
                {
                    ShowMenu.UserMenu(user.Nickname);
                    WriteLine("2");
                    IOHelper.WriteBorder();

                    Write(" Enter old password   : ");
                    if(oldPwd != null)
                        WriteLine(new string('*', oldPwd.Length));
                    else
                        switch(Misc.InputData(ref oldPwd, "Password", MagicNumbers.passwordMin, MagicNumbers.passwordMax, true))
                        {
                            case null: return;
                            case true: break;
                            case false:
                                oldPwd = null;
                                continue;
                        }

                    if(!VerifyPassword(oldPwd!, user.PwdSet))
                    {
                        oldPwd = null;
                        WriteLine(" Error: Wrong password");
                        ReadKey(true);
                        continue;
                    }

                    Write(" Enter new password   : ");
                    if(newPwd != null)
                        WriteLine(new string('*', newPwd.Length));
                    else
                        switch(Misc.InputData(ref newPwd, "Password", MagicNumbers.nicknameMin, MagicNumbers.nicknameMax, true))
                        {
                            case null: return;
                            case true: break;
                            case false:
                                newPwd = null;
                                continue;
                        }

                    if(newPwd!.Equals(oldPwd))
                    {
                        newPwd = null;
                        WriteLine(" Error: New password must be different");
                        ReadKey(true);
                        continue;
                    }

                    Write(" Confirm new password : ");
                    string? confirmPwd = null;
                    switch(Misc.InputData(ref confirmPwd, "Password", MagicNumbers.nicknameMin, MagicNumbers.nicknameMax, true))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }
                        
                    if(!confirmPwd!.Equals(newPwd))
                    {
                        newPwd = null;
                        WriteLine(" Error: Mis-match confirm password");
                        ReadKey(true);
                        continue;
                    }

                    PasswordSet newPwdSet = HashPassword(newPwd);

                    if(CommandHandler.Stream(stream, buffer, new(CommandType.ChangePassword, PasswordSet.Serialize(newPwdSet)), out _))
                    {
                        user.PwdSet = newPwdSet;
                        IOHelper.WriteBorder();
                        WriteLine(" Changed password successfully!");
                        ReadKey(true);
                        return;
                    }
                    else continue;
                }
            }
        }

        public class ShowMenu
        {
            public static void ConnectMenu(string serverIP, int port)
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

            public static void WelcomeMenu()
            {
                Clear();
                IOHelper.WriteHeader("Zelo");
                WriteLine(" 1. Register");
                WriteLine(" 2. Login");
                WriteLine(" 0. Shut down client");
                IOHelper.WriteBorder();
                Write(" Enter Choice: ");
            }
            
            public static void UserMenu(string nickname)
            {
                Clear();
                IOHelper.WriteHeader("Zelo");
                WriteLine($" Zello, {nickname}!");
                IOHelper.WriteBorder();
                WriteLine(" 1. Change nickname");
                WriteLine(" 2. Change password");
                WriteLine(" 3. Private message");
                WriteLine(" 4. Group message");
                WriteLine(" 0. Logout");
                IOHelper.WriteBorder();
                Write(" Enter Choice: ");
            }
        }

        public static class Misc
        {
            public static bool CheckIPv4(string ipAddress)
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

            /// <summary>
            /// Save user's input to 'dataBuffer', automatically print error prompt if input's length is shorter than 'minLength'.
            /// </summary>
            /// <param name="intercept"> Whether to hide input as '*'. </param>
            /// <returns> True if input sastifies the condition; false otherwise; null if cancelled. </returns>
            public static bool? InputData(ref string? dataBuffer, string dataName, int minLength, int? maxLength, bool intercept)
            {
                dataBuffer = IOHelper.ReadInput(maxLength, intercept);

                if(dataBuffer == null)
                    return null;
                
                if(dataBuffer.Length < minLength)
                {
                    WriteLine($" Error: {dataName} must have at least {minLength} characters");
                    ReadKey(true);
                    return false;
                }

                return true;
            }
        }

        public static class CommandHandler
        {
            /// <summary>
            /// Send 'cmdToSend' over provided NetworkStream 'stream', automatically output to console if 'receivedCmd' is error type.
            /// </summary>
            /// <returns> True if 'receivedCmd' is same type as 'cmdToSend'; false otherwise. </returns>
            public static bool Stream(NetworkStream stream, byte[] buffer, Command cmdToSend, out Command receivedCmd)
            {
                Command? nullableReceivedCmd;
                receivedCmd = new();

                lock(stream)
                {
                    stream.Write(EncodeString(Command.Serialize(cmdToSend)));
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    nullableReceivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, bytesRead));
                }

                if(nullableReceivedCmd == null)
                {
                    WriteLine(" Error: Received invalid command");
                    ReadKey(true);
                    return false;
                }

                switch((receivedCmd = nullableReceivedCmd).CommandType)
                {
                    case var value when value == cmdToSend.CommandType:
                        return true;

                    case CommandType.Error:
                        WriteLine($" Error: {receivedCmd.Payload}");
                        ReadKey(true);
                        return false;

                    default:
                        WriteLine(" Error: Received unknown command");
                        ReadKey(true);
                        return false;
                }
            }

            // public static bool Ping(NetworkStream stream, byte[] buffer)
            // {
            //     try
            //     {
            //         stream.Write(EncodeString(Command.Serialize(new(CommandType.Ping, null))));
            //         lock(buffer)
            //         return stream.Read(buffer, 0, buffer.Length) > 0;
            //     }
            //     catch(Exception)
            //     {
            //         return false;
            //     }
            // }

            public static void SendDisconnect(NetworkStream stream)
            {
                stream.Write(EncodeString(Command.Serialize(new(CommandType.Disconnect, null))));
            }
        }
    }
}