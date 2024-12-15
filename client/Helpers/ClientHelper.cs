using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using static System.Console;
using static Utilities;

static class ClientHelper
{
    public static class Action
    {
        public static void Login(NetworkStream stream, out User? loggedInUser)
        {
            byte[] buffer = new byte[MagicNum.bufferSize];
            Command cmdToSend = new();
            PasswordSet? pwdSet;
            loggedInUser = null;
            
            while(true)
            {
                ShowMenu.WelcomeMenu();
                WriteLine("1");
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

                cmdToSend.Set(CommandType.GetUserPwd, username);

                // Check if username exists and get password hash/salt of that username
                if (CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out Command receivedCmd))
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

                    if (CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out receivedCmd))
                    {
                        loggedInUser = User.Deserialize(receivedCmd.Payload);

                        if (loggedInUser == null || loggedInUser.UID == -1)
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

        public static void Register(NetworkStream stream)
        {
            byte[] buffer = new byte[MagicNum.bufferSize];
            StringBuilder username = new(), pwd = new(), confirmPwd = new(), nickname = new();
            Command cmdToSend = new();

            while(true)
            {
                ShowMenu.WelcomeMenu();
                WriteLine("2");
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
                if (!CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out _))
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
                if (CommandHandler.SendAndHandle(stream, ref buffer, cmdToSend, out _))
                {
                    IOHelper.WriteBorder();
                    WriteLine(" Registered successfully!");
                    ReadKey(true);
                }

                return;
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

                if (CommandHandler.SendAndHandle(stream, ref buffer, new(CommandType.ChangeNickname, newNickname), out _))
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

                if (CommandHandler.SendAndHandle(stream, ref buffer, new(CommandType.ChangePassword, PasswordSet.Serialize(newPwdSet)), out _))
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

                if (CommandHandler.SendAndHandle(stream, ref buffer, new(CommandType.CreateGroup, ChatGroup.Serialize(newGroup)), out _))
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
                    WriteLine(" Error: Invalid ID");
                    continue;
                }

                if (CommandHandler.SendAndHandle(stream, ref buffer, new(CommandType.DeleteGroup, groupID.ToString()), out _))
                {
                    IOHelper.WriteBorder();
                    WriteLine(" Deleted chat group successfully!");
                    ReadKey(true);
                }
                
                return;
            }
        }

        public static void SetPartner(NetworkStream stream, List<User> users, int curPage, int mainUserID, out User? partner)
        {
            byte[] buffer = new byte[MagicNum.bufferSize];
            partner = null;

            while(true)
            {
                ShowMenu.PrivateMsgMenu(users, curPage);
                WriteLine("1");
                IOHelper.WriteBorder();
                WriteLine(" < Press ESC to cancel >");

                Write(" Enter partner ID: ");
                string partnerIDString = "";
                switch(Misc.InputData(ref partnerIDString, "Partner ID", 1, 5, false))
                {
                    case null: return;
                    case true: break;
                    case false: continue;
                }

                int partnerID;

                try
                {
                    partnerID = Convert.ToInt32(partnerIDString);

                    if(partnerID < 1)
                        throw new FormatException();
                }
                catch (FormatException)
                {
                    WriteLine(" Error: Invalid ID");
                    ReadKey(true);
                    continue;
                }

                if(partnerID == mainUserID)
                {
                    WriteLine(" Error: Can't message to self");
                    ReadKey(true);
                    continue;
                }

                if (CommandHandler.SendAndHandle(stream, ref buffer, new(CommandType.SetPartner, partnerID.ToString()), out Command receivedCmd))
                {
                    partner = User.Deserialize(receivedCmd.Payload);

                    if(partner == null)
                    {
                        WriteLine(" Error: Received invalid user data");
                        ReadKey(true);
                        return;
                    }
                }
                
                return;
            }
        }

        public static void JoinGroupMenu(NetworkStream stream, List<ChatGroup> groups, int curPage, out ChatGroup? group)
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
                    WriteLine(" Error: Invalid ID");
                    continue;
                }

                if (CommandHandler.SendAndHandle(stream, ref buffer, new(CommandType.JoinGroup, groupID.ToString()), out Command receivedCmd))
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

        public static bool GetPartnerHistory(NetworkStream stream)
        {
            msgHistory.Clear();
            byte[] buffer = new byte[MagicNum.bufferSize];

            if (CommandHandler.SendAndHandle(stream, ref buffer, new(CommandType.GetPartnerHistory, null), out Command receivedCmd))
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

            if (CommandHandler.SendAndHandle(stream, ref buffer, new(CommandType.GetGroupHistory, groupID.ToString()), out Command receivedCmd))
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
            msgHistory.ForEach(msg => WriteLine(msg.ToString()));
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

        public static void StartChatting(NetworkStream stream, User mainUser, User? partner, ChatGroup? joinedGroup)
        {
            if(mainUser.UID == -1 || (joinedGroup == null && partner == null))
            {
                WriteLine(" Error: Null chatting data");
                return;
            }
            Clear();
            IOHelper.WriteHeader("Zelo");
            WriteLine(" All chat commands:");
            ShowMenu.ChatCommands();
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
            

            Task echoMsg = Task.Run(() => EchoMessage(stream, prompt, stopTokenSource.Token));
            
            WriteMsgHistory();
            WritePromt(prompt);

            while(true)
            {
                inputBuffer.Clear();
                content = IOHelper.ReadInput(inputBuffer, true, null, false);

                if (echoMsg.IsCompleted || echoMsg.IsFaulted || content == null)
                {
                    stopTokenSource.Cancel();
                    stopTokenSource.Dispose();
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                if (content.Trim().ElementAt(0) == '/')
                {
                    switch(content.Split(' ').ElementAt(0))
                    {
                        case "/help":
                            IOHelper.MoveCursor(- prompt.Length - inputBuffer.Length - WindowWidth);
                            WriteLine(new string(' ', WindowWidth - 1));
                            WriteLine("[Client] All chat commands:");
                            ShowMenu.ChatCommands();
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
                                WriteNotice($"\n[Client] Partner's info: '{partner.ToString(false)}'", prompt);
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

                        case "/leave":
                            stopTokenSource.Cancel();
                            stopTokenSource.Dispose();
                            return;
                        
                        default:
                            WriteNotice("[Client] Unknown chat command", prompt);
                            continue;
                    }
                }
                else
                {
                    Message message = new(mainUser, partner, joinedGroup, content);
                    cmdToSend.Set(CommandType.Message, Message.Serialize(message));
                    WriteMessage(message.ToString(), prompt);
                    lock (msgHistory)
                        msgHistory.Add(message);
                }

                stream.Write(EncodeString(Command.Serialize(cmdToSend)));
            }
        }

        public static async Task EchoMessage(NetworkStream stream, string prompt, CancellationToken stopToken)
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
                        case CommandType.MessageEcho:
                            Message? echoMsg = Message.Deserialize(receivedCmd.Payload);

                            if (echoMsg == null)
                                WriteNotice("[Client] Error: Null echo message", prompt);
                            else
                            {
                                WriteMessage(echoMsg.ToString(), prompt);
                                lock(msgHistory)
                                    msgHistory.Add(echoMsg);
                            }
                            continue;

                        case CommandType.GetGroupInfo:
                            ChatGroup? requestedGroup = ChatGroup.Deserialize(receivedCmd.Payload);
                            WriteNotice($"[Client] Group info: '{requestedGroup?.ToString(true)}'", prompt);
                            continue;

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
                WriteNotice($" Error while echoing msg: ({ex.GetType().Name}) {ex.Message}", prompt);
                // WriteLine(ex);
                ReadKey(true);
            }
        }

    }

    public static class ShowMenu
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
            WriteLine(" 1. Login");
            WriteLine(" 2. Register");
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
            WriteLine($" List of registered users (Page {curPage + 1}/{(users.Count - 1) / 10 + 1}):");

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
            WriteLine($" List of chat groups (Page {curPage + 1}/{(groups.Count - 1) / 10 + 1}):");
            IOHelper.WriteBorder();

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
            WriteLine($" List of created chat groups (Page {curPage + 1}/{(groups.Count - 1) / 10 + 1}):");
            IOHelper.WriteBorder();

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
            WriteLine(" /help             -- Show all chat commands");
            WriteLine(" /info             -- Show info of current chat room");
            WriteLine(" /clear /cls       -- Clear console");
            WriteLine(" /reload           -- Clear console then re-write all messages");
            WriteLine(" /file (filePath)  -- Send file to all connected users");
            WriteLine(" /leave            -- Leave chat room");
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
        public static bool SendAndHandle(NetworkStream stream, ref byte[] buffer, Command cmdToSend, out Command receivedCmd)
        {
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
            
            Command? tempCmd = Command.Deserialize(DecodeBytes(buffer, 0, totalRead));

            if(tempCmd == null)
            {
                WriteLine(" Error: Received null command");
                ReadKey(true);
                return false;
            }

            switch(tempCmd.CommandType)
            {
                case var value when value == cmdToSend.CommandType:
                    receivedCmd = tempCmd;
                    return true;

                case CommandType.Error:
                    WriteLine($" Error: {tempCmd.Payload}");
                    ReadKey(true);
                    return false;

                default:
                    WriteLine($" Error: Received invalid command {tempCmd.CommandType}");
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