using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using TextCopy;
using static System.Console;
using static Utilities;

static class ClientAction
{
    public static void Login(NetworkStream stream, ref byte[] buffer, out User? loggedInUser)
    {
        Command cmdToSend = new();
        PasswordSet? pwdSet;
        loggedInUser = null;

        while(true)
        {
            ClientMenu.Welcome();
            WriteLine("1");
            IOHelper.WriteBorder();
            WriteLine(" < Press ESC to cancel >");

            Write(" Enter username: ");
            string? username = ClientHelper.InputData("Username", MagicNum.usernameMin, MagicNum.nicknameMax, false);
            if (username == null)
                return;

            cmdToSend.Set(CommandType.GetUserPwd, username);

            // Check if username exists and get password hash/salt of that username
            if (ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out Command receivedCmd))
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
            string? pwd = ClientHelper.InputData("Password", MagicNum.passwordMin, MagicNum.passwordMax, true);
            if (pwd == null)
                return;

            if (VerifyPassword(pwd, pwdSet))
            {
                cmdToSend.Set(CommandType.Login, username);

                if (ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out receivedCmd))
                {
                    loggedInUser = User.Deserialize(receivedCmd.Payload);

                    if (loggedInUser == null || loggedInUser.UserID < 1)
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

    public static void Register(NetworkStream stream, ref byte[] buffer)
    {
        string?
            username = null,
            pwd = null,
            confirmPwd = null;
        Command cmdToSend = new();

        while(true)
        {
            ClientMenu.Welcome();
            WriteLine("2");
            IOHelper.WriteBorder();
            WriteLine(" < Press ESC to cancel >");

            Write(" Enter username   : ");
            if (username != null)
                WriteLine(username);
            else
                username = ClientHelper.InputData("Username", MagicNum.usernameMin, MagicNum.usernameMax, false);
            
            if (username == null)
                return;
                
            // Check availability of username
            cmdToSend.Set(CommandType.CheckUsername, username.ToString());
            if (!ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _))
            {
                username = null;
                continue;
            }

            Write(" Enter password   : ");
            if (pwd != null)
                WriteLine(new string('*', pwd.Length));
            else
                pwd = ClientHelper.InputData("Password", MagicNum.passwordMin, MagicNum.passwordMax, true);
            
            if (pwd == null)
                return;

            Write(" Confirm password : ");
            if (confirmPwd != null)
                WriteLine(new string('*', confirmPwd.Length));
            else
                confirmPwd = ClientHelper.InputData("Password", MagicNum.passwordMin, MagicNum.passwordMax, true);
            
            if (confirmPwd == null)
                return;
            
            if (!confirmPwd.ToString().Equals(pwd.ToString()))
            {
                pwd = null;
                confirmPwd = null;
                WriteLine(" Error: Mis-match confirm password");
                ReadKey(true);
                continue;
            }

            Write(" Enter nickname   : ");
            string? nickname = ClientHelper.InputData("Nickname", MagicNum.nicknameMin, MagicNum.nicknameMax, false);
            if (nickname == null)
                return;

            User registeredUser = new()
                {
                    Username = username,
                    Nickname = nickname,
                    PwdSet = HashPassword(pwd)
                };

            cmdToSend.Set(CommandType.Register, registeredUser.Serialize());
            if (ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _))
            {
                IOHelper.WriteBorder();
                WriteLine(" Registered successfully!");
                ReadKey(true);
            }

            return;
        }
    }

    public static void ChangeNickname(NetworkStream stream, ref byte[] buffer, ref User user)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter new nickname: ");
        string? newNickname = ClientHelper.InputData("Nickname", MagicNum.nicknameMin, MagicNum.nicknameMax, false);
        if (newNickname == null)
            return;

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.ChangeNickname, newNickname), out _))
        {
            user.Nickname = newNickname;
            IOHelper.WriteBorder();
            WriteLine(" Changed nickname successfully!");
            ReadKey(true);
        }
    }

    public static void ChangePassword(NetworkStream stream, ref byte[] buffer, ref User user)
    {
        if (user.PwdSet == null)
        {
            WriteLine(" Error: Null PasswordSet");
            ReadKey(true);
            return;
        }

        string?
            oldPwd = null,
            newPwd = null,
            confirmPwd;

        while(true)
        {
            ClientMenu.MainUser(user.Nickname);
            WriteLine("2");
            IOHelper.WriteBorder();
            WriteLine(" < Press ESC to cancel >");

            Write(" Enter old password   : ");
            if (oldPwd != null)
                WriteLine(new string('*', oldPwd.Length));
            else
                oldPwd = ClientHelper.InputData("Password", MagicNum.passwordMin, MagicNum.passwordMax, true);
            
            if (oldPwd == null)
                return;

            if (!VerifyPassword(oldPwd.ToString(), user.PwdSet))
            {
                oldPwd = null;
                WriteLine(" Error: Wrong password");
                ReadKey(true);
                continue;
            }

            Write(" Enter new password   : ");
            if (newPwd != null)
                WriteLine(new string('*', newPwd.Length));
            else
                newPwd = ClientHelper.InputData("Password", MagicNum.passwordMin, MagicNum.passwordMax, true);

            if (newPwd == null)
                return;

            if (newPwd.Equals(oldPwd))
            {
                newPwd = null;
                WriteLine(" Error: New password must be different");
                ReadKey(true);
                continue;
            }

            Write(" Confirm new password : ");
            confirmPwd = ClientHelper.InputData("Password", MagicNum.passwordMin, MagicNum.passwordMax, true);
            if (confirmPwd == null)
                return;

            if (!confirmPwd.Equals(newPwd))
            {
                newPwd = null;
                WriteLine(" Error: Mis-match confirm password");
                ReadKey(true);
                continue;
            }

            PasswordSet newPwdSet = HashPassword(newPwd);

            if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.ChangePassword, newPwdSet.Serialize()), out _))
            {
                user.PwdSet = newPwdSet;
                IOHelper.WriteBorder();
                WriteLine(" Changed password successfully!");
                ReadKey(true);
            }
        }
    }

    public static List<User>? GetUsers(NetworkStream stream, ref byte[] buffer, CommandType cmdType)
    {
        Command cmdToSend = new(cmdType, null);

        if (ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out Command receivedCmd))
        {
            var users = JsonSerializer.Deserialize<List<User>>(receivedCmd.Payload);

            if (users == null)
            {
                WriteLine(" Error: Received null user list");
                ReadKey(true);
            }

            return users;
        }

        return null;
    }

    public static List<Friend>? GetFriendList(NetworkStream stream, ref byte[] buffer)
    {
        Command cmdToSend = new(CommandType.GetFriendList, null);

        if (ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out Command receivedCmd))
        {
            var friends = JsonSerializer.Deserialize<List<Friend>>(receivedCmd.Payload);

            if (friends == null)
            {
                WriteLine(" Error: Received null friend list");
                ReadKey(true);
            }

            return friends;
        }

        return null;
    }

    public static void SetPartner(NetworkStream stream, ref byte[] buffer, List<Friend> friends, int mainUserID, out User? partner)
    {
        partner = null;
    
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter friend's ID: ");
        string? partnerIDString = ClientHelper.InputData("ID", 0, null, false);
        if (partnerIDString == null)
            return;

        int partnerID;

        try
        {
            partnerID = Convert.ToInt32(partnerIDString);

            if(!friends.Any(friend => friend.BaseUser.UserID == partnerID))
                throw new FormatException();
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid ID");
            ReadKey(true);
            return;
        }

        if (partnerID == mainUserID)
        {
            WriteLine(" Error: Can't message to self");
            ReadKey(true);
            return;
        }

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.SetPartner, partnerID.ToString()), out Command receivedCmd))
        {
            partner = User.Deserialize(receivedCmd.Payload);

            if(partner == null)
            {
                WriteLine(" Error: Received invalid user data");
                ReadKey(true);
            }
        }
    }

    public static void RemoveFriend(NetworkStream stream, ref byte[] buffer, List<Friend> friends)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter friend's ID: ");
        string? friendIDString = ClientHelper.InputData("ID", 0, null, false);
        if (friendIDString == null)
            return;

        int friendID;
        try
        {
            friendID = Convert.ToInt32(friendIDString);

            if(!friends.Any(friend => friend.BaseUser.UserID == friendID))
                throw new FormatException();
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid ID");
            ReadKey(true);
            return;
        }

        Command cmdToSend = new(CommandType.RemoveFriend, friendID.ToString());
        ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _);
    }

    public static void ProcessRequest(NetworkStream stream, ref byte[] buffer, CommandType cmdType, List<User>? receivedRqs)
    {
        int? userID = null;

        if (receivedRqs != null)
        {
            IOHelper.WriteBorder();
            WriteLine(" < Press ESC to cancel >");

            Write(" Enter user's ID: ");
            string? userIDString = ClientHelper.InputData("ID", 0, null, false);
            if (userIDString == null)
                return;

            try
            {
                userID = Convert.ToInt32(userIDString);

                if(!receivedRqs.Any(user => user.UserID == userID))
                    throw new FormatException();
            }
            catch (FormatException)
            {
                WriteLine(" Error: Invalid ID");
                ReadKey(true);
                return;
            }
        }

        Command cmdToSend = new(cmdType, userID?.ToString());
        ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _);
    }

    public static void SendRequest(NetworkStream stream, ref byte[] buffer, int mainUserID)
    {
        int? userID;
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter user's ID: ");
        string? userIDString = ClientHelper.InputData("ID", 0, null, false);
        if (userIDString == null)
            return;

        try
        {
            userID = Convert.ToInt32(userIDString);
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid ID");
            ReadKey(true);
            return;
        }

        if (userID == mainUserID)
        {
            WriteLine(" Error: Can't send request to self");
            ReadKey(true);
            return;
        }
        

        Command cmdToSend = new(CommandType.SendFriendRq, userID.ToString());
        if(ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Sent friend request successfully");
            ReadKey(true);
        }
    }

    public static void BlockUser(NetworkStream stream, ref byte[] buffer, int mainUserID)
    {
        int? userID;
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter user's ID: ");
        string? userIDString = ClientHelper.InputData("ID", 0, null, false);
        if (userIDString == null)
            return;

        try
        {
            userID = Convert.ToInt32(userIDString);
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid ID");
            ReadKey(true);
            return;
        }

        if (userID == mainUserID)
        {
            WriteLine(" Error: Can't block self");
            ReadKey(true);
            return;
        }
        

        Command cmdToSend = new(CommandType.BlockUser, userID.ToString());
        if(ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Blocked user successfully");
            ReadKey(true);
        }
    }

    public static List<ChatGroup>? GetGroups(NetworkStream stream, ref byte[] buffer, CommandType cmdTypeToSend)
    {
        Command cmdToSend = new(cmdTypeToSend, null);

        if (ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out Command receivedCmd))
        {
            var groups = JsonSerializer.Deserialize<List<ChatGroup>>(receivedCmd.Payload);

            if (groups == null)
            {
                WriteLine(" Error: Received null subcribed list");
                ReadKey(true);
            }

            return groups;
        }

        return null;
    }

    public static void SubUnsubToGroup(NetworkStream stream, ref byte[] buffer, List<ChatGroup> subcribedGroups, bool subcribing)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter chat group ID: ");
        string? groupIDString = ClientHelper.InputData("ID", 0, null, false);
        if (groupIDString == null)
            return;
        
        int groupID;
        try
        {
            groupID = Convert.ToInt32(groupIDString);

            if(subcribing == subcribedGroups.Any(group => group.GroupID == groupID))
                throw new FormatException();
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid ID");
            ReadKey(true);
            return;
        }

        if(ClientHelper.SendCmd(stream, ref buffer, new(subcribing ? CommandType.SubcribeToGroup : CommandType.RemoveSubcribed, groupID.ToString()), out _))
        {
            WriteLine($"{(subcribing ? "S" : "Uns")}ubscribe successfully");
            ReadKey(true);
        }
    }

    public static void JoinGroup(NetworkStream stream, ref byte[] buffer, List<ChatGroup> subcribedGroups, out ChatGroup? group)
    {
        group = null;

        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter chat group ID: ");
        string? groupIDString = ClientHelper.InputData("ID", 0, null, false);
        if (groupIDString == null)
            return;

        int groupID;
        try
        {
            groupID = Convert.ToInt32(groupIDString);

            if(!subcribedGroups.Any(group => group.GroupID == groupID))
                throw new FormatException();
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid ID");
            ReadKey(true);
            return;
        }

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.JoinGroup, groupID.ToString()), out Command receivedCmd))
        {
            group = ChatGroup.Deserialize(receivedCmd.Payload);

            if(group == null)
            {
                WriteLine(" Error: Received invalid chat group data");
                ReadKey(true);
            }
        }
    }

    public static void CreateGroup(NetworkStream stream, ref byte[] buffer, int creatorID)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter chat group name: ");
        string? groupName = ClientHelper.InputData("Chat group name", MagicNum.groupnameMin, MagicNum.groupNameMax, false);
        if (groupName == null)
            return;

        ChatGroup newGroup = new()
        {
            CreatorID = creatorID,
            GroupName = groupName
        };

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.CreateGroup, newGroup.Serialize()), out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Created chat group successfully!");
            ReadKey(true);
        }
        
        return;
    }

    public static void ChangeGroupName(NetworkStream stream, ref byte[] buffer, List<ChatGroup> createdGroups)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter chat group ID: ");
        string? groupIDString = ClientHelper.InputData("ID", 0, null, false);
        if (groupIDString == null)
            return;

        int groupID;

        try
        {
            groupID = Convert.ToInt32(groupIDString);

            if(!createdGroups.Any(group => group.GroupID == groupID))
                throw new FormatException();
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid ID");
            ReadKey(true);
            return;
        }

        ChatGroup? groupToChange = createdGroups.Find(group => group.GroupID == groupID);
        if (groupToChange == null)
            return;

        WriteLine($" Group's current name: {groupToChange.GroupName}");
        Write(" Enter new name: ");
        string? newName = ClientHelper.InputData("Group name", MagicNum.groupnameMin, MagicNum.groupNameMax, false);
        if (newName == null)
            return;

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.ChangeGroupName, newName), out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Changed chat group's name successfully!");
            ReadKey(true);
        }
        
        return;

    }

    public static void DeleteGroup(NetworkStream stream, ref byte[] buffer, List<ChatGroup> createdGroups)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter chat group ID: ");
        string? groupIDString = ClientHelper.InputData("ID", 0, null, false);
        if (groupIDString == null)
            return;

        int groupID;
        try
        {
            groupID = Convert.ToInt32(groupIDString);
            
            if(groupID < 1)
                throw new FormatException();

            if(!createdGroups.Any(group => group.GroupID == groupID))
                throw new FormatException();
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid ID");
            ReadKey(true);
            return;
        }

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.DeleteGroup, groupID.ToString()), out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Deleted chat group successfully!");
            ReadKey(true);
        }
    }

    public static void OpenDelCopyFile(string dirPath, List<string> files, int openDelCopy)
    {
        if (files.Count == 0)
            return;

        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter file's index: ");
        string? fileIndexString = IOHelper.ReadInput(false);
        if (fileIndexString == null)
            return;

        int fileIndex;
        try
        {
            fileIndex = Convert.ToInt32(fileIndexString);
            
            if(fileIndex < 0 || fileIndex >= files.Count)
                throw new FormatException();
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid index");
            ReadKey(true);
            return;
        }

        string filePath = files[fileIndex];

        if (openDelCopy == 1)
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        else if (openDelCopy == 2 && File.Exists(filePath))
            File.Delete(filePath);
        else if (openDelCopy == 3)
            ClipboardService.SetText(filePath);
    }

    public static void ChangeFileName(string dirPath, List<string> files)
    {
        if (files.Count == 0)
            return;

        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter file's index: ");
        string? fileIndexString = IOHelper.ReadInput(false);
        if (fileIndexString == null)
            return;

        int fileIndex;
        try
        {
            fileIndex = Convert.ToInt32(fileIndexString);
            
            if(fileIndex < 0 || fileIndex >= files.Count)
                throw new FormatException();
        }
        catch (FormatException)
        {
            WriteLine(" Error: Invalid index");
            ReadKey(true);
            return;
        }

        Write(" Enter file's new name (without extension): ");
        string? fileName = IOHelper.ReadInput(false);
        if (fileName == null)
            return;
            
        fileName += Path.GetExtension(files[fileIndex]);

        if (File.Exists(dirPath + fileName))
        {
            WriteLine(" Error: File with same name already exists");
            ReadKey(true);
        }
        else
            File.Move(files[fileIndex], dirPath + fileName);
    }

    public static void ChangeFolder(ref string curFolder)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Press ESC to cancel >");

        Write(" Enter file's index: ");
        string? newFolder = IOHelper.ReadInput(false);
        if (newFolder == null)
            return;

        if (!Path.Exists(newFolder))
        {
            WriteLine(" Error: Folder does not exist");
            WriteLine(" Do you want to create folder? (Y/N): ");
            if(IOHelper.ReadConfirm() ?? false)
            {
                Directory.CreateDirectory(newFolder);
                curFolder = newFolder;
                IOHelper.WriteBorder();
                WriteLine(" Changed save folder successfully");
            }
            else
                WriteLine(" Failed to change save folder");
            ReadKey(true);
        }
        else
        {
            curFolder = newFolder;
            IOHelper.WriteBorder();
            WriteLine(" Changed save folder successfully");
            ReadKey(true);
        }

    }

    private static readonly StringBuilder inputBuffer = new(MagicNum.inputLimit);
    private static readonly List<Message> msgHistory = [];

    public static bool GetPartnerHistory(NetworkStream stream)
    {
        msgHistory.Clear();
        byte[] buffer = new byte[MagicNum.bufferSize];

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.GetPartnerHistory, null), out Command receivedCmd))
        {
            List<Message>? messages = JsonSerializer.Deserialize<List<Message>>(receivedCmd.Payload);

            if (messages != null)
            {
                msgHistory.AddRange(messages);
                return true;
            }
        }

        return false;
    }

    public static bool GetGroupHistory(NetworkStream stream, int groupID)
    {
        msgHistory.Clear();
        byte[] buffer = new byte[MagicNum.bufferSize];

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.GetGroupHistory, groupID.ToString()), out Command receivedCmd))
        {
            List<Message>? messages = JsonSerializer.Deserialize<List<Message>>(receivedCmd.Payload);

            if (messages != null)
            {
                msgHistory.AddRange(messages);
                return true;
            }
        }

        return false;
    }

    public static void WritePromt(string prompt)
    {
        IOHelper.WriteBorder();
        Write(prompt);
        Write(inputBuffer);
    }

    public static void WriteMsgHistory()
    {
        Clear();
        msgHistory.ForEach(msg => WriteLine(msg.Print()));
    }

    public static void WriteMessage(string content, string prompt)
    {
        IOHelper.MoveCursor(- prompt.Length - inputBuffer.Length - WindowWidth);
        WriteLine(content.ToString().PadRight(WindowWidth - 1));
        WritePromt(prompt);
    }

    public static void WriteNotice(string content, string prompt)
    {
        IOHelper.MoveCursor(- prompt.Length - inputBuffer.Length - WindowWidth);
        WriteLine(new string(' ', WindowWidth - 1));
        WriteLine(content.ToString().PadRight(WindowWidth - 1));
        WritePromt(prompt);
    }

    public static void StartChatting(NetworkStream stream, User mainUser, User? partner, ChatGroup? joinedGroup, string savePath)
    {
        if(mainUser.UserID == -1 || (joinedGroup == null && partner == null))
        {
            WriteLine(" Error: Invalid chatting data");
            return;
        }
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine(" All chat commands:");
        ClientMenu.ChatCommands();
        IOHelper.WriteBorder();

        if (joinedGroup != null && GetGroupHistory(stream, joinedGroup.GroupID))
            WriteLine(" Load group's message history successfully");
        else if (partner != null && GetPartnerHistory(stream))
            WriteLine(" Load message history successfully");
        else
            WriteLine(" Failed to load message history");

        WriteLine(" Press any key to continue...");
        ReadKey(true);
        Clear();

        string prompt = $"[{mainUser.Nickname}] > ";
        string? content;
        Command cmdToSend = new();

        CancellationTokenSource stopTokenSource = new();

        Task echoMsg = Task.Run(() => EchoMessage(stream, prompt, savePath, stopTokenSource.Token));
        
        WriteMsgHistory();
        WritePromt(prompt);

        try
        {
            while(true)
            {
                inputBuffer.Clear();
                content = IOHelper.ReadInput(inputBuffer, true, null, false);

                if (echoMsg.IsCompleted || content == null)
                {
                    stopTokenSource.Cancel();
                    stopTokenSource.Dispose();
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                if (content.Trim().ElementAt(0) == '/')
                {
                    string chatCmd = content.Trim();
                    int spaceIndex = chatCmd.IndexOf(' ');
                    if (spaceIndex == -1) spaceIndex = chatCmd.Length;

                    switch(chatCmd[..spaceIndex])
                    {
                        case "/help":
                            IOHelper.MoveCursor(- prompt.Length - inputBuffer.Length - WindowWidth);
                            WriteLine(new string(' ', WindowWidth - 1));
                            WriteLine("[Client] All chat commands:");
                            ClientMenu.ChatCommands();
                            WritePromt(prompt);
                            continue;

                        case "/info":
                            if (joinedGroup != null)
                            {
                                cmdToSend.Set(CommandType.GetGroupInfo, joinedGroup.GroupID.ToString());
                                break;
                            }
                            else if (partner != null)
                            {
                                WriteNotice($"[Client] Partner's info: '{partner.Info(false, true)}'", prompt);
                            }
                            continue;

                        case "/clear": case "/cls":
                            Clear();
                            WritePromt(prompt);
                            continue;

                        case "/reload":
                            WriteMsgHistory();
                            WritePromt(prompt);
                            continue;

                        case "/file":
                            if (chatCmd.Length == spaceIndex)
                                WriteNotice("[Client] Error: Invalid file path", prompt);
                            else
                            {
                                string filePath = chatCmd[(spaceIndex + 1)..].Replace("\"", "");
                                WriteNotice($"[Client] Start sending file {filePath}", prompt);

                                if (SendFile(stream, filePath))
                                    WriteNotice("[Client] Sent file successfully", prompt);
                                else
                                    WriteNotice("[Client] Failed to send file", prompt);
                            }
                            continue;

                        case "/leave":
                            stopTokenSource.Cancel();
                            stopTokenSource.Dispose();
                            return;
                        
                        default:
                            WriteNotice("[Client] Error: Unknown chat command", prompt);
                            continue;
                    }
                }
                else
                {
                    Message message = new(mainUser, partner, joinedGroup, content);
                    cmdToSend.Set(CommandType.Message, message.Serialize());
                    WriteMessage(message.Print(), prompt);
                    lock (msgHistory)
                        msgHistory.Add(message);
                }

                stream.Write(EncodeString(cmdToSend.Serialize()));
            }
        }
        catch (Exception ex)
        {
            WriteNotice($" Error: {ex}", prompt);
            ClientHelper.SendDisconnect(stream);
        }
    }

    private static bool doneSendingFile = false;
    private static readonly string[] waitting = [
        "Sending file.   ",
        "Sending file..  ",
        "Sending file... ",
        "Sending file...."
    ];

    private static bool SendFile(NetworkStream stream, string filePath)
    {
        FileData fileToSend = new(Path.GetFileName(filePath), File.ReadAllBytes(filePath));
        Command cmdToSend = new(CommandType.SendFile, fileToSend.Serialize());

        lock (stream)
        {
            stream.Write(EncodeString(cmdToSend.Serialize()));
            stream.Write(fileToSend.FileBytes);
        }

        int waitIndex = 1;
        while(!doneSendingFile)
        {
            Write(waitting[waitIndex++]);
            IOHelper.MoveCursor(-16);
            waitIndex %= 4;
            Task.Delay(500);
        }

        return true;
    }

    public static async Task EchoMessage(NetworkStream stream, string prompt, string savePath, CancellationToken stopToken)
    {
        byte[] buffer = new byte[MagicNum.bufferSize];
        int bytesRead, totalRead = 0;
        Command? receivedCmd;

        try
        {
            while(true)
            {
                while((bytesRead = await stream.ReadAsync(buffer, totalRead, 1024, stopToken)) > 0)
                {
                    totalRead += bytesRead;
                    
                    if(bytesRead < 1024)
                        break;

                    if(totalRead + 1024 >= buffer.Length)
                        Array.Resize(ref buffer, buffer.Length * 2);
                }

                if(stopToken.IsCancellationRequested)
                    return;

                receivedCmd = Command.Deserialize(DecodeBytes(buffer, 0, totalRead));
                totalRead = 0;

                switch(receivedCmd?.CommandType)
                {
                    case CommandType.EchoMessage:
                        Message? echoMsg = Message.Deserialize(receivedCmd.Payload);

                        if (echoMsg == null)
                            WriteNotice("[Client] Error: Null echo message", prompt);
                        else
                        {
                            WriteMessage(echoMsg.Print(), prompt);
                            lock(msgHistory)
                                msgHistory.Add(echoMsg);
                        }
                        continue;

                    case CommandType.GetGroupInfo:
                        ChatGroup? requestedGroup = ChatGroup.Deserialize(receivedCmd.Payload);
                        WriteNotice($"[Client] Group info: '{requestedGroup?.Info(true, true)}'", prompt);
                        continue;

                    case CommandType.DoneSendingFile:
                        doneSendingFile = true;
                        continue;

                    case CommandType.SendFile:
                        string? filePath = await ReceiveFile(stream, receivedCmd, savePath, stopToken);
                        if (filePath != null)
                            WriteNotice($"[Client] File received and saved as {Path.GetFileName(filePath)}", prompt);
                        continue;

                    case CommandType.Disconnect:
                        WriteNotice("[Client] Server shut down", prompt);
                        return;

                    case CommandType.Error:
                        WriteNotice($"[Client] Error while echoing: {receivedCmd.Payload}", prompt);
                        return;

                    default:
                        WriteNotice($"[Client] Error while echoing: Received invalid command {receivedCmd?.CommandType}", prompt);
                        return;
                }
            }
        }
        catch(OperationCanceledException) { /* ignored */ }
        catch(IOException) {}
        catch(Exception ex)
        {
            WriteNotice($"[Client] Error while echoing msg: ({ex.GetType().Name}) {ex.Message}", prompt);
            // WriteLine(ex);
            ReadKey(true);
        }
    }

    private static async Task<string?> ReceiveFile(NetworkStream stream, Command cmd, string savePath, CancellationToken stopToken)
    {
        FileData? file = FileData.Deserialize(cmd.Payload);

        if (file == null)
            return null;

        int bytesRead = 0;
        byte[] fileBuffer = new byte[file.FileSize];
        while (bytesRead < file.FileSize)
            bytesRead += await stream.ReadAsync(fileBuffer, bytesRead, file.FileSize - bytesRead, stopToken);

        // Save file
        string filePath = savePath + file.FileName;
        string fileNameWOExt = Path.GetFileNameWithoutExtension(filePath);
        string fileExtension = Path.GetExtension(filePath);
        int counter = 1;
        while(File.Exists(filePath))
        {
            string newFileName = $"{fileNameWOExt} ({counter}){fileExtension}";
            filePath = Path.Combine(savePath, newFileName);
            counter++;
        }
        await File.WriteAllBytesAsync(filePath, fileBuffer, stopToken);
        return filePath;
    }
}