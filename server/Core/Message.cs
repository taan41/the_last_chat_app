class Message
{
    private readonly DateTime timestamp;
    private readonly int? senderID, receiverID, groupID;
    private readonly string nickname;
    private readonly string content;

    public Message(int _senderID, int? _receiverID, int? _groupID, string _nickname, string _content)
    {
        timestamp = DateTime.Now;
        senderID = _senderID;
        nickname = _nickname;
        content = _content;

        if(_receiverID.HasValue)
            receiverID = _receiverID;
        else if(_groupID.HasValue)
            groupID = _groupID;
        else
            throw new ArgumentException("Both ReceiverID and GroupID can't be null");
    }

    public Message(DateTime _timestamp, string _nickname, string _content)
    {
        timestamp = _timestamp;
        nickname = _nickname;
        content = _content;
    }

    public int? SenderID => senderID;
    public int? ReceiverID => receiverID;
    public int? GroupID => groupID;
    public string Nickname => nickname;
    public string Content => content;

    public override string ToString()
        => $"[{timestamp:dd/MM HH:mm}] {nickname}: {content}";
}