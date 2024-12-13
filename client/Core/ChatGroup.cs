using System.Text.Json;

[Serializable]
class ChatGroup(string groupName, int? creatorID)
{
    public int? GroupID { get; set; }
    public string GroupName { get; set; } = groupName;
    public int? CreatorID { get; set; } = creatorID;
    public DateTime? CreatedTime { get; set; }
    public int? ConnectedNum { get; set; }
    
    public override string ToString()
        => $"[ID: {GroupID:D3}] {GroupName} ({ConnectedNum} online)";

    public string ToString(bool showConnectedNum)
        => showConnectedNum ? ToString() : $"$[ID: {GroupID:D3}] {GroupName}";

    public static string Serialize(ChatGroup group) =>
        JsonSerializer.Serialize(group);

    public static ChatGroup? Deserialize(string data) =>
        JsonSerializer.Deserialize<ChatGroup>(data);
}