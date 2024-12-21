using System.Data;
using System.Text;
using MySql.Data.MySqlClient;

enum FriendStatus
{
    Pending, Confirmed, Blocked
}

static class FriendStatusOld
{
    public const string
        pending = "Pending",
        confirmed = "Confirmed",
        blocked = "Blocked";
}

static class DBHelper
{
    private static string connectionString = "";

    public class Initialize
    {
        public static bool Start(string server, string db, string uid, string password, out string errorMessage)
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
            using (MySqlCommand createDB = new(@$"
                CREATE DATABASE {dbName}
                CHARACTER SET = utf8mb4
                COLLATE = utf8mb4_unicode_ci;
                ", conn))
            {
                createDB.ExecuteNonQuery();
            }

            conn.ChangeDatabase(dbName);

            using (MySqlCommand createUsers = new(@$"
                CREATE TABLE Users (
                    UserID INT AUTO_INCREMENT PRIMARY KEY,
                    Username VARCHAR({MagicNum.usernameMax}) NOT NULL UNIQUE,
                    Nickname VARCHAR({MagicNum.nicknameMax}) NOT NULL,
                    PasswordHash VARBINARY({MagicNum.pwdHashLen}) DEFAULT NULL,
                    Salt VARBINARY({MagicNum.pwdSaltLen}) DEFAULT NULL,
                    OnlineStatus BOOLEAN DEFAULT FALSE,
                    CreatedTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    INDEX idx_userID (UserID),
                    INDEX idx_username (Username)
                )", conn))
            {
                createUsers.ExecuteNonQuery();
            }

            using (MySqlCommand createFriends = new(@"
                CREATE TABLE Friends (
                    FriendID INT AUTO_INCREMENT PRIMARY KEY,
                    SenderID INT NOT NULL,
                    ReceiverID INT NOT NULL,
                    FriendStatus VARCHAR(10) NOT NULL,
                    FOREIGN KEY (SenderID) REFERENCES Users(UserID) ON DELETE CASCADE,
                    FOREIGN KEY (ReceiverID) REFERENCES Users(UserID) ON DELETE CASCADE,
                    UNIQUE (SenderID, ReceiverID),
                    INDEX idx_sender (SenderID),
                    INDEX idx_receiver (ReceiverID)
                )", conn))
            {
                createFriends.ExecuteNonQuery();
            }

            using (MySqlCommand createChatGroups = new(@$"
                CREATE TABLE ChatGroups (
                    GroupID INT AUTO_INCREMENT PRIMARY KEY,
                    GroupName VARCHAR({MagicNum.groupNameMax}) NOT NULL,
                    CreatorID INT DEFAULT NULL,
                    CreatedTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    OnlineCount INT NOT NULL,
                    FOREIGN KEY (CreatorID) REFERENCES Users(UserID) ON DELETE SET NULL,
                    INDEX idx_groupID (GroupID)
                )", conn))
            {
                createChatGroups.ExecuteNonQuery();
            }

            using (MySqlCommand createGroupMembers = new(@"
                CREATE TABLE GroupMembers (
                    GroupMemberID INT AUTO_INCREMENT PRIMARY KEY,
                    GroupID INT NOT NULL,
                    MemberID INT NOT NULL,
                    JoinedTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY (GroupID) REFERENCES ChatGroups(GroupID) ON DELETE CASCADE,
                    FOREIGN KEY (MemberID) REFERENCES Users(UserID) ON DELETE CASCADE,
                    UNIQUE (GroupID, MemberID),
                    INDEX idx_group (GroupID),
                    INDEX idx_member (MemberID)
                )", conn))
            {
                createGroupMembers.ExecuteNonQuery();
            }

            using (MySqlCommand createPrivateMsg = new(@"
                CREATE TABLE PrivateMessages (
                    MessageID INT AUTO_INCREMENT PRIMARY KEY,
                    SentTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    SenderID INT NOT NULL,
                    ReceiverID INT NOT NULL,
                    Content TEXT NOT NULL,
                    ReadStatus BOOLEAN DEFAULT FALSE,
                    FOREIGN KEY (SenderID) REFERENCES Users(UserID) ON DELETE CASCADE,
                    FOREIGN KEY (ReceiverID) REFERENCES Users(UserID) ON DELETE CASCADE,
                    INDEX idx_sender (SenderID),
                    INDEX idx_receiver (ReceiverID)
                )", conn))
            {
                createPrivateMsg.ExecuteNonQuery();
            }

            using (MySqlCommand createGroupMsg = new(@"
                CREATE TABLE GroupMessages (
                    MessageID INT AUTO_INCREMENT PRIMARY KEY,
                    SentTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    SenderID INT NOT NULL,
                    GroupID INT NOT NULL,
                    Content TEXT NOT NULL,
                    FOREIGN KEY (SenderID) REFERENCES Users(UserID) ON DELETE CASCADE,
                    FOREIGN KEY (GroupID) REFERENCES ChatGroups(GroupID) ON DELETE CASCADE,
                    INDEX idx_sender (SenderID),
                    INDEX idx_group (GroupID)
                )", conn))
            {
                createGroupMsg.ExecuteNonQuery();
            }

            using (MySqlCommand createLog = new(@"
                CREATE TABLE ActivityLog (
                    LogIndex INT AUTO_INCREMENT PRIMARY KEY,
                    LogTime TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    Source VARCHAR(255) NOT NULL,
                    Content TEXT NOT NULL
                )", conn))
            {
                createLog.ExecuteNonQuery();
            }
        }

    }

    public class UserDB
    {
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

        public static async Task<(bool success, string errorMessage)> Add(User userToAdd)
        {
            string query = "INSERT INTO Users (Username, Nickname, PasswordHash, Salt, OnlineStatus) VALUES (@username, @nickname, @pwdHash, @salt, @onlineStatus)";

            if (userToAdd.PwdSet == null)
                return (false, "Null user password");

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@username", userToAdd.Username);
                cmd.Parameters.AddWithValue("@nickname", userToAdd.Nickname);
                cmd.Parameters.AddWithValue("@pwdHash", userToAdd.PwdSet.PwdHash);
                cmd.Parameters.AddWithValue("@salt", userToAdd.PwdSet.Salt);

                await cmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(User? requestedUser, string errorMessage)> Get(string username, bool getPwd)
        {
            string query = "SELECT UserID, Username, Nickname, PasswordHash, Salt, OnlineStatus FROM Users WHERE Username = @username";

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
                    byte[] pwdHash = new byte[MagicNum.pwdHashLen];
                    byte[] salt = new byte[MagicNum.pwdSaltLen];

                    reader.GetBytes("PasswordHash", 0, pwdHash, 0, MagicNum.pwdHashLen);
                    reader.GetBytes("Salt", 0, salt, 0, MagicNum.pwdSaltLen);

                    pwdSet = new(pwdHash, salt);
                }
                
                return (new()
                {
                    UserID = reader.GetInt32("UserID"),
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
        
        public static async Task<(User? requestedUser, string errorMessage)> Get(int userID, bool getPwd)
        {
            string query = getPwd ?
                "SELECT UserID, Username, Nickname, PasswordHash, Salt, OnlineStatus FROM Users WHERE UserID = @userID" :
                "SELECT UserID, Username, Nickname, OnlineStatus FROM Users WHERE UserID = @userID";

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
                    byte[] pwdHash = new byte[MagicNum.pwdHashLen];
                    byte[] salt = new byte[MagicNum.pwdSaltLen];

                    reader.GetBytes("PasswordHash", 0, pwdHash, 0, MagicNum.pwdHashLen);
                    reader.GetBytes("Salt", 0, salt, 0, MagicNum.pwdSaltLen);

                    pwdSet = new(pwdHash, salt);
                }
                
                return (new()
                {
                    UserID = reader.GetInt32("UserID"),
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
        
        public static async Task<(List<User>? users, string errorMessage)> GetAll(bool getPwd)
        {
            string query = getPwd ?
                "SELECT UserID, Username, Nickname, PasswordHash, Salt, OnlineStatus FROM Users ORDER BY UserID" :
                "SELECT UserID, Username, Nickname, OnlineStatus FROM Users ORDER BY UserID";

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
                        byte[] pwdHash = new byte[MagicNum.pwdHashLen];
                        byte[] salt = new byte[MagicNum.pwdSaltLen];

                        reader.GetBytes("PasswordHash", 0, pwdHash, 0, MagicNum.pwdHashLen);
                        reader.GetBytes("Salt", 0, salt, 0, MagicNum.pwdSaltLen);

                        pwdSet = new(pwdHash, salt);
                    }
                    
                    users.Add(new()
                    {
                        UserID = reader.GetInt32("UserID"),
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

        public static async Task<(bool success, string errorMessage)> Update(int userID, string? newNickname, bool? onlineStatus, PasswordSet? newPwd)
        {
            if (newNickname == null && onlineStatus == null && newPwd == null)
                return (false, "Invalid updated data");
                
            if (userID < 1)
                return (false, "Invalid user ID");

            List<string> querySets = [];
            if (newNickname != null) querySets.Add(" Nickname = @newNickname");
            if (onlineStatus != null) querySets.Add(" OnlineStatus = @onlineStatus");
            if (newPwd != null) querySets.Add(" PasswordHash = @newPwdHash, Salt = @newSalt");

            StringBuilder query = new("UPDATE Users SET");
            query.AppendJoin(',', querySets);
            query.Append(" WHERE UserID = @userID");

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query.ToString(), conn);
                cmd.Parameters.AddWithValue("@userID", userID);

                if (newNickname != null)
                    cmd.Parameters.AddWithValue("@newNickname", newNickname);
                
                if (onlineStatus != null)
                    cmd.Parameters.AddWithValue("@onlineStatus", onlineStatus);

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

        public static async Task<(bool success, string errorMessage)> SetAllOffline()
        {
            string query = "UPDATE Users SET OnlineStatus = FALSE";

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query.ToString(), conn);

                await cmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }

        }
    }

    public class FriendsDB
    {
        public static async Task<(bool success, string errorMessage)> Add(int senderID, int receiverID, FriendStatus status)
        {
            string query = "INSERT INTO Friends (SenderID, ReceiverID, FriendStatus) VALUES (@senderID, @receiverID, @status)";

            if (senderID < 1 || receiverID < 1)
                return (false, "Invalid user ID");

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@senderID", senderID);
                cmd.Parameters.AddWithValue("@receiverID", receiverID);
                cmd.Parameters.AddWithValue("@status", status.ToString());

                await cmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                if (ex.ErrorCode == 1062)
                    return (false, "Unable to send (more) request");
                return (false, ex.Message);
            }
        }

        public static async Task<(bool success, string errorMessage)> Update(int senderID, int receiverID, FriendStatus? status)
        {
            if (senderID < 1 || receiverID < 1)
                return (false, "Invalid user ID");

            string query = status != null ?
                "UPDATE Friends SET FriendStatus = @status WHERE SenderID = @senderID AND ReceiverID = @receiverID" :
                @"DELETE FROM Friends
                    WHERE (SenderID = @senderID AND ReceiverID = @receiverID)
                    OR (SenderID = @receiverID AND ReceiverID = @senderID AND FriendStatus = @confirmed)";

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@senderID", senderID);
                cmd.Parameters.AddWithValue("@receiverID", receiverID);
                if (status != null)
                    cmd.Parameters.AddWithValue("@status", status.ToString());
                else
                    cmd.Parameters.AddWithValue("@confirmed", FriendStatus.Confirmed.ToString());

                await cmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(bool success, string errorMessage)> UpdateAll(int receiverID, FriendStatus oldStatus, FriendStatus? newStatus)
        {
            if (receiverID < 1)
                return (false, "Invalid user ID");

            string query = newStatus != null ?
                "UPDATE Friends SET FriendStatus = @newStatus WHERE ReceiverID = @receiverID AND FriendStatus = @oldStatus" :
                "DELETE FROM Friends WHERE ReceiverID = @receiverID AND FriendStatus = @oldStatus";

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@receiverID", receiverID);
                cmd.Parameters.AddWithValue("@oldStatus", oldStatus.ToString());
                if (newStatus != null) cmd.Parameters.AddWithValue("@status", newStatus.ToString());

                await cmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(bool success, string errorMessage)> Block(int mainUserID, int blockedID)
        {
            if (blockedID < 1 || mainUserID < 1)
                return (false, "Invalid user ID");

            string delQuery =
                @"DELETE FROM Friends
                WHERE (SenderID = @mainUserID AND ReceiverID = @blockedID)
                OR (SenderID = @blockedID AND ReceiverID = @mainUserID)";

            string blockQuery = "INSERT INTO Friends (SenderID, ReceiverID, FriendStatus) VALUES (@senderID, @receiverID, @blocked)";

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand delCmd = new(delQuery, conn);
                delCmd.Parameters.AddWithValue("@mainUserID", mainUserID);
                delCmd.Parameters.AddWithValue("@blockedID", blockedID);

                await delCmd.ExecuteNonQueryAsync();

                using MySqlCommand blockCmd = new(blockQuery, conn);
                blockCmd.Parameters.AddWithValue("@senderID", blockedID);
                blockCmd.Parameters.AddWithValue("@receiverID", mainUserID);
                blockCmd.Parameters.AddWithValue("@blocked", FriendStatus.Blocked.ToString());

                await blockCmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(List<User>? pendingUsers, string errorMessage)> GetPendingRq(int mainUserID)
        {
            if (mainUserID < 1)
                return (null, "Invalid user ID");

            string query =
                @"SELECT
                    u.UserID,
                    u.Username,
                    u.Nickname
                FROM Users u
                JOIN Friends f ON u.UserID = f.SenderID AND f.FriendStatus = @pending
                WHERE f.ReceiverID = @mainUserID";

            List<User> pendingUsers = [];
            
            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@mainUserID", mainUserID);
                cmd.Parameters.AddWithValue("@pending", FriendStatus.Pending.ToString());
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    pendingUsers.Add(new()
                    {
                        UserID = reader.GetInt32("UserID"),
                        Username = reader.GetString("Username"),
                        Nickname = reader.GetString("Nickname"),
                    });
                }

                return (pendingUsers, "");
            }
            catch (MySqlException ex)
            {
                return (null, ex.Message);
            }
        }

        public static async Task<(List<Friend>? friends, string errorMessage)> GetFriends(int mainUserID)
        {
            if (mainUserID < 1)
                return (null, "Invalid user ID");

            string query =
                @"SELECT
                    DISTINCT u.UserID as FriendID,
                    u.Username,
                    u.Nickname,
                    u.OnlineStatus,
                    Count(m.MessageID) as UnreadCount
                FROM
					Users u
                LEFT JOIN
					PrivateMessages m ON u.UserID = m.SenderID AND m.ReceiverID = 1 AND m.ReadStatus = FALSE
                JOIN
					Friends f ON
						(u.UserID = f.SenderID OR u.UserID = f.ReceiverID)
                        AND (f.ReceiverID = @mainUserID OR f.SenderID = @mainUserID)
                        AND f.FriendStatus = @confirmed
                WHERE
					 u.UserID != @mainUserID
                GROUP BY
					u.UserID";

            List<Friend> friends = [];
            
            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@mainUserID", mainUserID);
                cmd.Parameters.AddWithValue("@confirmed", FriendStatus.Confirmed.ToString());
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    friends.Add(new(new()
                    {
                        UserID = reader.GetInt32("FriendID"),
                        Username = reader.GetString("Username"),
                        Nickname = reader.GetString("Nickname"),
                        PwdSet = null,
                    },  reader.GetBoolean("OnlineStatus"), reader.GetInt32("UnreadCount")));
                }

                return (friends, "");
            }
            catch (MySqlException ex)
            {
                return (null, ex.Message);
            }
        }
    }


    public class ChatGroupDB
    {
        public static async Task<(bool success, string errorMessage)> Add(ChatGroup chatGroup)
        {
            string createQuery = "INSERT INTO ChatGroups (GroupName, CreatorID, OnlineCount) VALUES (@groupName, @creatorID, @onlineCount)";
            string getQuery = "SELECT GroupID FROM ChatGroups WHERE GroupName = @groupName AND CreatorID = @creatorID ORDER BY CreatedTime DESC";
            string subcribeQuery = "INSERT INTO GroupMembers (GroupID, MemberID) VALUES (@groupID, @memberID)";

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand createCmd = new(createQuery, conn);
                createCmd.Parameters.AddWithValue("@groupName", chatGroup.GroupName);
                createCmd.Parameters.AddWithValue("@creatorID", chatGroup.CreatorID);
                createCmd.Parameters.AddWithValue("@onlineCount", chatGroup.OnlineCount);

                await createCmd.ExecuteNonQueryAsync();

                using MySqlCommand getCmd = new(getQuery, conn);
                getCmd.Parameters.AddWithValue("@groupName", chatGroup.GroupName);
                getCmd.Parameters.AddWithValue("@creatorID", chatGroup.CreatorID);

                int? groupID = (int?) await getCmd.ExecuteScalarAsync();
                if (groupID == null)
                    return (false, "Invalid groupID of just created group");

                using MySqlCommand subcribeCmd = new(subcribeQuery, conn);
                subcribeCmd.Parameters.AddWithValue("@groupID", groupID);
                subcribeCmd.Parameters.AddWithValue("@memberID", chatGroup.CreatorID);

                await subcribeCmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(bool success, string errorMessage)> SubUnsub(int groupID, int userID, bool subcribing)
        {
            string query = subcribing ? 
                "INSERT INTO GroupMembers (GroupID, MemberID) VALUES (@groupID, @memberID)" :
                "DELETE FROM GroupMembers WHERE GroupID = @groupID AND MemberID = @memberID";

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@groupID", groupID);
                cmd.Parameters.AddWithValue("@memberID", userID);

                await cmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(ChatGroup? requestedGroup, string errorMessage)> Get(int groupID)
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
        
        public static async Task<(List<ChatGroup>? groups, string errorMessage)> GetAll()
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
                        CreatorID = reader.IsDBNull("CreatorID") ? null : reader.GetInt32("CreatorID"),
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
        
        public static async Task<(List<ChatGroup>? groups, string errorMessage)> GetSubcribed(int userID)
        {
            if (userID < 1)
                return (null, "Invalid creator ID");

            string query =
                @"SELECT 
                    g.GroupID,
                    g.GroupName,
                    g.CreatorID,
                    g.CreatedTime,
                    g.OnlineCount
                FROM GroupMembers gm
                JOIN ChatGroups g ON gm.GroupID = g.GroupID
                WHERE gm.MemberID = @memberID
                ORDER BY g.GroupID";

            List<ChatGroup> subcribedGroups = [];
            
            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@memberID", userID);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    subcribedGroups.Add(new()
                    {
                        GroupName = reader.GetString("GroupName"),
                        GroupID = reader.GetInt32("GroupID"),
                        CreatorID = reader.IsDBNull("CreatorID") ? null : reader.GetInt32("CreatorID"),
                        CreatedTime = reader.GetDateTime("CreatedTime"),
                        OnlineCount = reader.GetInt32("OnlineCount")
                    });
                }

                return (subcribedGroups, "");
            }
            catch (MySqlException ex)
            {
                return (null, ex.Message);
            }
        }
        
        public static async Task<(List<ChatGroup>? groups, string errorMessage)> GetByCreator(int creatorID)
        {
            if (creatorID < 1)
                return (null, "Invalid creator ID");
            
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
                    groups.Add(new()
                    {
                        GroupID = reader.GetInt32("GroupID"),
                        GroupName = reader.GetString("GroupName"),
                        CreatorID = creatorID,
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

        public static async Task<(bool success, string errorMessage)> Update(int groupID, string? newName, int? onlineCount)
        {
            if (newName == null && onlineCount == null)
                return (false, "Invalid updated data");
                
            if (groupID < 1)
                return (false, "Invalid GroupID");

            StringBuilder query = new("UPDATE ChatGroups SET");

            if (newName != null)
                query.Append(" GroupName = @newName");

            if (onlineCount != null)
            {
                if (newName != null)
                    query.Append(',');
                query.Append(" OnlineCount = @onlineCount");
            }

            query.Append(" WHERE GroupID = @groupID");

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query.ToString(), conn);
                cmd.Parameters.AddWithValue("@groupID", groupID);
                if (newName != null) cmd.Parameters.AddWithValue("@newName", newName);
                if (onlineCount != null) cmd.Parameters.AddWithValue("@onlineCount", onlineCount);

                await cmd.ExecuteNonQueryAsync();

                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }

        }

        public static async Task<(bool success, string errorMessage)> Delete(int groupID)
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
    }

    public class MessageDB
    {
        public static async Task<(bool success, string errorMessage)> AddPrivate(Message message, bool readStatus)
        {
            string query = "INSERT INTO PrivateMessages (SenderID, ReceiverID, Content, ReadStatus) VALUES (@senderID, @receiverID, @content, @readStatus)";

            try
            {    
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@senderID", message.SenderID);
                cmd.Parameters.AddWithValue("@receiverID", message.ReceiverID);
                cmd.Parameters.AddWithValue("@content", message.Content);
                cmd.Parameters.AddWithValue("@readStatus", readStatus);

                await cmd.ExecuteNonQueryAsync();
                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(bool success, string errorMessage)> AddGroup(Message message)
        {
            string query = "INSERT INTO GroupMessages (SenderID, GroupID, Content) VALUES (@senderID, @groupID, @content)";

            try
            {    
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@senderID", message.SenderID);
                cmd.Parameters.AddWithValue("@groupID", message.GroupID);
                cmd.Parameters.AddWithValue("@content", message.Content);

                await cmd.ExecuteNonQueryAsync();
                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<(List<(int SenderID, int Count)>?, string errorMessage)> GetUnreadPrivate(int receiverID)
        {
            string unreadQuery =
                @"SELECT
                    DISTINCT SenderID,
                    Count(*) as Count
                FROM PrivateMessages
                WHERE ReceiverID = @receiverID AND readStatus = false
                GROUP BY SenderID";

            List<(int, int)> count = [];
            
            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(unreadQuery, conn);
                cmd.Parameters.AddWithValue("@receiverID", receiverID);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    count.Add(new(reader.GetInt32("SenderID"), reader.GetInt32("Count")));
                }

                return (count, "");
            }
            catch (MySqlException ex)
            {
                return (null, ex.Message);
            }
        }

        public static async Task<(int? unreadCount, string errorMessage)> GetUnreadGroup(User mainUser, ChatGroup group)
        {
            string timeQuery =
                @"SELECT
                    LogTime
                FROM ActivityLog
                WHERE (Source = @source AND Content = @content)
                ORDER BY LogTime DESC
                LIMIT BY 1";

            string countQuery =
                @"SELECT
                    Count(*) as Count
                FROM GroupMessages
                WHERE (GroupID = @groupID AND SentTime >= @leaveTime)
                ORDER BY SentTime";
            
            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand timeCmd = new(timeQuery, conn);
                timeCmd.Parameters.AddWithValue("@source", mainUser.ToString());
                timeCmd.Parameters.AddWithValue("@content", $"Disconnected from {group}");

                DateTime leaveTime = (DateTime?) await timeCmd.ExecuteScalarAsync() ?? group.CreatedTime;

                using MySqlCommand countCmd = new(countQuery, conn);
                countCmd.Parameters.AddWithValue("@groupID", group.GroupID);
                countCmd.Parameters.AddWithValue("@leaveTime", leaveTime);

                int unreadCount = (int?) await countCmd.ExecuteScalarAsync() ?? 0;

                return (unreadCount, "");
            }
            catch (MySqlException ex)
            {
                return (null, ex.Message);
            }
        }

        public static async Task<(bool success, string errorMessage)> SetReadPrivate(int receiverID, int senderID)
        {
            string query = 
                @"UPDATE PrivateMessages
                SET ReadStatus = TRUE
                WHERE (ReceiverID = @receiverID AND SenderID = @senderID AND ReadStatus = false)";

            try
            {    
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@receiverID", receiverID);
                cmd.Parameters.AddWithValue("@senderID", senderID);

                await cmd.ExecuteNonQueryAsync();
                return (true, "");
            }
            catch (MySqlException ex)
            {
                return (false, ex.Message);
            }
            
        }

        public static async Task<(List<Message>? requestedMsgList, string errorMessage)> GetHistoryPrivate(int mainUserID, int partnerID)
        {
            string query =
                @"SELECT
                    m.SentTime,
                    u.Nickname,
                    m.Content
                FROM PrivateMessages m
                JOIN Users u ON m.SenderID = u.UserID
                WHERE (m.SenderID = @mainUserID AND m.ReceiverID = @partnerID)
                OR (m.SenderID = @partnerID AND m.ReceiverID = @mainUserID)
                ORDER BY m.SentTime";

            List<Message> messages = [];
            
            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@mainUserID", mainUserID);
                cmd.Parameters.AddWithValue("@partnerID", partnerID);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    messages.Add(new()
                    {
                        Timestamp = reader.GetDateTime("SentTime"),
                        Nickname = reader.GetString("Nickname"),
                        Content = reader.GetString("Content")
                    });
                }

                return (messages, "");
            }
            catch (MySqlException ex)
            {
                return (null, ex.Message);
            }
        }

        public static async Task<(List<Message>? requestedMsgList, string errorMessage)> GetHistoryGroup(int groupID, int mainUserID)
        {
            string query = 
                @"SELECT
                    gm.SentTime,
                    u.Nickname,
                    gm.Content
                FROM GroupMessages gm
                JOIN Users u ON gm.SenderId = u.UserId
                JOIN Friends f ON
                    gm.SenderID = f.SenderID
                    AND f.ReceiverID = @mainUserID
                    AND f.FriendStatus != @blocked
                WHERE gm.GroupID = @groupID
                ORDER BY gm.SentTime";

            List<Message> messages = [];

            try
            {
                using MySqlConnection conn = new(connectionString);
                await conn.OpenAsync();

                using MySqlCommand cmd = new(query, conn);
                cmd.Parameters.AddWithValue("@mainUserID", mainUserID);
                cmd.Parameters.AddWithValue("@blocked", FriendStatus.Blocked.ToString());
                cmd.Parameters.AddWithValue("@groupID", groupID);
                
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    messages.Add(new()
                    {
                        Timestamp = reader.GetDateTime("SentTime"),
                        Nickname = reader.GetString("Nickname"),
                        Content = reader.GetString("Content")
                    });
                }

                return (messages, "");
            }
            catch (MySqlException ex)
            {
                return (null, ex.Message);
            }
        }
    }

    public class LogDB
    {
        public static async Task<(bool success, string errorMessage)> Add(string? source, string logContent)
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
        
        public static async Task<(List<Log>? requestedLogList, string errorMessage)> GetAll()
        {
            string query = "SELECT LogTime, Source, Content FROM ActivityLog ORDER BY LogTime";

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

        public static async Task<(bool success, string errorMessage)> Clear()
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
}