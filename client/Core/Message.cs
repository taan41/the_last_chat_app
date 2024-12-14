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

    // public Message(int senderID, int? receiverID, int? groupID, string nickname, string content) : this(DateTime.Now, nickname, content)

    public Message(User sender, User? receiver, ChatGroup? joinedGroup, string content)
    {
        if(receiver != null)
            ReceiverID = receiver.UID;
        else if(joinedGroup != null)
            GroupID = joinedGroup.GroupID;
        else
            throw new ArgumentException("Both ReceiverID and GroupID can't be null");
        
        SenderID = sender.UID;
        Nickname = sender.Nickname;
        Content = content;
    }

    public override string ToString()
        => $"[{Timestamp:dd/MM HH:mm}] {Nickname}: {Content}";
    
    public static string Serialize(Message pwdSet) =>
        JsonSerializer.Serialize(pwdSet);

    public static Message? Deserialize(string data) =>
        JsonSerializer.Deserialize<Message>(data);
}