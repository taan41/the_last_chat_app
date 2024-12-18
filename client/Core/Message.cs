using System.Text.Json;

[Serializable]
class Message
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Nickname { get; set; } = "nickname";
    public string Content { get; set; } = "content";
    public int? SenderID { get; set; }
    public int? ReceiverID { get; set; }
    public int? GroupID { get; set; }

    public Message() {}

    public Message(DateTime timesptamp, string nickname, string content)
    {
        Timestamp = timesptamp;
        Nickname = nickname;
        Content = content;
    }

    public Message(User sender, User? receiver, ChatGroup? joinedGroup, string content)
    {
        if(receiver != null)
            ReceiverID = receiver.UserID;
        else if(joinedGroup != null)
            GroupID = joinedGroup.GroupID;
        else
            throw new ArgumentException("Both ReceiverID and GroupID can't be null");
        
        SenderID = sender.UserID;
        Nickname = sender.Nickname;
        Content = content;
    }

    public string Print()
        => $"[{Timestamp:dd/MM HH:mm}] {Nickname}: {Content}";

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static Message? Deserialize(string data) =>
        JsonSerializer.Deserialize<Message>(data);
}