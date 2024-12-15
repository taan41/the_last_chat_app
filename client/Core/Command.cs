using System.Text.Json;

enum CommandType
{
    Empty, Ping, Error, GetAES,
    CheckUsername, Register,
    GetUserPwd, Login, Logout,
    ChangeNickname, ChangePassword,
    GetUserList, SetPartner, GetPartnerHistory, RemovePartner,
    GetCreatedGroups, CreateGroup, DeleteGroup,
    GetGroupList, GetGroupInfo, GetGroupHistory, 
    JoinGroup, LeaveGroup,
    Message, EchoMessage, SendFile,
    Disconnect
}

[Serializable]
class Command
{
    public CommandType CommandType { get; set; } = CommandType.Empty;
    public string Payload { get; set; } = "";

    public Command() {}

    public Command(CommandType cmdType, string? payload)
    {
        CommandType = cmdType;
        Payload = payload ?? "";
    }

    public void Set(CommandType cmdType, string? payload)
    {
        CommandType = cmdType;
        Payload = payload ?? "";
    }

    public void SetError(string? payload)
        => Set(CommandType.Error, payload);

    public string Name()
        => CommandType.ToString();

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static Command? Deserialize(string data) =>
        JsonSerializer.Deserialize<Command>(data);
}