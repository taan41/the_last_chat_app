using System.Text.Json;

enum CommandType
{
    Empty, Error, RequestAES,
    Register, Login, Logout, SetNickname,
    RequestUserList, UserAsPartner, DeletePartner,
    RequestGroupList, CreateGroup, DeleteGroup, JoinGroup, LeaveGroup,
    Message
}

[Serializable]
class Command
{
    public CommandType CommandType { get; set; } = CommandType.Empty;
    public string Payload { get; set; } = "";

    public Command() {}

    public Command(CommandType commandType, string? payLoad)
    {
        CommandType = commandType;
        Payload = payLoad ?? "";
    }
    
    public static string Serialize(Command command) =>
        JsonSerializer.Serialize(command);

    public static Command? Deserialize(string data) =>
        JsonSerializer.Deserialize<Command>(data);
}