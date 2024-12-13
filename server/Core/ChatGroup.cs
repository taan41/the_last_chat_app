using System.Text.Json;

[Serializable]
class ChatGroup
{
    public int? GroupID { get; set; }
    public string GroupName { get; set; } = "";
    public int? CreatorID { get; set; }
    public DateTime? CreatedTime { get; set; }
    public int OnlineCount { get; set; } = 0;

    public ChatGroup() {}

    public ChatGroup(string groupName, int? creatorID)
    {
        GroupName = groupName;
        CreatorID = creatorID;
    }
    
    public override string ToString()
        => $"[ID: {GroupID:D3}] {GroupName} ({OnlineCount} connected)";

    public string ToString(bool showOnline)
        => showOnline ? ToString() : $"$[ID: {GroupID:D3}] {GroupName}";

    public static string Serialize(ChatGroup group) =>
        JsonSerializer.Serialize(group);

    public static ChatGroup? Deserialize(string data) =>
        JsonSerializer.Deserialize<ChatGroup>(data);
}