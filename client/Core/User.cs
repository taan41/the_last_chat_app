using System.Text.Json;

[Serializable]
class User
{
    public int? UID { get; set; }
    public string Username { get; set; } = "";
    public string Nickname { get; set; } = "";
    public PasswordSet? PwdSet { get; set; }

    public User() {}

    public User(string username, string nickname)
    {
        Username = username;
        Nickname = nickname;
    }

    public User(int? uid, string username, string nickname, PasswordSet? pwdSet) : this(username, nickname)
    {
        UID = uid;
        PwdSet = pwdSet;
    }
    
    public override string ToString()
        => $"ID: {UID}, Username: {Username}, Nickname: {Nickname}";
    
    public static string Serialize(User user) =>
        JsonSerializer.Serialize(user);

    public static User? Deserialize(string data) =>
        JsonSerializer.Deserialize<User>(data);
}