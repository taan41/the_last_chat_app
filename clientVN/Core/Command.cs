using System.Text.Json;

enum CommandType
{
    Empty, Ping, Error, GetAES,
    CheckUsername, Register,
    GetUserPwd, Login, Logout,
    ChangeNickname, ChangePassword,
    GetFriendList, RemoveFriend,
    GetReceivedRq, AcceptFriendRq, AcceptAllRq, DenyFriendRq, DenyAllRq,
    GetAllUsers, SendFriendRq, BlockUser, UnblockUser, BlockAll,
    SetPartner, GetPartnerHistory, RemovePartner,
    GetCreatedGroups, CreateGroup, ChangeGroupName, DeleteGroup,
    GetSubcribed, RemoveSubcribed, GetAllGroups, SubcribeToGroup,
    JoinGroup, GetGroupInfo, GetGroupHistory, LeaveGroup,
    Message, EchoMessage, NoticePrivateMsg,
    SendFile, DoneSendingFile, AcceptFile,
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