using System.Net;
using System.Net.Sockets;

using static System.Console;

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
            // Clear();
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
            ServerHelper.ShowStartUpMenu(serverIP, port);

            switch(IOHelper.ReadInput(false))
            {
                case "1":
                    stopProgram = false;
                    return;

                case "2":
                    Write(" Enter IP: ");
                    serverIP = ReadLine();

                    if(serverIP == null || !ServerHelper.CheckIPv4(serverIP))
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
                    while(ServerHelper.ViewActivityLog());
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
            while(!token.IsCancellationRequested)
            {
                TcpClient client = await server.AcceptTcpClientAsync(token);

                ClientHandler clientHandler = new(client);

                lock(clientHanlders)
                    clientHanlders.Add(clientHandler);

                _ = Task.Run(() => clientHandler.HandlingClientAsync(token), token);
            }
        }
        catch(OperationCanceledException) {}
        catch(Exception ex)
        {
            LogManager.AddLog($"Error while accepting client: {ex.Message}");
        }
    }

    static void ServerControl(string? serverIP, int port)
    {
        while(true)
        {
            ServerHelper.ShowControlMenu(serverIP, port);

            switch(IOHelper.ReadInput(false))
            {
                case "4":
                    while(ServerHelper.ViewActivityLog());
                    continue;

                case "0": case null:
                    WriteLine(" Shutting down server...");
                    ReadKey(true);
                    return;

                default: continue;
            }
        }
    }

    public static void JoinPublicGroup(ChatGroup groupToJoin, ClientHandler client)
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

    public static void JoinPrivateGroup(ClientHandler client, int mainUserID, int partnerID)
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

    private class ServerHelper
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

        public static void ShowStartUpMenu(string? serverIP, int port)
        {
            Clear();
            IOHelper.WriteHeader("Zelo Server Control Center");
            WriteLine($" Server's IP: {serverIP ?? "Any"}");
            WriteLine($" Server's port: {port}");
            IOHelper.WriteBorder();
            WriteLine(" 1. Start server");
            WriteLine(" 2. Change IP");
            WriteLine(" 3. Change port");
            WriteLine(" 4. View activity log");
            WriteLine(" 0. Shut down program");
            IOHelper.WriteBorder();
            Write(" Enter choice: ");
        }

        public static void ShowControlMenu(string? serverIP, int port)
        {
            Clear();
            IOHelper.WriteHeader("Zelo Server Control Center");
            WriteLine(" Server is online");
            WriteLine($" Address: {serverIP ?? "Any"}");
            WriteLine($" Port: {port}");
            IOHelper.WriteBorder();
            WriteLine(" 1. Manage connected clients");
            WriteLine(" 2. Manage chat groups");
            WriteLine(" 3. Broadcast message");
            WriteLine(" 4. View activity log");
            WriteLine(" 0. Shut down server");
            IOHelper.WriteBorder();
            Write(" Enter choice: ");
        }

        // Return false when leaving viewer
        public static bool ViewActivityLog()
        {
            Clear();
            IOHelper.WriteHeader("Zelo Server Control Center");
            WriteLine(" Viewing activity log");
            WriteLine(" 'DEL' to clear log");
            WriteLine(" 'ESC' to return");
            IOHelper.WriteBorder();
            
            LogManager.WriteCurrentLog();

            CancellationTokenSource leaveLogViewerToken = new();
            _ = Task.Run(() => LogManager.WriteNewLogAsync(leaveLogViewerToken.Token));

            while(true)
            {
                ConsoleKey key = ReadKey(true).Key;

                switch(key)
                {
                    case ConsoleKey.Escape:
                        leaveLogViewerToken.Cancel();
                        return false;

                    case ConsoleKey.Delete:
                        LogManager.ClearLog();
                        leaveLogViewerToken.Cancel();
                        return true;
                }
            }
        }

    }
}