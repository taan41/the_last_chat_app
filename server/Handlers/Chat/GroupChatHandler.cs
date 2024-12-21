class GroupChatHandler(ChatGroup _chatGroup, ClientHandler client, Action<ChatHandler> disposeAction) : ChatHandler(client, disposeAction)
{
    private readonly ChatGroup chatGroup = _chatGroup;

    public ChatGroup GetGroup => chatGroup;

    public override async void EchoMessage(Message message, ClientHandler sourceClient)
    {
        await DBHelper.MessageDB.AddGroup(message);
        EchoCmd(new(CommandType.Message, message.Serialize()), sourceClient);
    }

    public override void EchoCmd(Command cmd, ClientHandler sourceClient)
    {
        foreach (ClientHandler client in connectedClients)
            if(client != sourceClient)
                client.EchoCmd(cmd, disposeTokenSource.Token);
    }

    public override async void AddClient(ClientHandler client)
    {
        base.AddClient(client);

        await DBHelper.ChatGroupDB.Update(chatGroup.GroupID, null, chatGroup.OnlineCount);
    }

    public override async void RemoveClient(ClientHandler client)
    {
        base.RemoveClient(client);
        
        await DBHelper.ChatGroupDB.Update(chatGroup.GroupID, null, chatGroup.OnlineCount);
    }

    public override async void Dispose()
    {
        base.Dispose();

        chatGroup.OnlineCount = 0;
        await DBHelper.ChatGroupDB.Update(chatGroup.GroupID, null, 0);
    }

    public override string ToString()
        => chatGroup.ToString();
}