using static System.Console;

static class ClientMenu
{
    public static void Connect(string serverIP, int port)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" IP server: {serverIP}");
        WriteLine($" Cổng server: {port}");
        IOHelper.WriteBorder();
        WriteLine(" 1. Kết nối tới server");
        WriteLine(" 2. Đổi IP");
        WriteLine(" 3. Đổi cổng");
        WriteLine(" 4. Quản lý thư mục lưu");
        WriteLine(" 0. Tắt chương trình");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void Welcome()
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine(" 1. Đăng nhập");
        WriteLine(" 2. Đăng ký");
        WriteLine(" 3. Quản lý thư mục lưu");
        WriteLine(" 0. Tắt chương trình");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }
    
    public static void MainUser(string nickname)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine($" Zello, {nickname}!");
        IOHelper.WriteBorder();
        WriteLine(" 1. Đổi nickname");
        WriteLine(" 2. Đổi mật khẩu");
        WriteLine(" 3. Bạn bè");
        WriteLine(" 4. Nhóm chat");
        WriteLine(" 5. Quản lý thư mục lưu");
        WriteLine(" 0. Đăng xuất");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void Friends(List<Friend> friends, int curPage, int receivedRequestCount)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        if (friends.Count == 0)
            WriteLine(" Danh sách bạn bè rỗng");
        else
        {
            WriteLine($" Danh sách bạn bè (Trang {curPage + 1}/{(friends.Count - 1) / 10 + 1}):");
            foreach(var friend in friends.GetRange(curPage * 10, Math.Min(friends.Count - curPage * 10, 10)))
                WriteLine($" • {friend.Info()}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Bắt đầu nhắn tin");
        WriteLine($" 2. Quản lý lời mời kết bạn {(receivedRequestCount > 0 ? $"({receivedRequestCount})" : "")}");
        WriteLine(" 3. Hủy kết bạn");
        WriteLine(" 4. Xem danh sách tất cả người dùng");
        WriteLine(" 7. Làm mới danh sách");
        WriteLine(" 8. Trang trước");
        WriteLine(" 9. Trang sau");
        WriteLine(" 0. Trờ về");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void ReceivedRq(List<User> users, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        if (users.Count == 0)
            WriteLine(" Danh sách lời mời kết bạn rỗng");
        else
        {
            WriteLine($" Có lời mời kết bạn từ (Trang {curPage + 1}/{(users.Count - 1) / 10 + 1}):");
            foreach(var user in users.GetRange(curPage * 10, Math.Min(users.Count - curPage * 10, 10)))
                WriteLine($" • {user.Info(false, true)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Chấp nhận");
        WriteLine(" 2. Chấp nhận tất cả");
        WriteLine(" 3. Từ chối");
        WriteLine(" 4. Từ chối tất cả");
        WriteLine(" 5. Chặn");
        WriteLine(" 6. Chặn tất cả");
        WriteLine(" 7. Làm mới danh sách");
        WriteLine(" 8. Trang trước");
        WriteLine(" 9. Trang sau");
        WriteLine(" 0. Trờ về");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void AllUser(List<User> users, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        if (users.Count == 0)
            WriteLine(" Danh sách người dùng rỗng");
        else
        {
            WriteLine($" Danh sách tất cả người dùng (Trang {curPage + 1}/{(users.Count - 1) / 10 + 1}):");
            foreach(var user in users.GetRange(curPage * 10, Math.Min(users.Count - curPage * 10, 10)))
                WriteLine($" • {user.Info(false, true)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Gừi lời mời kết bạn");
        // WriteLine(" 2. Xem các lời mời đã gửi");
        WriteLine(" 3. Chặn");
        WriteLine(" 7. Làm mới danh sách");
        WriteLine(" 8. Trang trước");
        WriteLine(" 9. Trang sau");
        WriteLine(" 0. Trờ về");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void ChatGroups(List<ChatGroup> subcribed, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        if (subcribed.Count == 0)
            WriteLine(" Danh sách nhóm đã tham gia rỗng");
        else
        {
            WriteLine($" Các nhóm đã tham gia (Trang {curPage + 1}/{(subcribed.Count - 1) / 10 + 1}):");
            foreach(var group in subcribed.GetRange(curPage * 10, Math.Min(subcribed.Count - curPage * 10, 10)))
                WriteLine($" • {group.Info(true, true)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Bắt đầu nhắn tin");
        WriteLine(" 2. Hủy tham gia");
        WriteLine(" 3. Xem danh sách tất cả các nhóm");
        WriteLine(" 4. Quản lý các nhóm đã tạo");
        WriteLine(" 7. Làm mới danh sách");
        WriteLine(" 8. Trang trước");
        WriteLine(" 9. Trang sau");
        WriteLine(" 0. Trờ về");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void AllGroups(List<ChatGroup> groups, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        if (groups.Count == 0)
            WriteLine(" Danh sách nhóm rỗng");
        else
        {
            WriteLine($" Danh sách tất cả các nhóm (Trang {curPage + 1}/{(groups.Count - 1) / 10 + 1}):");
            foreach(var group in groups.GetRange(curPage * 10, Math.Min(groups.Count - curPage * 10, 10)))
                WriteLine($" • {group.Info(true, false)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Tham gia");
        WriteLine(" 7. Làm mới danh sách");
        WriteLine(" 8. Trang trước");
        WriteLine(" 9. Trang sau");
        WriteLine(" 0. Trờ về");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void ManageCreated(List<ChatGroup> groups, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        if (groups.Count == 0)
            WriteLine(" Danh sách các nhóm đã tạo rỗng");
        else
        {
            WriteLine($" Các nhóm đã tạo (Trang {curPage + 1}/{(groups.Count - 1) / 10 + 1}):");
            foreach(var group in groups.GetRange(curPage * 10, Math.Min(groups.Count - curPage * 10, 10)))
                WriteLine($" • {group.Info(true, false)}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Tạo nhóm mới");
        WriteLine(" 2. Đổi tên nhóm");
        WriteLine(" 3. Xóa nhóm");
        WriteLine(" 8. Trang trước");
        WriteLine(" 9. Trang sau");
        WriteLine(" 0. Trờ về");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void File(List<string> files, int curPage)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        if (files.Count == 0)
            WriteLine(" Thư mục rỗng");
        else
        {
            WriteLine($" Danh sách tệp (Trang {curPage + 1}/{(files.Count - 1) / 10 + 1}):");
            for (int i = 0; i < Math.Min(files.Count - curPage * 10, 10); i++)
                WriteLine($" {i}. {Path.GetFileName(files[i])}");
        }

        IOHelper.WriteBorder();
        WriteLine(" 1. Mở");
        WriteLine(" 2. Xóa");
        WriteLine(" 3. Đổi tên tệp");
        WriteLine(" 4. Sao chép đường dẫn tệp");
        WriteLine(" 5. Đổi vị trí thư mục");
        WriteLine(" 8. Trang trước");
        WriteLine(" 9. Trang sau");
        WriteLine(" 0. Trờ về");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void FileFolder(string curPath)
    {
        Clear();
        IOHelper.WriteHeader("Zelo");
        WriteLine(" Vị trí thư mục lưu hiện tại:");
        WriteLine($" {curPath}");
        IOHelper.WriteBorder();
        WriteLine(" 1. Đổi vị trí");
        WriteLine(" 2. Khôi phục vị trí mặc định");
        WriteLine(" 0. Trờ về");
        IOHelper.WriteBorder();
        Write(" Chọn mục: ");
    }

    public static void ChatCommands()
    {
        WriteLine(" /help             -- Hiện tất cả các lệnh");
        WriteLine(" /info             -- Hiện thông tin nhóm");
        WriteLine(" /clear /cls       -- Làm sạch console");
        WriteLine(" /reset            -- Làm sạch console và hiện thị lại tin nhắn");
        WriteLine(" /file (filePath)  -- Gửi file theo đường dẫn tới nhóm");
        WriteLine(" /exit             -- Rời nhóm");
        WriteLine(" Cũng có thể rời nhóm bằng phím 'ESC'");
    }
}