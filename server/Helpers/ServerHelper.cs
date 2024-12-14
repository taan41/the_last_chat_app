using System.Net;

using static System.Console;

static class ServerHelper
{
    public static class ShowMenu
    {
        public static void StartUpMenu(string? serverIP, int port)
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

        public static void ControlMenu(string? serverIP, int port)
        {
            Clear();
            IOHelper.WriteHeader("Zelo Server Control Center");
            WriteLine(" Server is online");
            WriteLine($" Address: {serverIP ?? "Any"}");
            WriteLine($" Port: {port}");
            IOHelper.WriteBorder();
            WriteLine(" 1. View connected clients");
            WriteLine(" 2. Manage chat groups");
            WriteLine(" 3. Add server's notice");
            WriteLine(" 4. View activity log");
            WriteLine(" 0. Shut down server");
            IOHelper.WriteBorder();
            Write(" Enter choice: ");
        }

        public static void ConnectedClients(List<ClientHandler> clients, int curPage)
        {
            Clear();
            IOHelper.WriteHeader("Zelo Server Control Center");
            WriteLine($" List of connected clients (Page {curPage + 1}/{(clients.Count - 1) / 10 + 1}):");
            IOHelper.WriteBorder();

            foreach(var client in clients.GetRange(curPage * 10, Math.Min(clients.Count - curPage * 10, 10)))
            {
                WriteLine($" • {client.EndPoint} (logged in as: '{client.User?.ToString() ?? "None"}'");
            }

            IOHelper.WriteBorder();
            WriteLine(" 8. Previous page");
            WriteLine(" 9. Next page");
            WriteLine(" 0. Return");
            IOHelper.WriteBorder();
            Write(" Enter Choice: ");
        }

        public static void ManageGroups(List<ChatGroup> groups, int curPage)
        {
            Clear();
            IOHelper.WriteHeader("Zelo Server Control Center");
            WriteLine($" List of chat groups (Page {curPage + 1}/{(groups.Count - 1) / 10 + 1}):");
            IOHelper.WriteBorder();

            foreach(ChatGroup group in groups.GetRange(curPage * 10, Math.Min(groups.Count - curPage * 10, 10)))
            {
                WriteLine($" • {group.ToString(true)}");
            }

            IOHelper.WriteBorder();
            WriteLine(" 1. Change chat group's name");
            WriteLine(" 2. Delete chat group");
            WriteLine(" 8. Previous page");
            WriteLine(" 9. Next page");
            WriteLine(" 0. Return");
            IOHelper.WriteBorder();
            Write(" Enter Choice: ");
        }

        // Return false when leaving viewer
        public static bool ActivityLog()
        {
            Clear();
            IOHelper.WriteHeader("Zelo Server Control Center");
            WriteLine(" Viewing activity log");
            WriteLine(" 'DEL' to clear log");
            WriteLine(" 'ESC' to return");
            IOHelper.WriteBorder();
            
            LogManager.WriteCurrentLog();
            LogManager.ToggleLogView(true);

            while(true)
            {
                ConsoleKey key = ReadKey(true).Key;

                switch(key)
                {
                    case ConsoleKey.Escape:
                        LogManager.ToggleLogView(false);
                        return false;

                    case ConsoleKey.Delete:
                        LogManager.ClearLog();
                        return true;
                }
            }
        }
    }

    public static class Misc
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
    }
}