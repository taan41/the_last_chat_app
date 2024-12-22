using System.Diagnostics;
using System.Net.Sockets;

using static System.Console;

class Client
{
    const string defaultIP =
        // "127.0.0.1";        // localhost IP
        // "192.168.0.105"; // wifi (?) IP
        "26.244.97.115"; // tan Radmin IP
    const int defaultPort = 5000;

    static readonly string defaultFolder = Environment.CurrentDirectory + @"\ZeloFiles\";
    static string curFolder = defaultFolder;

    public static void Main()
    {
        string serverIP = defaultIP;
        int port = defaultPort;

        Directory.CreateDirectory(curFolder);

        while (true)
        {
            try
            {
                ConnectServerMenu(ref serverIP, ref port, out bool stopProgram);
                if (stopProgram)
                    return;

                using TcpClient client = new(serverIP, port);
                using NetworkStream stream = client.GetStream();
                stream.ReadTimeout = MagicNum.streamTimeOut;
                stream.WriteTimeout = MagicNum.streamTimeOut;

                WriteLine(" Kết nối tới server thành công!");
                ReadKey(true);

                while (true)
                {
                    WelcomeMenu(stream, out User? loggedInUser, out stopProgram);

                    if (stopProgram)
                        return;
                    if (loggedInUser == null)
                        throw new ArgumentNullException("Null user");

                    UserMenu(stream, loggedInUser);
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException || ex is SocketException)
                    WriteLine(" Server offline");
                else
                    // WriteLine($" Lỗi: ({ex.GetType().Name}) {ex.Message}");
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
            ClientMenu.Connect(serverIP, port);

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    return;

                case "2":
                    Write(" Nhập IP: ");
                    input = IOHelper.ReadInput(false);

                    if (input != null && ClientHelper.CheckIPv4(input))
                        serverIP = input; 
                    else
                    {
                        serverIP = defaultIP;
                        WriteLine(" Lỗi: IP không hợp lệ");
                        ReadKey(true);
                    }
                    continue;

                case "3":
                    Write(" Nhập cổng: ");

                    try
                    {
                        port = Convert.ToInt32(ReadLine());
                        if (port < 0 || port > 65535)
                            throw new FormatException();
                    }
                    catch(FormatException)
                    {
                        port = defaultPort;
                        WriteLine(" Lỗi: Cổng không hợp lệ");
                        ReadKey(true);
                    }
                    continue;

                case "4":
                    FileMenu();
                    continue;

                case "0": case null:
                    WriteLine(" Tắt chương trình...");
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
        byte[] buffer = new byte[MagicNum.bufferSize];

        while(true)
        {
            ClientMenu.Welcome();

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    ClientAction.Login(stream, ref buffer, out loggedInUser);
                    if (loggedInUser != null)
                        return;
                    continue;

                case "2":
                    ClientAction.Register(stream, ref buffer);
                    continue;

                case "3":
                    FileMenu();
                    continue;

                case "0": case null:
                    WriteLine(" Tắt chương trình...");
                    ClientHelper.SendDisconnect(stream);
                    stopProgram = true;
                    return;

                default: continue;
            }
        }
    }

    static void UserMenu(NetworkStream stream, User mainUser)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        Command cmdToSend = new();

        while (true)
        {
            ClientMenu.MainUser(mainUser.Nickname);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientAction.ChangeNickname(stream, ref buffer, ref mainUser);
                    continue;

                case "2":
                    ClientAction.ChangePassword(stream, ref buffer, ref mainUser);
                    continue;

                case "3":
                    FriendsMenu(stream, mainUser);
                    continue;

                case "4":
                    GroupMenu(stream, mainUser);
                    continue;

                case "5":
                    FileMenu();
                    continue;

                case "0": case null:
                    WriteLine(" Đang đăng xuất...");
                    cmdToSend.Set(CommandType.Logout, null);
                    ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _);
                    return;

                default:
                    continue;
            }
        }
    }

    static void FriendsMenu(NetworkStream stream, User mainUser)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        List<Friend>? friends = ClientAction.GetFriendList(stream, ref buffer);
        List<User>? receivedRqs = ClientAction.GetUsers(stream, ref buffer, CommandType.GetReceivedRq);
        Command cmdToSend = new();

        int curPage = 0;
        while (true)
        {
            if (friends == null) return;
            if (receivedRqs == null) return;

            ClientMenu.Friends(friends, curPage, receivedRqs.Count);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientAction.SetPartner(stream, ref buffer, friends, mainUser.UserID, out User? partner);

                    if(partner != null)
                    {
                        ClientAction.StartChatting(stream, mainUser, partner, null, curFolder);

                        cmdToSend.Set(CommandType.RemovePartner, null);
                        ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _);
                    }
                    break;

                case "2":
                    ReceivedRqMenu(stream, ref receivedRqs);
                    break;

                case "3":
                    ClientAction.RemoveFriend(stream, ref buffer, friends);
                    break;

                case "4":
                    AllUserMenu(stream, mainUser);
                    break;

                case "7":
                    break;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < (friends.Count - 1) / 10)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
            
            friends = ClientAction.GetFriendList(stream, ref buffer);
            receivedRqs = ClientAction.GetUsers(stream, ref buffer, CommandType.GetReceivedRq);
        }
    }

    static void ReceivedRqMenu(NetworkStream stream, ref List<User>? receivedRqs)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        CommandType cmdTypeToSend;

        int curPage = 0;
        string? input;
        while (true)
        {
            if (receivedRqs == null) return;

            ClientMenu.ReceivedRq(receivedRqs, curPage);

            input = IOHelper.ReadInput(false);

            switch (input)
            {
                case "1" or "2" or "3" or "4" or "5" or "6":
                    break;

                case "7":
                    receivedRqs = ClientAction.GetUsers(stream, ref buffer, CommandType.GetReceivedRq);
                    continue;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < (receivedRqs.Count - 1) / 10)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }

            cmdTypeToSend = input switch
            {
                "1" => CommandType.AcceptFriendRq,
                "2" => CommandType.AcceptAllRq,
                "3" => CommandType.DenyFriendRq,
                "4" => CommandType.DenyAllRq,
                "5" => CommandType.BlockUser,
                "6" => CommandType.BlockAll,
                _ => CommandType.Empty
            };

            receivedRqs = input switch
            {
                "2" or "4" or "6" => null,
                _ => receivedRqs
            };

            ClientAction.ProcessRequest(stream, ref buffer, cmdTypeToSend, receivedRqs);
            receivedRqs = ClientAction.GetUsers(stream, ref buffer, CommandType.GetReceivedRq);
        }
    }

    static void AllUserMenu(NetworkStream stream, User mainUser)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        List<User>? users = ClientAction.GetUsers(stream, ref buffer, CommandType.GetAllUsers);

        int curPage = 0;
        string? input;
        while (true)
        {
            if (users == null) return;

            ClientMenu.AllUser(users, curPage);

            input = IOHelper.ReadInput(false);

            switch (input)
            {
                case "1":
                    ClientAction.SendRequest(stream, ref buffer, mainUser.UserID);
                    continue;

                case "2":
                    ClientAction.BlockUser(stream, ref buffer, mainUser.UserID);
                    continue;

                case "7":
                    users = ClientAction.GetUsers(stream, ref buffer, CommandType.GetAllUsers);
                    continue;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < (users.Count - 1) / 10)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
        }
    }

    static void GroupMenu(NetworkStream stream, User mainUser)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        List<ChatGroup>? subcribedGroups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetSubcribed);
        Command cmdToSend = new();

        int curPage = 0;
        while (true)
        {
            if (subcribedGroups == null) return;
            
            ClientMenu.ChatGroups(subcribedGroups, curPage);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientAction.JoinGroup(stream, ref buffer, subcribedGroups, out ChatGroup? joinedGroup);

                    if(joinedGroup != null)
                    {
                        ClientAction.StartChatting(stream, mainUser, null, joinedGroup, curFolder);
                        
                        cmdToSend.Set(CommandType.LeaveGroup, null);
                        ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _);
                    }
                    subcribedGroups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetSubcribed);
                    continue;

                case "2":
                    ClientAction.SubUnsubToGroup(stream, ref buffer, subcribedGroups, false);
                    subcribedGroups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetSubcribed);
                    continue;

                case "3":
                    AllGroupMenu(stream, subcribedGroups);
                    subcribedGroups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetSubcribed);
                    continue;

                case "4":
                    ManageCreatedMenu(stream, mainUser);
                    subcribedGroups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetSubcribed);
                    continue;

                case "7":
                    subcribedGroups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetSubcribed);
                    continue;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < (subcribedGroups.Count - 1) / 10)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
        }
    }

    static void AllGroupMenu(NetworkStream stream, List<ChatGroup> subcribedGroups)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        List<ChatGroup>? groups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetAllGroups);

        int curPage = 0;
        while (true)
        {
            if (groups == null) return;

            ClientMenu.AllGroups(groups, curPage);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientAction.SubUnsubToGroup(stream, ref buffer, subcribedGroups, true);
                    continue;

                case "7":
                    groups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetAllGroups);
                    continue;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < (groups.Count - 1) / 10)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
        }
    }

    static void ManageCreatedMenu(NetworkStream stream, User mainUser)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        List<ChatGroup>? groups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetCreatedGroups);

        int curPage = 0;
        while (true)
        {
            if (groups == null) return;

            ClientMenu.ManageCreated(groups, curPage);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientAction.CreateGroup(stream, ref buffer, mainUser.UserID);
                    groups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetCreatedGroups);
                    continue;

                case "2":
                    ClientAction.ChangeGroupName(stream, ref buffer, groups);
                    groups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetCreatedGroups);
                    continue;

                case "3":
                    ClientAction.DeleteGroup(stream, ref buffer, groups);
                    groups = ClientAction.GetGroups(stream, ref buffer, CommandType.GetCreatedGroups);
                    continue;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < (groups.Count - 1) / 10)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
        }
    }

    static void FileMenu()
    {
        List<string> files;

        int curPage = 0;
        while (true)
        {
            files = [.. Directory.GetFiles(curFolder)];

            ClientMenu.File(files, curPage);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientAction.OpenDelCopyFile(curFolder, files, 1);
                    continue;

                case "2":
                    ClientAction.OpenDelCopyFile(curFolder, files, 2);
                    continue;

                case "3":
                    ClientAction.ChangeFileName(curFolder, files);
                    continue;

                case "4":
                    ClientAction.OpenDelCopyFile(curFolder, files, 3);
                    continue;

                case "5":
                    FileFolderMenu();
                    continue;

                case "8":
                    if (curPage > 0)
                        curPage--;
                    continue;

                case "9":
                    if (curPage < (files.Count - 1) / 10)
                        curPage++;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
        }
    }
    
    static void FileFolderMenu()
    {
        while (true)
        {
            ClientMenu.FileFolder(curFolder);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientAction.ChangeFolder(ref curFolder);
                    continue;

                case "2":
                    curFolder = defaultFolder;
                    continue;

                case "0": case null:
                    return;

                default:
                    continue;
            }
        }
    }
}