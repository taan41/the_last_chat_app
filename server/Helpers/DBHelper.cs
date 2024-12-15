using System.Data;
using System.Net;
using System.Text;
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
                CreatedTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )", conn))
        {
            createUsers.ExecuteNonQuery();
        }

        using (MySqlCommand createChatGroups = new(@$"
            CREATE TABLE ChatGroups (
                GroupID INT AUTO_INCREMENT PRIMARY KEY,
                GroupName NVARCHAR({MagicNumbers.groupNameMax}) NOT NULL,
                CreatorID INT DEFAULT NULL,
                CreatedTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                OnlineCount INT NOT NULL,
                FOREIGN KEY (CreatorID) REFERENCES Users(UserID) ON DELETE SET NULL
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
                Content TEXT CHARACTER SET utf8mb4 NOT NULL,
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

    public static async Task<(bool success, string errorMessage)> CheckUsername(string username)
    {
        string query = "SELECT * FROM Users WHERE Username = @username";

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", username);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!reader.HasRows) // No same username found
                return (true, "");
            else
                return (false, "Unavailable username");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(bool success, string errorMessage)> AddUser(User user)
    {
        string query = "INSERT INTO Users (Username, Nickname, PasswordHash, Salt) VALUES (@username, @nickname, @pwdHash, @salt)";

        if (user.PwdSet == null)
            return (false, "Null user password");

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", user.Username);
            cmd.Parameters.AddWithValue("@nickname", user.Nickname);
            cmd.Parameters.AddWithValue("@pwdHash", user.PwdSet.PwdHash);
            cmd.Parameters.AddWithValue("@salt", user.PwdSet.Salt);

            await cmd.ExecuteNonQueryAsync();

            return (true, "");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(User? requestedUser, string errorMessage)> GetUser(string username, bool getPwd)
    {
        string query = "SELECT UserID, Username, Nickname, PasswordHash, Salt FROM Users WHERE Username=@username";

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@username", username);

            using var reader = await cmd.ExecuteReaderAsync();

            if (! await reader.ReadAsync()) // User not found
                return (null, $"No user with username '{username}' found");

            PasswordSet? pwdSet = null;

            if (getPwd)
            {
                byte[] pwdHash = new byte[MagicNumbers.pwdHashLen];
                byte[] salt = new byte[MagicNumbers.pwdSaltLen];

                reader.GetBytes("PasswordHash", 0, pwdHash, 0, MagicNumbers.pwdHashLen);
                reader.GetBytes("Salt", 0, salt, 0, MagicNumbers.pwdSaltLen);

                pwdSet = new(pwdHash, salt);
            }
            
            return (new()
            {
                UID = reader.GetInt32("UserID"),
                Username = reader.GetString("Username"),
                Nickname = reader.GetString("Nickname"),
                PwdSet = pwdSet
            }, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }
    
    public static async Task<(User? requestedUser, string errorMessage)> GetUser(int userID, bool getPwd)
    {
        string query = "SELECT UserID, Username, Nickname, PasswordHash, Salt FROM Users WHERE UserID=@userID";

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@userID", userID);

            using var reader = await cmd.ExecuteReaderAsync();

            if (! await reader.ReadAsync()) // User not found
                return (null, $"No user with ID '{userID}' found");

            PasswordSet? pwdSet = null;

            if (getPwd)
            {
                byte[] pwdHash = new byte[MagicNumbers.pwdHashLen];
                byte[] salt = new byte[MagicNumbers.pwdSaltLen];

                reader.GetBytes("PasswordHash", 0, pwdHash, 0, MagicNumbers.pwdHashLen);
                reader.GetBytes("Salt", 0, salt, 0, MagicNumbers.pwdSaltLen);

                pwdSet = new(pwdHash, salt);
            }
            
            return (new()
            {
                UID = reader.GetInt32("UserID"),
                Username = reader.GetString("Username"),
                Nickname = reader.GetString("Nickname"),
                PwdSet = pwdSet
            }, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }
    
    public static async Task<(List<User>? users, string errorMessage)> GetAllUser(bool getPwd)
    {
        string query = getPwd ?
            "SELECT UserID, Username, Nickname, PasswordHash, Salt FROM Users ORDER BY UserID" :
            "SELECT UserID, Username, Nickname FROM Users ORDER BY UserID";

        List<User> users = [];
        
        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                PasswordSet? pwdSet = null;

                if (getPwd)
                {
                    byte[] pwdHash = new byte[MagicNumbers.pwdHashLen];
                    byte[] salt = new byte[MagicNumbers.pwdSaltLen];

                    reader.GetBytes("PasswordHash", 0, pwdHash, 0, MagicNumbers.pwdHashLen);
                    reader.GetBytes("Salt", 0, salt, 0, MagicNumbers.pwdSaltLen);

                    pwdSet = new(pwdHash, salt);
                }
                
                users.Add(new()
                {
                    UID = reader.GetInt32("UserID"),
                    Username = reader.GetString("Username"),
                    Nickname = reader.GetString("Nickname"),
                    PwdSet = pwdSet
                });
            }

            return (users, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }

    public static async Task<(bool success, string errorMessage)> UpdateUser(User userToUpdate, string? newNickname, PasswordSet? newPwd)
    {
        if (newNickname == null && newPwd == null)
            return (false, "Invalid updated data");
            
        if (userToUpdate.UID == -1 || userToUpdate.PwdSet == null)
            return (false, "Null UID/Password Set");

        StringBuilder query = new("UPDATE Users SET");

        if (newNickname != null)
            query.Append(" Nickname = @newNickname");

        if (newPwd != null)
        {
            if (newNickname != null)
                query.Append(',');
            query.Append(" PasswordHash = @newPwdHash, Salt = @newSalt");
        }

        query.Append(" WHERE UserID = @userID");

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query.ToString(), conn);
            cmd.Parameters.AddWithValue("@userID", userToUpdate.UID);

            if (newNickname != null)
                cmd.Parameters.AddWithValue("@newNickname", newNickname);

            if (newPwd != null)
            {
                cmd.Parameters.AddWithValue("@newPwdHash", newPwd.PwdHash);
                cmd.Parameters.AddWithValue("@newSalt", newPwd.Salt);
            }

            await cmd.ExecuteNonQueryAsync();

            return (true, "");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(bool success, string errorMessage)> AddChatGroup(ChatGroup chatGroup)
    {
        string query = "INSERT INTO ChatGroups (GroupName, CreatorID, OnlineCount) VALUES (@groupName, @creatorID, @onlineCount)";

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@groupName", chatGroup.GroupName);
            cmd.Parameters.AddWithValue("@creatorID", chatGroup.CreatorID);
            cmd.Parameters.AddWithValue("@onlineCount", chatGroup.OnlineCount);

            await cmd.ExecuteNonQueryAsync();

            return (true, "");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(ChatGroup? requestedGroup, string errorMessage)> GetChatGroup(int groupID)
    {
        string query = "SELECT GroupName, CreatorID, CreatedTime, OnlineCount FROM ChatGroups WHERE GroupID = @groupID";

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@groupID", groupID);

            using var reader = await cmd.ExecuteReaderAsync();

            if (! await reader.ReadAsync()) // Chat group not found
                return (null, $"No chat group with ID '{groupID}' found");

            return (new()
            {
                GroupID = groupID,
                GroupName = reader.GetString("GroupName"),
                CreatorID = reader.GetInt32("CreatorID"),
                CreatedTime = reader.GetDateTime("CreatedTime"),
                OnlineCount = reader.GetInt32("OnlineCount")
            }, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }
    
    public static async Task<(List<ChatGroup>? groups, string errorMessage)> GetAllChatGroup()
    {
        string query = "SELECT GroupID, GroupName, CreatorID, CreatedTime, OnlineCount FROM ChatGroups ORDER BY GroupID";

        List<ChatGroup> groups = [];
        
        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                groups.Add(new()
                {
                    GroupName = reader.GetString("GroupName"),
                    GroupID = reader.GetInt32("GroupID"),
                    CreatorID = reader.IsDBNull(reader.GetOrdinal("CreatorID")) ? null : reader.GetInt32("CreatorID"),
                    CreatedTime = reader.GetDateTime("CreatedTime"),
                    OnlineCount = reader.GetInt32("OnlineCount")
                });
            }

            return (groups, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }
    
    public static async Task<(List<ChatGroup>? groups, string errorMessage)> GetChatGroupByCreator(int creatorID)
    {
        string query = "SELECT GroupID, GroupName, CreatedTime, OnlineCount FROM ChatGroups WHERE CreatorID = @creatorID ORDER BY GroupID";

        List<ChatGroup> groups = [];
        
        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@creatorID", creatorID);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                groups.Add(new(reader.GetString("GroupName"), creatorID)
                {
                    GroupID = reader.GetInt32("GroupID"),
                    CreatedTime = reader.GetDateTime("CreatedTime"),
                    OnlineCount = reader.GetInt32("OnlineCount")
                });
            }

            return (groups, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }

    public static async Task<(bool success, string errorMessage)> UpdateChatGroup(ChatGroup updatedGroup, bool updateOnlCount)
    {
        if (updatedGroup.GroupID < 1)
            return (false, "Invalid GroupID");

        string query = updateOnlCount ?
            "UPDATE ChatGroups SET GroupName = @newGroupName, OnlineCount = @onlineCount WHERE GroupID = @groupID" :
            "UPDATE ChatGroups SET GroupName = @newGroupName WHERE GroupID = @groupID";

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@newGroupName", updatedGroup.GroupName);
            cmd.Parameters.AddWithValue("@groupID", updatedGroup.GroupID);
            if(updateOnlCount) cmd.Parameters.AddWithValue("@onlineCount", updatedGroup.OnlineCount);

            await cmd.ExecuteNonQueryAsync();

            return (true, "");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }

    }

    public static async Task<(bool success, string errorMessage)> DeleteChatGroup(int groupID)
    {
        string query = "DELETE FROM ChatGroups WHERE GroupID = @groupID";

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@groupID", groupID);

            await cmd.ExecuteNonQueryAsync();

            return (true, "");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(bool success, string errorMessage)> SaveMessage(Message message)
    {
        string query = "INSERT INTO Messages (SenderID, ReceiverID, GroupID, Content) VALUES (@senderID, @receiverID, @groupID, @message)";

        try
        {    
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@senderID", message.SenderID);
            cmd.Parameters.AddWithValue("@receiverID", message.ReceiverID);
            cmd.Parameters.AddWithValue("@groupID", message.GroupID);
            cmd.Parameters.AddWithValue("@message", message.Content);

            await cmd.ExecuteNonQueryAsync();
            return (true, "");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }
    }

    public static async Task<(List<Message>? requestedMsgList, string errorMessage)> GetPrivateMessageHistory(int senderID, int receiverID)
    {
        string query =
            @"SELECT
                m.SentTime,
                u.Nickname,
                m.Content
            FROM Messages m
            JOIN Users u ON m.SenderID = u.UserID
            WHERE (m.SenderID = @senderID AND m.ReceiverID = @receiverID)
            OR (m.SenderID = @receiverID AND m.ReceiverID = @senderID)
            ORDER BY m.SentTime";

        List<Message> messages = [];
        
        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@senderID", senderID);
            cmd.Parameters.AddWithValue("@receiverID", receiverID);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime timestamp = reader.GetDateTime("SentTime");
                string nickname = reader.GetString("Nickname");
                string message = reader.GetString("Content");

                messages.Add(new(timestamp, nickname, message));
            }

            return (messages, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }

    public static async Task<(List<Message>? requestedMsgList, string errorMessage)> GetGroupHistory(int groupID)
    {
        string query = 
            @"SELECT
                m.SentTime,
                u.Nickname,
                m.Content
            FROM Messages m
            JOIN Users u ON m.SenderId = u.UserId
            WHERE m.GroupID = @groupID
            ORDER BY m.SentTime";

        List<Message> messages = [];

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@groupID", groupID);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                DateTime timestamp = reader.GetDateTime("SentTime");
                string nickname = reader.GetString("Nickname");
                string message = reader.GetString("Content");

                messages.Add(new(timestamp, nickname, message));
            }

            return (messages, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }

    public static async Task<(bool success, string errorMessage)> AddLog(string? source, string logContent)
    {
        string query = "INSERT INTO ActivityLog (Source, Content) VALUES (@source, @content)";

        try
        {    
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            cmd.Parameters.AddWithValue("@source", source ?? "null");
            cmd.Parameters.AddWithValue("@content", logContent);

            await cmd.ExecuteNonQueryAsync();
            return (true, "");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }
    }
    
    public static async Task<(List<Log>? requestedLogList, string errorMessage)> GetLogHistory()
    {
        string query = "SELECT LogTime, Content FROM ActivityLog ORDER BY LogTime";

        List<Log> logList = [];
        
        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand cmd = new(query, conn);
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                logList.Add(new(reader.GetDateTime("LogTime"), reader.GetString("Source"), reader.GetString("Content")));
            }

            return (logList, "");
        }
        catch (MySqlException ex)
        {
            return (null, ex.Message);
        }
    }

    public static async Task<(bool success, string errorMessage)> ClearLog()
    {
        string delQuery = "DELETE FROM ActivityLog";
        string resetIncrementQuery = "ALTER TABLE ActivityLog AUTO_INCREMENT = 1";

        try
        {
            using MySqlConnection conn = new(connectionString);
            await conn.OpenAsync();

            using MySqlCommand delCmd = new(delQuery, conn);
            await delCmd.ExecuteNonQueryAsync();

            using MySqlCommand resetIncrementCmd = new(resetIncrementQuery, conn);
            await resetIncrementCmd.ExecuteNonQueryAsync();

            return (true, "");
        }
        catch (MySqlException ex)
        {
            return (false, ex.Message);
        }
    }
}