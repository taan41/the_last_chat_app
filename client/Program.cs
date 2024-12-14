using System.Diagnostics.Contracts;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using static System.Console;
using static Utilities;

class Client
{
    const string defaultIP =
        "127.0.0.1";        // localhost IP
        // "192.168.0.105"; // wifi (?) IP
        // "26.244.97.115"; // tan Radmin IP
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
                if (stopProgram)
                    return;

                using TcpClient client = new(serverIP, port);
                using NetworkStream stream = client.GetStream();
                stream.ReadTimeout = MagicNum.streamTimeOut;
                stream.WriteTimeout = MagicNum.streamTimeOut;

                WriteLine(" Connected to server successfully!");
                ReadKey(true);

                while(true)
                {
                    WelcomeMenu(stream, out User? loggedInUser, out stopProgram);

                    if (stopProgram)
                        return;
                    if(loggedInUser == null)
                        throw new ArgumentNullException("Null user");

                    UserMenu(stream, loggedInUser);
                }
            }
            catch(IOException)
            {
                WriteLine(" Error: Server is offline");
                ReadKey(true);
            }
            catch(Exception ex)
            {
                // WriteLine($" Error: ({ex.GetType().Name}) {ex.Message}");
                WriteLine(ex);
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

                    if (input != null && Helper.Misc.CheckIPv4(input))
                        serverIP = input; 
                    else
                    {
                        serverIP = defaultIP;
                        WriteLine(" Error: Invalid IP");
                        ReadKey(true);
                    }
                    continue;

                case "3":
                    Write(" Enter port: ");

                    try
                    {
                        port = Convert.ToInt32(ReadLine());
                        if (port < 0 || port > 65535)
                            throw new FormatException();
                    }
                    catch(FormatException)
                    {
                        port = defaultPort;
                        WriteLine(" Error: Invalid port");
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
                    if (loggedInUser != null)
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
        byte[] buffer = new byte[MagicNum.bufferSize];
        Command cmdToSend = new();

        while (true)
        {
            Helper.ShowMenu.UserMenu(loggedInUser.Nickname);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    Helper.ClientAction.ChangeNickname(stream, ref loggedInUser);
                    continue;

                case "2":
                    Helper.ClientAction.ChangePassword(stream, ref loggedInUser);
                    continue;

                case "3":
                    // PrivateMsgMenu(stream, ref loggedInUser);
                    continue;

                case "4":
                    GroupMsgMenu(stream, ref loggedInUser);
                    continue;

                case "0": case null:
                    WriteLine(" Logging out...");
                    cmdToSend.Set(CommandType.Logout, null);
                    Helper.CommandHandler.Stream(stream, ref buffer, cmdToSend, out _);
                    return;

                default:
                    continue;
            }
        }
    }

    static void GroupMsgMenu(NetworkStream stream, ref User loggedInUser)
    {
        while (true)
        {
            Helper.ShowMenu.GroupMsgMenu();

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    JoinGroupMenu(stream, loggedInUser);
                    continue;

                case "2":
                    ManageGroupMenu(stream, loggedInUser);
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
        }
    }

    static void JoinGroupMenu(NetworkStream stream, User loggedInUser)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        List<ChatGroup>? groups;
        Command cmdToSend = new();

        int curPage = 0, maxPage;
        while (true)
        {
            // Get list of all available groups
            cmdToSend.Set(CommandType.RequestGroupList, null);

            if (Helper.CommandHandler.Stream(stream, ref buffer, cmdToSend, out Command receivedCmd))
            {
                groups = JsonSerializer.Deserialize<List<ChatGroup>>(receivedCmd.Payload);

                if (groups == null)
                {
                    WriteLine(" Error: Received null list");
                    ReadKey(true);
                    return;
                }

                maxPage = groups.Count / 10;
            }
            else return;

            Helper.ShowMenu.JoinGroupMenu(groups, curPage);
            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    Helper.ClientAction.GetGroupInfo(stream, groups, curPage, out ChatGroup? joinedGroup);

                    if(joinedGroup != null)
                    {
                        Helper.ClientAction.StartChatting(stream, loggedInUser, null, joinedGroup);
                    }

                    cmdToSend.Set(CommandType.LeaveGroup, null);
                    Helper.CommandHandler.Stream(stream, ref buffer, cmdToSend, out _);
                    continue;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < maxPage)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
        }
    }

    static void ManageGroupMenu(NetworkStream stream, User loggedInUser)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        List<ChatGroup>? groups;

        // Get list of groups created by currently logged-in user
        Command cmdToSend = new(CommandType.RequestCreatedGroups, loggedInUser.UID.ToString());

        if (Helper.CommandHandler.Stream(stream, ref buffer, cmdToSend, out Command receivedCmd))
        {
            groups = JsonSerializer.Deserialize<List<ChatGroup>>(receivedCmd.Payload);

            if (groups == null)
            {
                WriteLine(" Error: Received null list");
                ReadKey(true);
                return;
            }
        }
        else return;

        int curPage = 0, maxPage = groups.Count / 10;
        while (true)
        {
            Helper.ShowMenu.ManageGroupMenu(groups, curPage);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    Helper.ClientAction.CreateChatGroup(stream, (int) loggedInUser.UID!, groups, curPage);
                    break;

                case "2":
                    Helper.ClientAction.DeleteChatGroup(stream, groups, curPage);
                    break;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < maxPage)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }

            // Refresh list after creating/deleting
            cmdToSend.Set(CommandType.RequestCreatedGroups, loggedInUser.UID.ToString());

            if (Helper.CommandHandler.Stream(stream, ref buffer, cmdToSend, out receivedCmd))
            {
                groups = JsonSerializer.Deserialize<List<ChatGroup>>(receivedCmd.Payload);

                if (groups == null)
                {
                    WriteLine(" Error: Received null list");
                    ReadKey(true);
                    return;
                }

                maxPage = groups.Count / 10;
            }
            else return;
        }
    }

    static void Chatting(User user, User? partner, ChatGroup? group)
    {

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
            if (string.IsNullOrWhiteSpace(roomName) || roomName.Length > 30)
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
            
            if (roomName == null) return null;
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
                byte[] buffer = new byte[MagicNum.bufferSize];
                StringBuilder username = new(), pwd = new(), confirmPwd = new(), nickname = new();
                Command cmdToSend = new();

                while(true)
                {
                    ShowMenu.WelcomeMenu();
                    WriteLine("1");
                    IOHelper.WriteBorder();
                    WriteLine(" < Press ESC to cancel >");

                    Write(" Enter username   : ");
                    if (username.Length > 0)
                        WriteLine(username);
                    else
                        switch(Misc.InputData(ref username, "Username", MagicNum.usernameMin, MagicNum.usernameMax, false))
                        {
                            case null: return;
                            case true: break;
                            case false: continue;
                        }

                    // Check availability of username
                    cmdToSend.Set(CommandType.CheckUsername, username.ToString());
                    if (!CommandHandler.Stream(stream, ref buffer, cmdToSend, out _))
                    {
                        username.Clear();
                        continue;
                    }

                    Write(" Enter password   : ");
                    if (pwd.Length > 0)
                        WriteLine(new string('*', pwd.Length));
                    else
                        switch(Misc.InputData(ref pwd, "Password", MagicNum.passwordMin, MagicNum.passwordMax, true))
                        {
                            case null: return;
                            case true: break;
                            case false: continue;
                        }

                    Write(" Confirm password : ");
                    if (confirmPwd.Length > 0)
                        WriteLine(new string('*', confirmPwd.Length));
                    else
                    switch(Misc.InputData(ref confirmPwd, "Password", MagicNum.passwordMin, MagicNum.passwordMax, true))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }
                    
                    if (!confirmPwd.ToString().Equals(pwd.ToString()))
                    {
                        pwd.Clear();
                        confirmPwd.Clear();
                        WriteLine(" Error: Mis-match confirm password");
                        ReadKey(true);
                        continue;
                    }

                    Write(" Enter nickname   : ");
                    switch(Misc.InputData(ref nickname, "Nickname", MagicNum.nicknameMin, MagicNum.nicknameMax, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    User registeredUser = new()
                        {
                            Username = username.ToString(),
                            Nickname = nickname.ToString(),
                            PwdSet = HashPassword(pwd.ToString())
                        };

                    cmdToSend.Set(CommandType.Register, User.Serialize(registeredUser));
                    if (CommandHandler.Stream(stream, ref buffer, cmdToSend, out _))
                    {
                        IOHelper.WriteBorder();
                        WriteLine(" Registered successfully!");
                        ReadKey(true);
                    }

                    return;
                }
            }

            public static void Login(NetworkStream stream, out User? loggedInUser)
            {
                byte[] buffer = new byte[MagicNum.bufferSize];
                Command cmdToSend = new();
                PasswordSet? pwdSet;
                loggedInUser = null;
                
                while(true)
                {
                    ShowMenu.WelcomeMenu();
                    WriteLine("2");
                    IOHelper.WriteBorder();
                    WriteLine(" < Press ESC to cancel >");

                    Write(" Enter username: ");
                    string username = "";
                    switch(Misc.InputData(ref username, "Username", MagicNum.nicknameMin, MagicNum.nicknameMax, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    cmdToSend.Set(CommandType.RequestUserPwd, username);

                    // Check if username exists and get password hash/salt of that username
                    if (CommandHandler.Stream(stream, ref buffer, cmdToSend, out Command receivedCmd))
                    {
                        pwdSet = PasswordSet.Deserialize(receivedCmd.Payload);
                        if (pwdSet == null)
                        {
                            WriteLine(" Error: Received invalid password");
                            ReadKey(true);
                            continue;
                        }
                    }
                    else continue;

                    Write(" Enter password: ");
                    string pwd = "";
                    switch(Misc.InputData(ref pwd, "Password", MagicNum.passwordMin, MagicNum.passwordMax, true))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    if (VerifyPassword(pwd, pwdSet))
                    {
                        cmdToSend = new(CommandType.Login, username);

                        if (CommandHandler.Stream(stream, ref buffer, cmdToSend, out receivedCmd))
                        {
                            loggedInUser = User.Deserialize(receivedCmd.Payload);

                            if (loggedInUser == null || loggedInUser.UID == null)
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
                byte[] buffer = new byte[MagicNum.bufferSize];

                while(true)
                {
                    ShowMenu.UserMenu(user.Nickname);
                    WriteLine("1");
                    IOHelper.WriteBorder();
                    WriteLine(" < Press ESC to cancel >");

                    Write(" Enter new nickname: ");
                    string newNickname = "";
                    switch(Misc.InputData(ref newNickname, "Nickname", MagicNum.nicknameMin, MagicNum.nicknameMax, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    if (CommandHandler.Stream(stream, ref buffer, new(CommandType.ChangeNickname, newNickname), out _))
                    {
                        user.Nickname = newNickname;
                        IOHelper.WriteBorder();
                        WriteLine(" Changed nickname successfully!");
                        ReadKey(true);
                        return;
                    }
                    else continue;
                }
            }

            public static void ChangePassword(NetworkStream stream, ref User user)
            {
                if (user.PwdSet == null)
                {
                    WriteLine(" Error: Null PasswordSet");
                    return;
                }

                byte[] buffer = new byte[MagicNum.bufferSize];
                StringBuilder oldPwd = new(), newPwd = new(), confirmPwd = new();

                while(true)
                {
                    ShowMenu.UserMenu(user.Nickname);
                    WriteLine("2");
                    IOHelper.WriteBorder();
                    WriteLine(" < Press ESC to cancel >");

                    Write(" Enter old password   : ");
                    if (oldPwd.Length > 0)
                        WriteLine(new string('*', oldPwd.Length));
                    else
                        switch(Misc.InputData(ref oldPwd, "Password", MagicNum.passwordMin, MagicNum.passwordMax, true))
                        {
                            case null: return;
                            case true: break;
                            case false: continue;
                        }

                    if (!VerifyPassword(oldPwd.ToString(), user.PwdSet))
                    {
                        oldPwd.Clear();
                        WriteLine(" Error: Wrong password");
                        ReadKey(true);
                        continue;
                    }

                    Write(" Enter new password   : ");
                    if (newPwd.Length > 0)
                        WriteLine(new string('*', newPwd.Length));
                    else
                        switch(Misc.InputData(ref newPwd, "Password", MagicNum.passwordMin, MagicNum.passwordMax, true))
                        {
                            case null: return;
                            case true: break;
                            case false: continue;
                        }

                    if (newPwd.ToString().Equals(oldPwd.ToString()))
                    {
                        newPwd.Clear();
                        WriteLine(" Error: New password must be different");
                        ReadKey(true);
                        continue;
                    }

                    Write(" Confirm new password : ");
                    switch(Misc.InputData(ref confirmPwd, "Password", MagicNum.passwordMin, MagicNum.passwordMax, true))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }
                        
                    if (!confirmPwd.ToString().Equals(newPwd.ToString()))
                    {
                        newPwd.Clear();
                        WriteLine(" Error: Mis-match confirm password");
                        ReadKey(true);
                        continue;
                    }

                    PasswordSet newPwdSet = HashPassword(newPwd.ToString());

                    if (CommandHandler.Stream(stream, ref buffer, new(CommandType.ChangePassword, PasswordSet.Serialize(newPwdSet)), out _))
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

            public static void CreateChatGroup(NetworkStream stream, int creatorID, List<ChatGroup> groups, int curPage)
            {
                byte[] buffer = new byte[MagicNum.bufferSize];

                while(true)
                {
                    ShowMenu.ManageGroupMenu(groups, curPage);
                    WriteLine("1");
                    IOHelper.WriteBorder();
                    WriteLine(" < Press ESC to cancel >");

                    Write(" Enter chat group name: ");
                    string groupName = "";
                    switch(Misc.InputData(ref groupName, "Chat group name", MagicNum.groupnameMin, MagicNum.groupNameMax, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    ChatGroup newGroup = new()
                    {
                        CreatorID = creatorID,
                        GroupName = groupName
                    };

                    if (CommandHandler.Stream(stream, ref buffer, new(CommandType.CreateGroup, ChatGroup.Serialize(newGroup)), out _))
                    {
                        IOHelper.WriteBorder();
                        WriteLine(" Created chat group successfully!");
                        ReadKey(true);
                    }
                    
                    return;
                }
            }

            public static void DeleteChatGroup(NetworkStream stream, List<ChatGroup> groups, int curPage)
            {
                byte[] buffer = new byte[MagicNum.bufferSize];

                while(true)
                {
                    ShowMenu.ManageGroupMenu(groups, curPage);
                    WriteLine("2");
                    IOHelper.WriteBorder();
                    WriteLine(" < Press ESC to cancel >");

                    Write(" Enter chat group ID: ");
                    string groupIDString = "";
                    switch(Misc.InputData(ref groupIDString, "Chat group ID", 1, 5, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    int groupID;

                    try
                    {
                        groupID = Convert.ToInt32(groupIDString);
                        
                        if(groupID < 1)
                            throw new FormatException();
                    }
                    catch (FormatException)
                    {
                        WriteLine(" Invalid ID");
                        continue;
                    }

                    if (CommandHandler.Stream(stream, ref buffer, new(CommandType.DeleteGroup, groupID.ToString()), out _))
                    {
                        IOHelper.WriteBorder();
                        WriteLine(" Deleted chat group successfully!");
                        ReadKey(true);
                    }
                    
                    return;
                }
            }

            public static void GetGroupInfo(NetworkStream stream, List<ChatGroup> groups, int curPage, out ChatGroup? group)
            {
                byte[] buffer = new byte[MagicNum.bufferSize];
                group = null;

                while(true)
                {
                    ShowMenu.JoinGroupMenu(groups, curPage);
                    WriteLine("1");
                    IOHelper.WriteBorder();
                    WriteLine(" < Press ESC to cancel >");

                    Write(" Enter chat group ID: ");
                    string groupIDString = "";
                    switch(Misc.InputData(ref groupIDString, "Chat group ID", 1, 5, false))
                    {
                        case null: return;
                        case true: break;
                        case false: continue;
                    }

                    int groupID;

                    try
                    {
                        groupID = Convert.ToInt32(groupIDString);

                        if(groupID < 1)
                            throw new FormatException();
                    }
                    catch (FormatException)
                    {
                        WriteLine(" Invalid ID");
                        continue;
                    }

                    if (CommandHandler.Stream(stream, ref buffer, new(CommandType.JoinGroup, groupID.ToString()), out Command receivedCmd))
                    {
                        group = ChatGroup.Deserialize(receivedCmd.Payload);

                        if(group == null)
                        {
                            WriteLine(" Error: Received invalid chat group data");
                            ReadKey(true);
                            return;
                        }
                    }
                    
                    return;
                }
            }

            private static readonly StringBuilder inputBuffer = new();
            private static readonly List<Message> msgHistory = [];

            public static void StartChatting(NetworkStream stream, User mainUser, User? partner, ChatGroup? joinedGroup)
            {
                if(mainUser.UID == null || (joinedGroup == null && partner == null))
                {
                    WriteLine(" Error: Null chatting data");
                    return;
                }
                Clear();
                IOHelper.WriteHeader("Zelo");
                WriteLine(" All chat commands:");
                ShowMenu.ChatCommands();
                IOHelper.WriteBorder();
                
                Command cmdToSend = new();

                WriteLine(" Press any key to continue...");
                ReadKey(true);
                Clear();

                string prompt = $"<{mainUser.Nickname}> ";
                string? content;
                Message message = new((int) mainUser.UID, partner?.UID, joinedGroup?.GroupID, mainUser.Nickname, "");

                CancellationTokenSource stopTokenSource = new();

                _ = Task.Run(() => EchoMessage(stream, prompt, stopTokenSource.Token));

                while(true)
                {
                    Write(prompt);

                    inputBuffer.Clear();
                    content = IOHelper.ReadInput(inputBuffer, null, false);

                    if(content == null )
                        return;
                    if(string.IsNullOrWhiteSpace(content))
                        continue;

                    if(content.Trim().ElementAt(0) == '/')
                    {
                        switch(content.Trim())
                        {
                            case "/leave":
                                return;
                            
                            default:
                                WriteLine("[System] Unknown chat command");
                                continue;
                        }
                    }

                    message.Content = content;
                    cmdToSend.Set(CommandType.Message, Message.Serialize(message));
                    stream.Write(EncodeString(Command.Serialize(cmdToSend)));
                }
            }

            public static void WriteMessage(string content, string prompt)
            {
                IOHelper.MoveCursorLeft(prompt.Length + inputBuffer.Length);
                WriteLine(content.ToString());
                Write(prompt);
                Write(inputBuffer);
            }

            public static async Task EchoMessage(NetworkStream stream, string prompt, CancellationToken stopToken)
            {
                byte[] buffer = new byte[MagicNum.bufferSize];
                int bytesRead, totalRead = 0;
                Command? receivedCmd;
                Message? echoMsg;

                try
                {
                    while(!stopToken.IsCancellationRequested)
                    {
                        while(true)
                        {
                            bytesRead = await stream.ReadAsync(buffer, totalRead, 1024, stopToken);

                            totalRead += bytesRead;
                            
                            if(bytesRead < 1024)
                                break;

                            if(totalRead + 1024 >= buffer.Length)
                                Array.Resize(ref buffer, buffer.Length * 2);
                        }

                        receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, totalRead));
                        totalRead = 0;

                        switch(receivedCmd?.CommandType)
                        {
                            case CommandType.MessageEcho:
                                echoMsg = Message.Deserialize(receivedCmd.Payload);

                                if (echoMsg == null)
                                    WriteMessage("Error: Null echo message", prompt);
                                else
                                    WriteMessage(echoMsg.ToString(), prompt);

                                continue;

                            case CommandType.Error:
                                WriteMessage($" Error: {receivedCmd.Payload}", prompt);
                                continue;

                            default:
                                WriteMessage(" Error: Received invalid command", prompt);
                                continue;
                        }
                    }
                }
                catch(OperationCanceledException) { /* ignored */ }
                catch(Exception ex)
                {
                    WriteLine($" Error while echoing msg: ({ex.GetType().Name}) {ex.Message}");
                    ReadKey(true);
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

            public static void PrivateMsgMenu(List<User> users, int curPage)
            {
                Clear();
                IOHelper.WriteHeader("Zelo");
                WriteLine($" List of registered users (Page {curPage + 1}/{users.Count / 10 + 1}):");

                foreach(User user in users.GetRange(curPage * 10, Math.Min(users.Count - curPage * 10, 10)))
                {
                    WriteLine($" • {user.ToString(false)}");
                }

                IOHelper.WriteBorder();
                WriteLine(" 1. Enter ID of partner");
                WriteLine(" 8. Previous page");
                WriteLine(" 9. Next page");
                WriteLine(" 0. Return");
                IOHelper.WriteBorder();
                Write(" Enter Choice: ");
            }

            public static void GroupMsgMenu()
            {
                Clear();
                IOHelper.WriteHeader("Zelo");
                WriteLine(" 1. View chat group list & Join one");
                WriteLine(" 2. Create & Manage created chat group(s)");
                WriteLine(" 0. Return");
                IOHelper.WriteBorder();
                Write(" Enter Choice: ");
            }

            public static void JoinGroupMenu(List<ChatGroup> groups, int curPage)
            {
                Clear();
                IOHelper.WriteHeader("Zelo");
                WriteLine($" List of chat groups (Page {curPage + 1}/{groups.Count / 10 + 1}):");

                foreach(ChatGroup group in groups.GetRange(curPage * 10, Math.Min(groups.Count - curPage * 10, 10)))
                {
                    WriteLine($" • {group.ToString(true)}");
                }

                IOHelper.WriteBorder();
                WriteLine(" 1. Join chat group using ID");
                WriteLine(" 8. Previous page");
                WriteLine(" 9. Next page");
                WriteLine(" 0. Return");
                IOHelper.WriteBorder();
                Write(" Enter Choice: ");
            }

            public static void ManageGroupMenu(List<ChatGroup> groups, int curPage)
            {
                Clear();
                IOHelper.WriteHeader("Zelo");
                WriteLine($" List of created chat groups (Page {curPage + 1}/{groups.Count / 10 + 1}):");

                foreach(ChatGroup group in groups.GetRange(curPage * 10, Math.Min(groups.Count - curPage * 10, 10)))
                {
                    WriteLine($" • {group.ToString(false)}");
                }

                IOHelper.WriteBorder();
                WriteLine(" 1. Create new chat group");
                WriteLine(" 2. Delete chat group using ID");
                WriteLine(" 8. Previous page");
                WriteLine(" 9. Next page");
                WriteLine(" 0. Return");
                IOHelper.WriteBorder();
                Write(" Enter Choice: ");
            }
        
            public static void ChatCommands()
            {
                WriteLine(" /help         -- Show all chat commands");
                WriteLine(" /info         -- Show info of chat group/partner");
                WriteLine(" /clear /cls   -- Clear console");
                WriteLine(" /reload       -- Clear console then re-write all messages");
                WriteLine(" /leave        -- Leave chat room");
                WriteLine(" You can also leave using 'ESC' key");
            }
        }

        public static class Misc
        {
            public static bool CheckIPv4(string ipAddress)
            {
                if (!IPAddress.TryParse(ipAddress, out _))
                    return false;

                string[] parts = ipAddress.Split('.');
                if (parts.Length != 4) return false;

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
            public static bool? InputData(ref string data, string dataName, int minLength, int? maxLength, bool intercept)
            {
                string? inputBuffer = IOHelper.ReadInput(maxLength, intercept);

                if (inputBuffer == null)
                    return null;
                
                if (inputBuffer.Length < minLength)
                {
                    WriteLine($" Error: {dataName} must have at least {minLength} characters");
                    ReadKey(true);
                    return false;
                }

                data = inputBuffer;
                return true;
            }

            
            public static bool? InputData(ref StringBuilder dataBuilder, string dataName, int minLength, int? maxLength, bool intercept)
            {
                dataBuilder.Clear();
                string? inputBuffer = IOHelper.ReadInput(maxLength, intercept);

                if (inputBuffer == null)
                    return null;
                
                if (inputBuffer.Length < minLength)
                {
                    WriteLine($" Error: {dataName} must have at least {minLength} characters");
                    ReadKey(true);
                    return false;
                }

                dataBuilder.Append(inputBuffer);
                return true;
            }
        }

        public static class CommandHandler
        {
            /// <summary>
            /// Send 'cmdToSend' over provided NetworkStream 'stream'. Automatically output to console if 'receivedCmd' is error type.
            /// </summary>
            /// <returns> True if 'receivedCmd' is same type as 'cmdToSend'; false otherwise. </returns>
            public static bool Stream(NetworkStream stream, ref byte[] buffer, Command cmdToSend, out Command receivedCmd)
            {
                Command? tempCmd;
                receivedCmd = new();

                int bytesRead, totalRead = 0;
                lock(stream)
                {
                    stream.Write(EncodeString(Command.Serialize(cmdToSend)));

                    while(true)
                    {
                        bytesRead = stream.Read(buffer, totalRead, 1024);

                        totalRead += bytesRead;
                        
                        if(bytesRead < 1024)
                            break;

                        if(totalRead + 1024 >= buffer.Length)
                            Array.Resize(ref buffer, buffer.Length * 2);
                    }
                }
                
                tempCmd = Command.Deserialize(DecodeBytes(buffer, 0, totalRead));
                switch(tempCmd?.CommandType)
                {
                    case var value when value == cmdToSend.CommandType:
                        receivedCmd = tempCmd!;
                        return true;

                    case CommandType.Error:
                        WriteLine($" Error: {receivedCmd.Payload}");
                        ReadKey(true);
                        return false;

                    default:
                        WriteLine(" Error: Received invalid command");
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