using System.Text;
using System.Text.Json;

[Serializable]
class User
{
    public int UserID { get; set; } = -1;
    public string Username { get; set; } = "";
    public string Nickname { get; set; } = "";
    public bool OnlineStatus { get; set; } = false;
    public PasswordSet? PwdSet { get; set; }

    public User() {}

    public User(User userTemplate)
    {
        UserID = userTemplate.UserID;
        Username = userTemplate.Username;
        Nickname = userTemplate.Nickname;
        PwdSet = userTemplate.PwdSet;
    }
    
    public override string ToString()
        => $"'User({UserID})'";

    public string Info(bool showUsername, bool showNickname, bool showOnline)
    {
        StringBuilder info = new($"[ID: {UserID}]");
        if (showUsername) info.Append($" Username: '{Username}'");
        if (showNickname) info.Append($" Nickname: '{Nickname}'");
        if (showOnline) info.Append($" ({(OnlineStatus ? "Online" : "Offline")})");
        return info.ToString();
    }

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static User? Deserialize(string data) =>
        JsonSerializer.Deserialize<User>(data);
}