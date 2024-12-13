using System.Text.Json;

[Serializable]
class ChatGroup(string groupName, int? creatorID)
{
    public int? GroupID { get; set; }
    public string GroupName { get; set; } = groupName;
    public int? CreatorID { get; set; } = creatorID;
    public DateTime? CreateTime { get; set; }
    public int? ConnectedNum { get; set; }

    public static string Serialize(ChatGroup group) =>
        JsonSerializer.Serialize(group);

    public static ChatGroup? Deserialize(string data) =>
        JsonSerializer.Deserialize<ChatGroup>(data);
}