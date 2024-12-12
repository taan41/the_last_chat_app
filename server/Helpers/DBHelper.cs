using System.Data;
using MySql.Data.MySqlClient;

static class DbHelper
{
    private static string connectionString = "";

    public static bool InitMySql(string server, string db, string uid, string password, out string errorMessage)
    {
        connectionString = $"server={server};uid={uid};pwd={password};";
        errorMessage = "";

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new($"SHOW DATABASES LIKE '{db}';", conn);

            using MySqlDataReader reader = cmd.ExecuteReader();
            if (!reader.HasRows) // Database does not exist
            {
                reader.Close();
                CreateDB(conn, db);
            }

            connectionString += $"database={db};";
            return true;
        }
        catch(MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static void CreateDB(MySqlConnection conn, string dbName)
    {
        using (MySqlCommand createDB = new($"CREATE DATABASE {dbName};", conn))
        {
            createDB.ExecuteNonQuery();
        }

        conn.ChangeDatabase(dbName);

        using (MySqlCommand createUsers = new(@$"
            CREATE TABLE Users (
                UserID INT AUTO_INCREMENT PRIMARY KEY,
                Username NVARCHAR({MagicNumbers.usernameLimit}) NOT NULL UNIQUE,
                PasswordHash VARBINARY({MagicNumbers.pwdHashLen}) DEFAULT NULL,
                Salt VARBINARY({MagicNumbers.pwdSaltLen}) DEFAULT NULL,
                Nickname NVARCHAR({MagicNumbers.nicknameLimit}) NOT NULL,
                CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )", conn))
        {
            createUsers.ExecuteNonQuery();
        }

        using (MySqlCommand createChatGroups = new(@$"
            CREATE TABLE ChatGroups (
                GroupID INT AUTO_INCREMENT PRIMARY KEY,
                GroupName NVARCHAR({MagicNumbers.groupNameLimit}) NOT NULL,
                CreatedBy INT NOT NULL,
                CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )", conn))
        {
            createChatGroups.ExecuteNonQuery();
        }

        using (MySqlCommand createMessages = new(@"
            CREATE TABLE Messages (
                MessageID INT AUTO_INCREMENT PRIMARY KEY,
                SentTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                SenderID INT NOT NULL,
                ReceiverID INT DEFAULT NULL,
                GroupID INT DEFAULT NULL,
                MessageText TEXT CHARACTER SET utf8mb4 NOT NULL,
                FOREIGN KEY (SenderID) REFERENCES Users(UserID) ON DELETE CASCADE,
                FOREIGN KEY (ReceiverID) REFERENCES Users(UserID) ON DELETE CASCADE,
                FOREIGN KEY (GroupID) REFERENCES ChatGroups(GroupID) ON DELETE CASCADE
            )", conn))
        {
            createMessages.ExecuteNonQuery();
        }

        using (MySqlCommand createLog = new(@"
            CREATE TABLE ActivityLog (
                LogIndex INT AUTO_INCREMENT PRIMARY KEY,
                LogTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                Content TEXT CHARACTER SET utf8mb4 NOT NULL
            )", conn))
        {
            createLog.ExecuteNonQuery();
        }
    }

    public static bool Register(string username, string password, string nickname, out string errorMessage)
    {
        string query = "INSERT INTO Users (Username, PasswordHash, Salt, Nickname) VALUES (@username, @passwordHash, @salt, @nickname)";
        errorMessage = "";

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@nickname", nickname);

            (byte[] hash, byte[] salt) = Utilities.HashPassword(password);
            cmd.Parameters.AddWithValue("@passwordHash", hash);
            cmd.Parameters.AddWithValue("@salt", salt);

            cmd.ExecuteNonQuery();

            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static User? Login(string username, string password, out string errorMessage)
    {
        string query = "SELECT PasswordHash, Salt, UserID, Username, Nickname FROM Users WHERE Username=@username";
        errorMessage = "";

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", username);

            using MySqlDataReader reader = cmd.ExecuteReader();

            if(!reader.Read())
            {
                errorMessage = "User not found";
                return null;
            }

            byte[] passwordHash = new byte[MagicNumbers.pwdHashLen];
            byte[] salt = new byte[MagicNumbers.pwdSaltLen];
            reader.GetBytes("PasswordHash", 0, passwordHash, 0, MagicNumbers.pwdHashLen);
            reader.GetBytes("Salt", 0, salt, 0, MagicNumbers.pwdSaltLen);

            if(Utilities.VerifyPassword(password, passwordHash, salt))
                return new(reader.GetInt32("UserID"), reader.GetString("Username"), reader.GetString("Nickname"));
            else
            {
                errorMessage = "Wrong password";
                return null;
            }
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return null;
        }
    }

    public static bool SetNickname(int userID, string newNickname, out string errorMessage)
    {
        string query = "UPDATE Users SET Nickname = @newNickname WHERE UserID = @userID";
        errorMessage = "";

        try
        {    
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@newNickname", newNickname);
            cmd.Parameters.AddWithValue("@userID", userID);

            cmd.ExecuteNonQuery();
            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool SavePrivateMessage(Message message, out string errorMessage)
    {
        string query = "INSERT INTO Messages (SenderID, ReceiverID, MessageText) VALUES (@senderID, @receiverID, @message)";
        errorMessage = "";

        try
        {    
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@senderID", message.SenderID);
            cmd.Parameters.AddWithValue("@receiverID", message.ReceiverID);
            cmd.Parameters.AddWithValue("@message", message.Content);

            cmd.ExecuteNonQuery();
            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool SaveGroupMessage(Message message, out string errorMessage)
    {
        string query = "INSERT INTO Messages (SenderID, GroupID, MessageText) VALUES (@senderID, @groupID, @message)";
        errorMessage = "";

        try
        {    
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@senderID", message.SenderID);
            cmd.Parameters.AddWithValue("@groupID", message.GroupID);
            cmd.Parameters.AddWithValue("@message", message.Content);

            cmd.ExecuteNonQuery();
            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static List<Message>? GetPrivateMessageHistory(int senderID, int receiverID, out string errorMessage)
    {
        string query =
            @"SELECT
                m.SentTime,
                u.Nickname,
                m.MessageText
            FROM Messages m
            JOIN Users u ON m.SenderID = u.UserID
            WHERE (m.SenderID = @senderID AND m.ReceiverID = @receiverID)
            OR (m.SenderID = @receiverID AND m.ReceiverID = @senderID)
            ORDER BY m.SentTime";
        errorMessage = "";

        List<Message> messages = [];
        
        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@senderID", senderID);
            cmd.Parameters.AddWithValue("@receiverID", receiverID);
            
            using MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                DateTime timestamp = reader.GetDateTime("Timestamp");
                string nickname = reader.GetString("Nickname");
                string message = reader.GetString("MessageText");

                messages.Add(new(timestamp, nickname, message));
            }
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return null;
        }

        return messages;
    }

    public static List<Message>? GetGroupMessageHistory(int groupID, out string errorMessage)
    {
        string query = 
            @"SELECT
                m.SentTime,
                u.Nickname,
                m.MessageText
            FROM Messages m
            JOIN Users u ON m.SenderId = u.UserId
            WHERE m.GroupID = @groupID
            ORDER BY m.SentTime";
        errorMessage = "";

        List<Message> messages = [];

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@groupID", groupID);
            
            using MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                DateTime timestamp = reader.GetDateTime("Timestamp");
                string nickname = reader.GetString("Nickname");
                string message = reader.GetString("MessageText");

                messages.Add(new(timestamp, nickname, message));
            }
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return null;
        }

        return messages;
    }

    public static bool AddLog(string logContent, out string errorMessage)
    {
        string query = "INSERT INTO ActivityLog (Content) VALUES (@content)";
        errorMessage = "";

        try
        {    
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@content", logContent);

            cmd.ExecuteNonQuery();
            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
    
    public static List<Log>? GetLogHistory(out string errorMessage)
    {
        string query = @"SELECT LogTime, Content From ActivityLog ORDER BY LogTime";
        errorMessage = "";

        List<Log> logList = [];
        
        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            
            using MySqlDataReader reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                DateTime timestamp = reader.GetDateTime("LogTime");
                string content = reader.GetString("Content");

                logList.Add(new(timestamp, content));
            }
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return null;
        }

        return logList;
    }
}