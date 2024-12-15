using System.Net;
using System.Net.Sockets;

using static System.Console;
using static ServerHelper;

class Server
{
    const int defaultPort = 5000;

    static readonly List<ClientHandler> clientHanlders = [];
    static readonly List<ChatGroupHandler> groupHandlers = [];
    static TcpListener? server;

    public static void Main()
    {
        string? serverIP = null;
        int port = defaultPort;

        ThreadPool.SetMinThreads(20, 20);

        try
        {
            if(!CheckMySqlConn())
                return;

            while(true)
            {
                ServerStartUp(ref serverIP, ref port, out bool stopProgram);
                if(stopProgram)
                    return;

                if(serverIP == null)
                    server = new(IPAddress.Any, port);
                else
                    server = new(IPAddress.Parse(serverIP), port);

                server.Start();
                LogManager.AddLog($"Server starts on address: {serverIP ?? "Any"}, port: {port}");

                CancellationTokenSource serverStopToken = new();
                _ = Task.Run(() => AcceptClientsAsync(server, serverStopToken.Token));

                ServerControl(serverIP, port);

                serverStopToken.Cancel();
                server.Stop();
                LogManager.AddLog("Server stops");
            }
        }
        catch(Exception ex)
        {
            WriteLine($" Error while running program: {ex.Message}");
            LogManager.AddLog($"Error while running program: {ex.Message}");
            ReadKey(true);
        }
    }

    static bool CheckMySqlConn()
    {
        string? server, db, uid, password;
        bool done = false;

        while(!done)
        {
            IOHelper.WriteHeader("Zelo Server Control Center");
            WriteLine(" MySql database info:");

            Write(" Server (default 'localhost'): ");
            if((server = IOHelper.ReadInput(false)) == null)
                return false;
            if(string.IsNullOrWhiteSpace(server))
                server = "localhost";

            Write(" Database (default 'chatapp'): ");
            if((db = IOHelper.ReadInput(false)) == null)
                return false;
            if(string.IsNullOrWhiteSpace(db))
                db = "chatapp";

            Write(" UID (default 'root'): ");
            if((uid = IOHelper.ReadInput(false)) == null)
                return false;
            if(string.IsNullOrWhiteSpace(uid))
                uid = "root";

            Write(" Password: ");
            if((password = IOHelper.ReadInput(true)) == null)
                return false;
            if(string.IsNullOrWhiteSpace(password))
                password = "KoCo0401@mysql";

            IOHelper.WriteBorder();

            if(done = DbHelper.InitMySql(server, db, uid, password, out string errorMessage))
                WriteLine(" Connect to MySql database successfully");
            else
                WriteLine($" Error while connecting to MySql DB: {errorMessage}");

            LogManager.Initialize();
            ReadKey(true);
        }
        
        return true;
    }

    static void ServerStartUp(ref string? serverIP, ref int port, out bool stopProgram)
    {
        while(true)
        {
            ShowMenu.StartUpMenu(serverIP, port);

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    stopProgram = false;
                    return;

                case "2":
                    Write(" Enter IP: ");
                    serverIP = ReadLine();

                    if(serverIP == null || !Misc.CheckIPv4(serverIP))
                    {
                        serverIP = null;
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

                case "4":
                    while(ShowMenu.ActivityLog());
                    continue;

                case "0": case null:
                    WriteLine(" Shutting down program...");
                    stopProgram = true;
                    return;

                default: continue;
            }
        }
    }

    static async Task AcceptClientsAsync(TcpListener server, CancellationToken token)
    {
        try
        {
            while(true)
            {
                TcpClient client = await server.AcceptTcpClientAsync(token);

                if(token.IsCancellationRequested)
                    return;

                ClientHandler clientHandler = new(client);

                lock(clientHanlders)
                    clientHanlders.Add(clientHandler);

                _ = Task.Run(() => clientHandler.HandlingClientAsync(token), token);
            }
        }
        catch(OperationCanceledException) {}
        catch(Exception ex)
        {
            LogManager.AddLog($"Error: {ex.Message}");
        }
    }

    static void ServerControl(string? serverIP, int port)
    {
        while(true)
        {
            ShowMenu.ControlMenu(serverIP, port);

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    ViewConnectedClients();
                    continue;

                case "2":
                    ManageChatGroups();
                    continue;

                case "3":
                    break;

                case "4":
                    while(ShowMenu.ActivityLog());
                    continue;

                case "0": case null:
                    WriteLine(" Shutting down server...");
                    ReadKey(true);
                    return;

                default: continue;
            }
        }
    }

    static void ViewConnectedClients()
    {
        int curPage = 0, maxPage;

        while(true)
        {
            maxPage = (clientHanlders.Count - 1) / 10;

            ShowMenu.ConnectedClients(clientHanlders, curPage);

            switch (IOHelper.ReadInput(false))
            {
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

    static async void ManageChatGroups()
    {
        (List<ChatGroup>? groups, _) = await DbHelper.GetAllChatGroup();

        if (groups == null)
            return;
        
        string? input;
        int curPage = 0, maxPage = (groups.Count - 1) / 10, action = 0;


        while(true)
        {
            ShowMenu.ManageGroups(groups, curPage);
            var (Left, Top) = GetCursorPosition();
            WriteLine();
            WriteLine(" Warning: Very buggy due to unknown thread-related (?) error");
            SetCursorPosition(Left, Top);

            switch (IOHelper.ReadInput(false))
            {
                case "1":
                    action = 1;
                    break;
                
                case "2":
                    action = 2;
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

            IOHelper.WriteBorder();
            Write(" Enter group's ID: ");
            input = IOHelper.ReadInput(5, false);

            if (input == null)
            {
                action = 0;
                continue;
            }

            int groupID;
            ChatGroup? groupToChange;

            try
            {
                groupID = Convert.ToInt32(input);
                
                if (groupID < 1)
                    throw new FormatException();

                if ((groupToChange = groups.Find(group => group.GroupID == groupID)) == null)
                    throw new FormatException();
            }
            catch (FormatException)
            {
                WriteLine(" Error: Invalid ID");
                ReadKey(true);
                continue;
            }

            if (action == 1)
            {
                Write(" Enter group's Name: ");
                input = IOHelper.ReadInput(MagicNumbers.groupNameMax, false);

                if (input == null)
                {
                    action = 0;
                    continue;
                }

                groupToChange.GroupName = input;
                await DbHelper.UpdateChatGroup(groupToChange, false);
            }
            else if (action == 2)
            {
                groups.Remove(groupToChange);
                await DbHelper.DeleteChatGroup(groupID);
                groupHandlers.Find(handler => handler.GetGroup != null && handler.GetGroup.GroupID == groupID)?.Dispose();
            }
            
            action = 0;
            continue;
        }
    }

    public static void JoinChatGroup(ChatGroup groupToJoin, ClientHandler client)
    {
        lock(groupHandlers)
        {
            foreach(ChatGroupHandler handler in groupHandlers)
            {
                if(handler.GetGroup != null && handler.GetGroup.GroupID == groupToJoin.GroupID)
                {
                    handler.AddClient(client);
                    return;
                }
            }

            groupHandlers.Add(new(groupToJoin, client, DisposeChatGroup));
        }
    }

    public static void JoinPrivate(ClientHandler client, int mainUserID, int partnerID)
    {
        lock(groupHandlers)
        {
            foreach(ChatGroupHandler handler in groupHandlers)
            {
                if(handler.GetMemIDs != null && handler.GetMemIDs.Contains(mainUserID) && handler.GetMemIDs.Contains(partnerID))
                {
                    handler.AddClient(client);
                    return;
                }
            }

            groupHandlers.Add(new(client, mainUserID, partnerID, DisposeChatGroup));
        }
    }

    public static void DisposeChatGroup(ChatGroupHandler groupHandler)
    {
        lock(groupHandlers)
        {
            groupHandlers.Remove(groupHandler);
        }
    }

    public static void DisposeChatGroup(int groupID)
    {
        lock(groupHandlers)
        {
            groupHandlers.Find(handler => handler.GetGroup != null && handler.GetGroup.GroupID == groupID)?.Dispose();
        }
    }
}