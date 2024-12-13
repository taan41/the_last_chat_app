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
                Username NVARCHAR({MagicNumbers.usernameMax}) NOT NULL UNIQUE,
                Nickname NVARCHAR({MagicNumbers.nicknameMax}) NOT NULL,
                PasswordHash VARBINARY({MagicNumbers.pwdHashLen}) DEFAULT NULL,
                Salt VARBINARY({MagicNumbers.pwdSaltLen}) DEFAULT NULL,
                CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )", conn))
        {
            createUsers.ExecuteNonQuery();
        }

        using (MySqlCommand createChatGroups = new(@$"
            CREATE TABLE ChatGroups (
                GroupID INT AUTO_INCREMENT PRIMARY KEY,
                GroupName NVARCHAR({MagicNumbers.groupNameMax}) NOT NULL,
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

    public static bool CheckUsername(string username, out string errorMessage)
    {
        string query = "SELECT * FROM Users WHERE Username = @username";
        errorMessage = "";

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", username);

            using MySqlDataReader reader = cmd.ExecuteReader();
            if(!reader.Read())
                return true;
            else
                return false;
        }
        catch(MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool Register(User user, out string errorMessage)
    {
        string query = "INSERT INTO Users (Username, Nickname, PasswordHash, Salt) VALUES (@username, @nickname, @pwdHash, @salt)";
        errorMessage = "";

        if(user.PwdSet == null)
        {
            errorMessage = "Invalid user password";
            return false;
        }

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", user.Username);
            cmd.Parameters.AddWithValue("@nickname", user.Nickname);
            cmd.Parameters.AddWithValue("@pwdHash", user.PwdSet.PwdHash);
            cmd.Parameters.AddWithValue("@salt", user.PwdSet.Salt);

            cmd.ExecuteNonQuery();

            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static PasswordSet? GetUserPwd(string username, out string errorMessage)
    {
        string query = "SELECT PasswordHash, Salt FROM Users WHERE Username = @username";
        errorMessage = "";

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", username);

            using MySqlDataReader reader = cmd.ExecuteReader();

            if(!reader.Read()) // Username not found
                return null;

            byte[] pwdHash = new byte[MagicNumbers.pwdHashLen];
            byte[] salt = new byte[MagicNumbers.pwdSaltLen];

            reader.GetBytes("PasswordHash", 0, pwdHash, 0, MagicNumbers.pwdHashLen);
            reader.GetBytes("Salt", 0, salt, 0, MagicNumbers.pwdSaltLen);

            return new(pwdHash, salt);
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return null;
        }
    }

    public static User? Login(string username, out string errorMessage)
    {
        string query = "SELECT UserID, Username, Nickname FROM Users WHERE Username=@username";
        errorMessage = "";

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", username);

            using MySqlDataReader reader = cmd.ExecuteReader();

            if(!reader.Read()) // User not found
                return null;

            return new(reader.GetInt32("UserID"), reader.GetString("Username"), reader.GetString("Nickname"), null);
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return null;
        }
    }

    public static bool SetNickname(User user, string newNickname, out string errorMessage)
    {
        string query = "UPDATE Users SET Nickname = @newNickname WHERE UserID = @userID";
        errorMessage = "";

        if(user.UID == null)
        {
            errorMessage = "Null UID";
            return false;
        }

        try
        {    
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@newNickname", newNickname);
            cmd.Parameters.AddWithValue("@userID", user.UID);

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
        string query = "SELECT LogTime, Content FROM ActivityLog ORDER BY LogTime";
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

            return logList;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return null;
        }
    }

    public static bool ClearLog(out string errorMessage)
    {
        string delQuery = "DELETE FROM ActivityLog";
        string resetIncrementQuery = "ALTER TABLE ActivityLog AUTO_INCREMENT = 1";
        errorMessage = "";

        try
        {
            using MySqlConnection conn = new(connectionString);
            conn.Open();

            using MySqlCommand delCmd = new(delQuery, conn);
            delCmd.ExecuteNonQuery();

            using MySqlCommand resetIncrementCmd = new(resetIncrementQuery, conn);
            resetIncrementCmd.ExecuteNonQuery();

            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}