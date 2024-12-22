using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

[Serializable]
class ChatGroup
{
    public int GroupID { get; set; } = -1;
    public string GroupName { get; set; } = "";
    public int? CreatorID { get; set; }
    public DateTime CreatedTime { get; set; } = DateTime.Now;
    public int OnlineCount { get; set; } = 0;

    [JsonIgnore]
    public bool IsPublic { get; set; } = true;

    public ChatGroup() {}

    public ChatGroup(string groupName, int? creatorID, bool isPublic)
    {
        GroupName = groupName;
        CreatorID = creatorID;
        IsPublic = isPublic;
    }
    
    public override string ToString()
        => $"Group(ID:{GroupID})";

    public string Info(bool showName, bool showOnline)
    {
        StringBuilder info = new($"[ID: {GroupID:D3}]");
        if (showName) info.Append($" Name: {GroupName}");
        if (showOnline) info.Append($" ({OnlineCount} connected)");
        return info.ToString();
    }

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static ChatGroup? Deserialize(string data) =>
        JsonSerializer.Deserialize<ChatGroup>(data);
}