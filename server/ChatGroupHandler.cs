class ChatGroupHandler
{
    private readonly ChatGroup? chatGroup;
    private readonly List<int>? memberIDs;
    private readonly List<ClientHandler> connectedClients = [];
    private readonly Action<ChatGroupHandler> disposeAction;
    private readonly CancellationTokenSource disposeTokenSource = new();

    public ChatGroup? GetGroup => chatGroup;
    public List<int>? GetMemIDs => memberIDs;

    public ChatGroupHandler(ChatGroup _chatGroup, ClientHandler client, Action<ChatGroupHandler> _disposeAction)
    {
        chatGroup = _chatGroup;
        disposeAction = _disposeAction;

        AddClient(client);
    }

    public ChatGroupHandler(ClientHandler client, int mainUserID, int partnerID, Action<ChatGroupHandler> _disposeAction)
    {
        memberIDs = [mainUserID, partnerID];
        disposeAction = _disposeAction;

        AddClient(client);
    }

    public async void EchoMessage(Message message, ClientHandler sourceClient)
    {
        await DbHelper.SaveMessage(message);
        EchoCmd(new(CommandType.Message, message.Serialize()), sourceClient);
    }

    public void EchoCmd(Command cmd, ClientHandler sourceClient)
    {
        foreach (ClientHandler client in connectedClients)
            if(client != sourceClient)
                client.EchoCmd(cmd, disposeTokenSource.Token);
    }

    public async void AddClient(ClientHandler client)
    {
        client.SetUpGroupHandler(this);
        
        lock(connectedClients)
        {
            connectedClients.Add(client);
        }

        if(chatGroup != null)
        {
            chatGroup.OnlineCount = connectedClients.Count;
            await DbHelper.UpdateChatGroup(chatGroup, true);
        }
        
        LogManager.AddLog($"{client} connected", this);
    }

    public async void RemoveClient(ClientHandler client)
    {
        lock(connectedClients)
        {
            connectedClients.Remove(client);
        }

        if(chatGroup != null)
        {
            chatGroup.OnlineCount = connectedClients.Count;
            await DbHelper.UpdateChatGroup(chatGroup, true);
        }
        
        LogManager.AddLog($"{client} disconnected", this);

        if(connectedClients.Count == 0)
        {
            disposeTokenSource.Cancel();
            disposeAction(this);
            LogManager.AddLog($"Auto-disposed", this);
        }
    }

    public async void Dispose()
    {
        lock(connectedClients)
        {
            connectedClients.ForEach(client => client.SetUpGroupHandler(null));
            connectedClients.Clear();
        }

        if(chatGroup != null)
        {
            chatGroup.OnlineCount = 0;
            await DbHelper.UpdateChatGroup(chatGroup, true);
        }

        disposeTokenSource.Cancel();
        disposeAction(this);
        LogManager.AddLog($"Manually disposed", this);
    }

    public override string ToString()
        => chatGroup?.ToString(false) ?? $"User'{memberIDs?[0]}'&'{memberIDs?[1]}'";
}