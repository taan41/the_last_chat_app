class PrivateChatHandler(ClientHandler client, int user1ID, int user2ID, Action<ChatHandler> disposeAction) : ChatHandler(client, disposeAction)
{
    private readonly List<int> members = user1ID < user2ID ? [user1ID, user2ID] : [user2ID, user1ID];

    public List<int> GetMembers => members;

    public override async void EchoMessage(Message message, ClientHandler sourceClient)
    {
        await DBHelper.MessageDB.AddPrivate(message, connectedClients.Count == 2);
        EchoCmd(new(CommandType.EchoMessage, message.Serialize()), sourceClient);
    }

    public override void EchoCmd(Command cmd, ClientHandler sourceClient)
    {
        foreach (ClientHandler client in connectedClients)
            if(client != sourceClient)
                client.EchoCmd(cmd, disposeTokenSource.Token);
    }

    public override void AddClient(ClientHandler client)
    {
        base.AddClient(client);
    }

    public override void RemoveClient(ClientHandler client)
    {
        base.RemoveClient(client);
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    public override string ToString()
        => $"Private({members[0]}-{members[1]})";
}