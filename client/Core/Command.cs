using System.Text.Json;

enum CommandType
{
    Empty, Ping, Error, RequestAES,
    CheckUsername, Register,
    RequestUserPwd, Login, Logout,
    SetNickname,
    RequestUserList, UserAsPartner, DeletePartner,
    RequestGroupList, CreateGroup, DeleteGroup, JoinGroup, LeaveGroup,
    Message,
    Disconnect
}

[Serializable]
class Command
{
    public CommandType CommandType { get; set; } = CommandType.Empty;
    public string Payload { get; set; } = "";

    public Command() {}

    public Command(CommandType commandType, string? payload)
    {
        CommandType = commandType;
        Payload = payload ?? "";
    }

    public void Set(CommandType cmdType, string? payload)
    {
        CommandType = cmdType;
        Payload = payload ?? "";
    }

    public void SetError(string? payload)
        => Set(CommandType.Error, payload);
    
    public static string Serialize(Command command) =>
        JsonSerializer.Serialize(command);

    public static Command? Deserialize(string data) =>
        JsonSerializer.Deserialize<Command>(data);
}