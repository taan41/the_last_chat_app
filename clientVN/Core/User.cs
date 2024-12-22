using System.Text;
using System.Text.Json;

[Serializable]
class User
{
    public int UserID { get; set; } = -1;
    public string Username { get; set; } = "";
    public string Nickname { get; set; } = "";
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
        => $"User(ID:{UserID})";

    public string Info(bool showUsername, bool showNickname)
    {
        StringBuilder sb = new($"[ID: {UserID}]");
        if (showUsername) sb.Append($" Username: {Username}");
        if (showNickname) sb.Append($" Nickname: {Nickname}");
        return sb.ToString();
    }

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static User? Deserialize(string data) =>
        JsonSerializer.Deserialize<User>(data);
}