using System.Text;
using System.Text.Json;

[Serializable]
class Friend(User baseUser, bool isOnline, int unreadCount)
{
    public User BaseUser { get; set; } = baseUser;
    public bool IsOnline { get; set; } = isOnline;
    public int UnreadCount { get; set; } = unreadCount;

    public override string ToString()
        => $"Friend(ID:{BaseUser.UserID})";

    public string Info()
    {
        StringBuilder sb = new(BaseUser.Info(false, true));
        sb.Append($" ({(IsOnline ? "Online" : "Offline")})");
        if (UnreadCount > 0) sb.Append($" ({UnreadCount} unread)");

        return sb.ToString();
    }

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static Friend? Deserialize(string data) =>
        JsonSerializer.Deserialize<Friend>(data);
}