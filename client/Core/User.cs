using System.Text.Json;

[Serializable]
class User
{
    public int? UID { get; set; }
    public string Username { get; set; } = "";
    public string Nickname { get; set; } = "";
    public PasswordSet? PwdSet { get; set; }

    public User() {}

    public User(User userTemplate)
    {
        UID = userTemplate.UID;
        Username = userTemplate.Username;
        Nickname = userTemplate.Nickname;
        PwdSet = userTemplate.PwdSet;
    }
    
    public override string ToString()
        => $"[ID: {UID}] Username: {Username}, Nickname: {Nickname}";

    public string ToString(bool showUsername)
        => showUsername ? ToString(true) : $"[ID: {UID}] Nickname: {Nickname}";
    
    public static string Serialize(User user) =>
        JsonSerializer.Serialize(user);

    public static User? Deserialize(string data) =>
        JsonSerializer.Deserialize<User>(data);
}