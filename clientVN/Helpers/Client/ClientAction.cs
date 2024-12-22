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
            WriteLine(" < Nhấn 'ESC' để hủy >");

            Write(" Nhập username: ");
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
                    WriteLine(" Lỗi: Thông tin mật khẩu không hợp lệ");
                    ReadKey(true);
                    continue;
                }
            }
            else continue;

            Write(" Nhập mật khẩu: ");
            string? pwd = ClientHelper.InputData("Mật khẩu", MagicNum.passwordMin, MagicNum.passwordMax, true);
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
                        WriteLine(" Lỗi: Thông tin người dùng không hợp lệ");
                        ReadKey(true);
                        return;
                    }
                    else
                    {
                        loggedInUser.PwdSet = pwdSet;
                        IOHelper.WriteBorder();
                        WriteLine(" Đăng nhập thành công");
                        ReadKey(true);
                        return;
                    }
                }
            }
            else
            {
                WriteLine(" Lỗi: Mật khẩu sai");
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
            WriteLine(" < Nhấn 'ESC' để hủy >");

            Write(" Nhập username: ");
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

            Write(" Nhập mật khẩu: ");
            if (pwd != null)
                WriteLine(new string('*', pwd.Length));
            else
                pwd = ClientHelper.InputData("Mật khẩu", MagicNum.passwordMin, MagicNum.passwordMax, true);
            
            if (pwd == null)
                return;

            Write(" Xác nhận mật khẩu: ");
            if (confirmPwd != null)
                WriteLine(new string('*', confirmPwd.Length));
            else
                confirmPwd = ClientHelper.InputData("Mật khẩu", MagicNum.passwordMin, MagicNum.passwordMax, true);
            
            if (confirmPwd == null)
                return;
            
            if (!confirmPwd.ToString().Equals(pwd.ToString()))
            {
                pwd = null;
                confirmPwd = null;
                WriteLine(" Lỗi: Mis-match confirm mật khẩu");
                ReadKey(true);
                continue;
            }

            Write(" Nhập nickname   : ");
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
                WriteLine(" Đăng ký thành công");
                ReadKey(true);
            }

            return;
        }
    }

    public static void ChangeNickname(NetworkStream stream, ref byte[] buffer, ref User user)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập nickname mới: ");
        string? newNickname = ClientHelper.InputData("Nickname", MagicNum.nicknameMin, MagicNum.nicknameMax, false);
        if (newNickname == null)
            return;

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.ChangeNickname, newNickname), out _))
        {
            user.Nickname = newNickname;
            IOHelper.WriteBorder();
            WriteLine(" Đổi nickname thành công");
            ReadKey(true);
        }
    }

    public static void ChangePassword(NetworkStream stream, ref byte[] buffer, ref User user)
    {
        if (user.PwdSet == null)
        {
            WriteLine(" Lỗi: Thông tin mật khẩu không hợp lệ");
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
            WriteLine(" < Nhấn 'ESC' để hủy >");

            Write(" Nhập mật khẩu cũ: ");
            if (oldPwd != null)
                WriteLine(new string('*', oldPwd.Length));
            else
                oldPwd = ClientHelper.InputData("Mật khẩu", MagicNum.passwordMin, MagicNum.passwordMax, true);
            
            if (oldPwd == null)
                return;

            if (!VerifyPassword(oldPwd.ToString(), user.PwdSet))
            {
                oldPwd = null;
                WriteLine(" Lỗi: Mật khẩu sai");
                ReadKey(true);
                continue;
            }

            Write(" Nhập mật khẩu mới: ");
            if (newPwd != null)
                WriteLine(new string('*', newPwd.Length));
            else
                newPwd = ClientHelper.InputData("Mật khẩu", MagicNum.passwordMin, MagicNum.passwordMax, true);

            if (newPwd == null)
                return;

            if (newPwd.Equals(oldPwd))
            {
                newPwd = null;
                WriteLine(" Lỗi: Mật khẩu mới phải khác mật khẩu cũ");
                ReadKey(true);
                continue;
            }

            Write(" Xác nhận mật khẩu mới: ");
            confirmPwd = ClientHelper.InputData("Mật khẩu", MagicNum.passwordMin, MagicNum.passwordMax, true);
            if (confirmPwd == null)
                return;

            if (!confirmPwd.Equals(newPwd))
            {
                newPwd = null;
                WriteLine(" Lỗi: Xác nhận mật khẩu sai");
                ReadKey(true);
                continue;
            }

            PasswordSet newPwdSet = HashPassword(newPwd);

            if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.ChangePassword, newPwdSet.Serialize()), out _))
            {
                user.PwdSet = newPwdSet;
                IOHelper.WriteBorder();
                WriteLine(" Đổi mật khẩu thành công");
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
                WriteLine(" Lỗi: Thông tin danh sách không hợp lệ");
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
                WriteLine(" Lỗi: Thông tin danh sách không hợp lệ");
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
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập ID bạn bè: ");
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
            WriteLine(" Lỗi: ID không hợp lệ");
            ReadKey(true);
            return;
        }

        if (partnerID == mainUserID)
        {
            WriteLine(" Lỗi: Không thể nhắn tin bản thân");
            ReadKey(true);
            return;
        }

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.SetPartner, partnerID.ToString()), out Command receivedCmd))
        {
            partner = User.Deserialize(receivedCmd.Payload);

            if(partner == null)
            {
                WriteLine(" Lỗi: Thông tin người dùng không hợp lệ");
                ReadKey(true);
            }
        }
    }

    public static void RemoveFriend(NetworkStream stream, ref byte[] buffer, List<Friend> friends)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập ID bạn bè: ");
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
            WriteLine(" Lỗi: ID không hợp lệ");
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
            WriteLine(" < Nhấn 'ESC' để hủy >");

            Write(" Nhập ID người dùng: ");
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
                WriteLine(" Lỗi: ID không hợp lệ");
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
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập ID người dùng: ");
        string? userIDString = ClientHelper.InputData("ID", 0, null, false);
        if (userIDString == null)
            return;

        try
        {
            userID = Convert.ToInt32(userIDString);
        }
        catch (FormatException)
        {
            WriteLine(" Lỗi: ID không hợp lệ");
            ReadKey(true);
            return;
        }

        if (userID == mainUserID)
        {
            WriteLine(" Lỗi: Không thể gửi lời mời tới bản thân");
            ReadKey(true);
            return;
        }
        

        Command cmdToSend = new(CommandType.SendFriendRq, userID.ToString());
        if(ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Gửi lời mời kết bạn thành công");
            ReadKey(true);
        }
    }

    public static void BlockUser(NetworkStream stream, ref byte[] buffer, int mainUserID)
    {
        int? userID;
        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập ID người dùng: ");
        string? userIDString = ClientHelper.InputData("ID", 0, null, false);
        if (userIDString == null)
            return;

        try
        {
            userID = Convert.ToInt32(userIDString);
        }
        catch (FormatException)
        {
            WriteLine(" Lỗi: ID không hợp lệ");
            ReadKey(true);
            return;
        }

        if (userID == mainUserID)
        {
            WriteLine(" Lỗi: Không thể chặn bản thân");
            ReadKey(true);
            return;
        }
        

        Command cmdToSend = new(CommandType.BlockUser, userID.ToString());
        if(ClientHelper.SendCmd(stream, ref buffer, cmdToSend, out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Chặn thành công");
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
                WriteLine(" Lỗi: Thông tin danh sách không hợp lệ");
                ReadKey(true);
            }

            return groups;
        }

        return null;
    }

    public static void SubUnsubToGroup(NetworkStream stream, ref byte[] buffer, List<ChatGroup> subcribedGroups, bool subcribing)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập ID nhóm: ");
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
            WriteLine(" Lỗi: ID không hợp lệ");
            ReadKey(true);
            return;
        }

        if(ClientHelper.SendCmd(stream, ref buffer, new(subcribing ? CommandType.SubcribeToGroup : CommandType.RemoveSubcribed, groupID.ToString()), out _))
        {
            WriteLine($"{(subcribing ? "T" : "Hủy t")}ham gia thành công");
            ReadKey(true);
        }
    }

    public static void JoinGroup(NetworkStream stream, ref byte[] buffer, List<ChatGroup> subcribedGroups, out ChatGroup? group)
    {
        group = null;

        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập ID nhóm: ");
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
            WriteLine(" Lỗi: ID không hợp lệ");
            ReadKey(true);
            return;
        }

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.JoinGroup, groupID.ToString()), out Command receivedCmd))
        {
            group = ChatGroup.Deserialize(receivedCmd.Payload);

            if(group == null)
            {
                WriteLine(" Lỗi: Thông tin danh sách không hợp lệ");
                ReadKey(true);
            }
        }
    }

    public static void CreateGroup(NetworkStream stream, ref byte[] buffer, int creatorID)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập tên nhóm: ");
        string? groupName = ClientHelper.InputData("Tên nhóm", MagicNum.groupnameMin, MagicNum.groupNameMax, false);
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
            WriteLine(" Tạo nhóm thành công");
            ReadKey(true);
        }
        
        return;
    }

    public static void ChangeGroupName(NetworkStream stream, ref byte[] buffer, List<ChatGroup> createdGroups)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập ID nhóm: ");
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
            WriteLine(" Lỗi: ID không hợp lệ");
            ReadKey(true);
            return;
        }

        ChatGroup? groupToChange = createdGroups.Find(group => group.GroupID == groupID);
        if (groupToChange == null)
            return;

        WriteLine($" Tên nhóm hiện tại: {groupToChange.GroupName}");
        Write(" Nhập tên mới: ");
        string? newName = ClientHelper.InputData("Tên nhóm", MagicNum.groupnameMin, MagicNum.groupNameMax, false);
        if (newName == null)
            return;

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.ChangeGroupName, newName), out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Đổi tên nhóm thành công");
            ReadKey(true);
        }
        
        return;

    }

    public static void DeleteGroup(NetworkStream stream, ref byte[] buffer, List<ChatGroup> createdGroups)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập ID nhóm: ");
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
            WriteLine(" Lỗi: ID không hợp lệ");
            ReadKey(true);
            return;
        }

        if (ClientHelper.SendCmd(stream, ref buffer, new(CommandType.DeleteGroup, groupID.ToString()), out _))
        {
            IOHelper.WriteBorder();
            WriteLine(" Xóa nhóm thành công");
            ReadKey(true);
        }
    }

    public static void OpenDelCopyFile(string dirPath, List<string> files, int openDelCopy)
    {
        if (files.Count == 0)
            return;

        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập thứ tự tệp: ");
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
            WriteLine(" Lỗi: Thứ tự không hợp lệ");
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
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập thứ tự tệp: ");
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
            WriteLine(" Lỗi: Thứ tự không hợp lệ");
            ReadKey(true);
            return;
        }

        Write(" Nhập tên mới (không đuôi): ");
        string? fileName = IOHelper.ReadInput(false);
        if (fileName == null)
            return;
            
        fileName += Path.GetExtension(files[fileIndex]);

        if (File.Exists(dirPath + fileName))
        {
            WriteLine(" Lỗi: Tệp cùng tên đã tồn tại");
            ReadKey(true);
        }
        else
            File.Move(files[fileIndex], dirPath + fileName);
    }

    public static void ChangeFolder(ref string curFolder)
    {
        IOHelper.WriteBorder();
        WriteLine(" < Nhấn 'ESC' để hủy >");

        Write(" Nhập thứ tự tệp: ");
        string? newFolder = IOHelper.ReadInput(false);
        if (newFolder == null)
            return;

        if (!Path.Exists(newFolder))
        {
            WriteLine(" Lỗi: Thư mục không tồn tại");
            WriteLine(" Tạo thư mục? (Y/N): ");
            if(IOHelper.ReadConfirm() ?? false)
            {
                Directory.CreateDirectory(newFolder);
                curFolder = newFolder;
                IOHelper.WriteBorder();
                WriteLine(" Đổi vị trí thư mục lưu thành công");
            }
            else
                WriteLine(" Đổi vị trí thư mục lưu không thành công");
            ReadKey(true);
        }
        else
        {
            curFolder = newFolder;
            IOHelper.WriteBorder();
            WriteLine(" Đổi vị trí thư mục lưu thành công");
            ReadKey(true);
        }

    }

    private static readonly StringBuilder inputBuffer = new(MagicNum.inputLimit);
    private static readonly List<Message> msgHistory = [];

    private static bool GetPartnerHistory(NetworkStream stream)
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

    private static bool GetGroupHistory(NetworkStream stream, int groupID)
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

    private static void WritePromt(string prompt)
    {
        IOHelper.WriteBorder();
        Write(prompt);
        Write(inputBuffer);
    }

    private static void WriteMsgHistory()
    {
        Clear();
        msgHistory.ForEach(msg => WriteLine(msg.Print()));
    }

    private static void WriteMessage(string content, string prompt)
    {
        IOHelper.MoveCursor(- prompt.Length - inputBuffer.Length - WindowWidth);
        WriteLine(content.ToString().PadRight(WindowWidth - 1));
        WritePromt(prompt);
    }

    private static void WriteNotice(string content, string prompt)
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
            WriteLine(" Lỗi: Thông tin nhóm/người dùng không hợp lệ");
            return;
        }
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine(" Các lệnh chat:");
        ClientMenu.ChatCommands();
        IOHelper.WriteBorder();

        if (joinedGroup != null && GetGroupHistory(stream, joinedGroup.GroupID))
            WriteLine(" Tải lịch sử tin nhắn nhóm thành công");
        else if (partner != null && GetPartnerHistory(stream))
            WriteLine(" Tải lịch sử tin nhắn thành công");
        else
            WriteLine(" Lỗi: Tải lịch sử tin nhắn không thành công");

        WriteLine(" Nhấn phím bất kỳ để tiếp tục...");
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
                            WriteLine("[Hệ thống] Các lệnh chat:");
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
                                WriteNotice($"[Hệ thống] Thông tin bạn: '{partner.Info(false, true)}'", prompt);
                            }
                            continue;

                        case "/clear": case "/cls":
                            Clear();
                            WritePromt(prompt);
                            continue;

                        case "/reset":
                            WriteMsgHistory();
                            WritePromt(prompt);
                            continue;

                        case "/file":
                            if (chatCmd.Length == spaceIndex)
                                WriteNotice("[Hệ thống] Lỗi: Đường dẫn tệp không hợp lệ", prompt);
                            else
                            {
                                string filePath = chatCmd[(spaceIndex + 1)..].Replace("\"", "");
                                WriteNotice($"[Hệ thống] Bắt đầu gửi tệp {filePath}", prompt);

                                if (SendFile(stream, filePath))
                                    WriteNotice("[Hệ thống] Gửi tệp thành công", prompt);
                                else
                                    WriteNotice("[Hệ thống] Gửi tệp không thành công", prompt);
                            }
                            continue;

                        case "/exit":
                            stopTokenSource.Cancel();
                            stopTokenSource.Dispose();
                            return;
                        
                        default:
                            WriteNotice("[Hệ thống] Lỗi: Lệnh không tồn tại", prompt);
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
            WriteNotice($" Lỗi: {ex}", prompt);
            ClientHelper.SendDisconnect(stream);
        }
    }

    private static bool doneSendingFile = false;
    private static readonly string[] waitting = [
        "Đang gửi tệp.   ",
        "Đang gửi tệp..  ",
        "Đang gửi tệp... ",
        "Đang gửi tệp...."
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
                            WriteNotice("[Hệ thống] Lỗi: Tin nhắn rỗng", prompt);
                        else
                        {
                            WriteMessage(echoMsg.Print(), prompt);
                            lock(msgHistory)
                                msgHistory.Add(echoMsg);
                        }
                        continue;

                    case CommandType.GetGroupInfo:
                        ChatGroup? requestedGroup = ChatGroup.Deserialize(receivedCmd.Payload);
                        WriteNotice($"[Hệ thống] Thông tin nhóm: '{requestedGroup?.Info(true, true)}'", prompt);
                        continue;

                    case CommandType.DoneSendingFile:
                        doneSendingFile = true;
                        continue;

                    case CommandType.SendFile:
                        string? filePath = await ReceiveFile(stream, receivedCmd, savePath, stopToken);
                        if (filePath != null)
                            WriteNotice($"[Hệ thống] Lưu tệp đã nhận tại {Path.GetFileName(filePath)}", prompt);
                        continue;

                    case CommandType.Disconnect:
                        WriteNotice("[Hệ thống] Server ngừng hoạt động", prompt);
                        return;

                    case CommandType.Error:
                        WriteNotice($"[Hệ thống] Lỗi: {receivedCmd.Payload}", prompt);
                        return;

                    default:
                        WriteNotice($"[Hệ thống] Lỗi: Lệnh phản hồi không hợp lệ {receivedCmd?.CommandType}", prompt);
                        return;
                }
            }
        }
        catch(OperationCanceledException) { /* ignored */ }
        catch(IOException) {}
        catch(Exception ex)
        {
            WriteNotice($"[Hệ thống] Lỗi: ({ex.GetType().Name}) {ex.Message}", prompt);
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