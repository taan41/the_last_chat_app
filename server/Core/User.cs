using System.Text;
using System.Text.Json;

[Serializable]
class User
{
    public int UserID { get; set; } = -1;
    public string Username { get; set; } = "";
    public string Nickname { get; set; } = "";
    public PasswordSet? PwdSet { get; set; }
    public bool OnlineStatus { get; set; } = false;

    public User() {}

    public User(User userTemplate)
    {
        UserID = userTemplate.UserID;
        Username = userTemplate.Username;
        Nickname = userTemplate.Nickname;
        PwdSet = userTemplate.PwdSet;
    }
    
    public override string ToString()
        => $"User(ID:{UserID})";

    public string Info(bool showUsername, bool showNickname, bool showOnline)
    {
        List<string> info = [];
        if (showUsername) info.Add($" Username: {Username}");
        if (showNickname) info.Add($" Nickname: {Nickname}");

        StringBuilder sb = new($"[ID: {UserID}]");
        sb.AppendJoin(',', info);
        if (showOnline) sb.Append($" ({(OnlineStatus ? "Online" : "Offline")})");
        return sb.ToString();
    }

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static User? Deserialize(string data) =>
        JsonSerializer.Deserialize<User>(data);
}