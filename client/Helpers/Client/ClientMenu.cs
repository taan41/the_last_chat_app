using static System.Console;

static class ClientMenu
{
    public static void Connect(string serverIP, int port)
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

    public static void Welcome()
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine(" 1. Login");
        WriteLine(" 2. Register");
        WriteLine(" 0. Shut down client");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }
    
    public static void MainUser(string nickname)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" Zello, {nickname}!");
        IOHelper.WriteBorder();
        WriteLine(" 1. Change nickname");
        WriteLine(" 2. Change password");
        WriteLine(" 3. Friends");
        WriteLine(" 4. Chat groups");
        WriteLine(" 5. File folder");
        WriteLine(" 0. Logout");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }

    public static void Friends(List<Friend> friends, int curPage, int receivedRequestCount)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" Friend list (Page {curPage + 1}/{(friends.Count - 1) / 10 + 1}):");

        foreach(var friend in friends.GetRange(curPage * 10, Math.Min(friends.Count - curPage * 10, 10)))
        {
            WriteLine($" • {friend.Info()}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Start messaging");
        WriteLine($" 2. Received friend requests {(receivedRequestCount > 0 ? $"({receivedRequestCount})" : "")}");
        WriteLine(" 3. Remove friend");
        WriteLine(" 4. View list of all users");
        WriteLine(" 7. Refresh list");
        WriteLine(" 8. Previous page");
        WriteLine(" 9. Next page");
        WriteLine(" 0. Return");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }

    public static void ReceivedRq(List<User> users, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" Received requests from users (Page {curPage + 1}/{(users.Count - 1) / 10 + 1}):");

        foreach(var user in users.GetRange(curPage * 10, Math.Min(users.Count - curPage * 10, 10)))
        {
            WriteLine($" • {user.Info(false, true)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Accept");
        WriteLine(" 2. Accept all");
        WriteLine(" 3. Deny");
        WriteLine(" 4. Deny all");
        WriteLine(" 5. Block");
        WriteLine(" 6. Block all");
        WriteLine(" 7. Refresh list");
        WriteLine(" 8. Previous page");
        WriteLine(" 9. Next page");
        WriteLine(" 0. Return");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }

    public static void AllUser(List<User> users, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" Registered user list (Page {curPage + 1}/{(users.Count - 1) / 10 + 1}):");

        foreach(var user in users.GetRange(curPage * 10, Math.Min(users.Count - curPage * 10, 10)))
        {
            WriteLine($" • {user.Info(false, true)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Send friend request");
        // WriteLine(" 2. View sent requests");
        // WriteLine(" 3. Block");
        WriteLine(" 7. Refresh list");
        WriteLine(" 8. Previous page");
        WriteLine(" 9. Next page");
        WriteLine(" 0. Return");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }

    public static void ChatGroups(List<ChatGroup> subcribed, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" Subscribed chat groups (Page {curPage + 1}/{(subcribed.Count - 1) / 10 + 1}):");
        IOHelper.WriteBorder();

        foreach(var group in subcribed.GetRange(curPage * 10, Math.Min(subcribed.Count - curPage * 10, 10)))
        {
            WriteLine($" • {group.Info(true, true)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Join group");
        WriteLine(" 2. Unsubscribe");
        WriteLine(" 3. View all chat group list");
        WriteLine(" 4. Create & manage created chat groups");
        WriteLine(" 7. Refresh list");
        WriteLine(" 8. Previous page");
        WriteLine(" 9. Next page");
        WriteLine(" 0. Return");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }

    public static void AllGroups(List<ChatGroup> groups, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" All chat groups (Page {curPage + 1}/{(groups.Count - 1) / 10 + 1}):");
        IOHelper.WriteBorder();

        foreach(var group in groups.GetRange(curPage * 10, Math.Min(groups.Count - curPage * 10, 10)))
        {
            WriteLine($" • {group.Info(true, false)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Subcribe");
        WriteLine(" 7. Refresh list");
        WriteLine(" 8. Previous page");
        WriteLine(" 9. Next page");
        WriteLine(" 0. Return");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }

    public static void ManageCreated(List<ChatGroup> groups, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" Created chat groups (Page {curPage + 1}/{(groups.Count - 1) / 10 + 1}):");
        IOHelper.WriteBorder();

        foreach(var group in groups.GetRange(curPage * 10, Math.Min(groups.Count - curPage * 10, 10)))
        {
            WriteLine($" • {group.Info(true, false)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Create new");
        WriteLine(" 2. Change group's name");
        WriteLine(" 3. Delete");
        WriteLine(" 8. Previous page");
        WriteLine(" 9. Next page");
        WriteLine(" 0. Return");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }

    public static void FileFolder(string curPath)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine(" Current file folder:");
        WriteLine($" {curPath}");
        IOHelper.WriteBorder();
        WriteLine(" 1. View files in folder");
        WriteLine(" 2. Change folder");
        WriteLine(" 3. Revert to default folder");
        WriteLine(" 0. Return");
        IOHelper.WriteBorder();
        Write(" Enter choice: ");
    }

    public static void ViewFile(List<string> files, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" File list (Page {curPage + 1}/{(files.Count - 1) / 10 + 1}):");
        IOHelper.WriteBorder();

        foreach(var file in files.GetRange(curPage * 10, Math.Min(files.Count - curPage * 10, 10)))
        {
            WriteLine($" • {file}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Open");
        WriteLine(" 2. Delete");
        WriteLine(" 3. Change file's name");
        WriteLine(" 8. Previous page");
        WriteLine(" 9. Next page");
        WriteLine(" 0. Return");
        IOHelper.WriteBorder();
        Write(" Enter Choice: ");
    }

    public static void ChatCommands()
    {
        WriteLine(" /help             -- Show all chat commands");
        WriteLine(" /info             -- Show info of current chat room");
        WriteLine(" /clear /cls       -- Clear console");
        WriteLine(" /reload           -- Clear console then re-write all messages");
        WriteLine(" /file (filePath)  -- Send file to current chat room");
        WriteLine(" /leave            -- Leave chat room");
        WriteLine(" You can also leave using 'ESC' key");
    }
}