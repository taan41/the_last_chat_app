using System.Net.Sockets;
using System.Text.Json;

using static System.Console;
using static ClientHelper;

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
                WriteLine($" Error: ({ex.GetType().Name}) {ex.Message}");
                // WriteLine(ex);
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
            ShowMenu.ConnectMenu(serverIP, port);

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    return;

                case "2":
                    Write(" Enter IP: ");
                    input = IOHelper.ReadInput(false);

                    if (input != null && Misc.CheckIPv4(input))
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
            ShowMenu.WelcomeMenu();

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    ClientHelper.Action.Login(stream, out loggedInUser);
                    if (loggedInUser != null)
                        return;
                    continue;

                case "2":
                    ClientHelper.Action.Register(stream);
                    continue;

                case "0": case null:
                    WriteLine(" Shutting down client...");
                    CommandHandler.SendDisconnect(stream);
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
            ShowMenu.UserMenu(loggedInUser.Nickname);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientHelper.Action.ChangeNickname(stream, ref loggedInUser);
                    continue;

                case "2":
                    ClientHelper.Action.ChangePassword(stream, ref loggedInUser);
                    continue;

                case "3":
                    PrivateMsgMenu(stream, ref loggedInUser);
                    continue;

                case "4":
                    GroupMsgMenu(stream, ref loggedInUser);
                    continue;

                case "0": case null:
                    WriteLine(" Logging out...");
                    cmdToSend.Set(CommandType.Logout, null);
                    CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out _);
                    return;

                default:
                    continue;
            }
        }
    }

    static void PrivateMsgMenu(NetworkStream stream, ref User loggedInUser)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        List<User>? users;
        Command cmdToSend = new();

        int curPage = 0, maxPage;
        while (true)
        {
            // Get list of all users
            cmdToSend.Set(CommandType.GetUserList, null);

            if (CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out Command receivedCmd))
            {
                users = JsonSerializer.Deserialize<List<User>>(receivedCmd.Payload);

                if (users == null)
                {
                    WriteLine(" Error: Received null list");
                    ReadKey(true);
                    return;
                }

                maxPage = (users.Count - 1) / 10;
            }
            else return;

            ShowMenu.PrivateMsgMenu(users, curPage);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientHelper.Action.SetPartner(stream, users, curPage, loggedInUser.UID, out User? partner);

                    if(partner != null)
                    {
                        ClientHelper.Action.StartChatting(stream, loggedInUser, partner, null);
                    }

                    cmdToSend.Set(CommandType.RemovePartner, null);
                    CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out _);
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

    static void GroupMsgMenu(NetworkStream stream, ref User loggedInUser)
    {
        while (true)
        {
            ShowMenu.GroupMsgMenu();

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
            cmdToSend.Set(CommandType.GetGroupList, null);

            if (CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out Command receivedCmd))
            {
                groups = JsonSerializer.Deserialize<List<ChatGroup>>(receivedCmd.Payload);

                if (groups == null)
                {
                    WriteLine(" Error: Received null list");
                    ReadKey(true);
                    return;
                }

                maxPage = (groups.Count - 1) / 10;
            }
            else return;

            ShowMenu.JoinGroupMenu(groups, curPage);
            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientHelper.Action.JoinGroupMenu(stream, groups, curPage, out ChatGroup? joinedGroup);

                    if(joinedGroup != null)
                    {
                        ClientHelper.Action.StartChatting(stream, loggedInUser, null, joinedGroup);
                    }

                    cmdToSend.Set(CommandType.LeaveGroup, null);
                    CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out _);
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
        Command cmdToSend = new(CommandType.GetCreatedGroups, loggedInUser.UID.ToString());

        if (CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out Command receivedCmd))
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

        int curPage = 0, maxPage = (groups.Count - 1) / 10;
        while (true)
        {
            ShowMenu.ManageGroupMenu(groups, curPage);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    ClientHelper.Action.CreateChatGroup(stream, loggedInUser.UID, groups, curPage);
                    break;

                case "2":
                    ClientHelper.Action.DeleteChatGroup(stream, groups, curPage);
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
            cmdToSend.Set(CommandType.GetCreatedGroups, loggedInUser.UID.ToString());

            if (CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out receivedCmd))
            {
                groups = JsonSerializer.Deserialize<List<ChatGroup>>(receivedCmd.Payload);

                if (groups == null)
                {
                    WriteLine(" Error: Received null list");
                    ReadKey(true);
                    return;
                }

                maxPage = (groups.Count - 1) / 10;
            }
            else return;
        }
    }
}