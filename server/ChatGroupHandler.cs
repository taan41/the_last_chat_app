class ChatGroupHandler
{
    private readonly ChatGroup? chatGroup;
    private readonly List<int>? membersIDs;
    private readonly List<ClientHandler> connectedClients = [];
    private readonly Action<ChatGroupHandler> disposeAction;

    public ChatGroup? GetGroup => chatGroup;
    public List<int>? GetMemIDs => membersIDs;

    public ChatGroupHandler(ChatGroup _chatGroup, ClientHandler client, Action<ChatGroupHandler> _disposeAction)
    {
        AddClient(client);
        chatGroup = _chatGroup;
        disposeAction = _disposeAction;
    }

    public ChatGroupHandler(ClientHandler client1, int mainUserID, int partnerID, Action<ChatGroupHandler> _disposeAction)
    {
        AddClient(client1);
        membersIDs = [mainUserID, partnerID];
        disposeAction = _disposeAction;
    }

    public async Task EchoMessage(Message message, ClientHandler sourceClient)
    {
        await DbHelper.SaveGroupMessage(message);

        foreach (ClientHandler client in connectedClients)
            if(client != sourceClient)
                await client.EchoMessage(message);
    }

    public void AddClient(ClientHandler client)
    {
        client.SetUpGroupHandler(this);
        
        lock(connectedClients)
        {
            connectedClients.Add(client);

            if(chatGroup != null)
            {
                chatGroup.OnlineCount = connectedClients.Count;
                Task.WhenAny(DbHelper.UpdateChatGroup(chatGroup));
                LogManager.AddLog($"{client.EndPoint} connected to group '{chatGroup.ToString(false)}'");
            }
        }
    }

    public void RemoveClient(ClientHandler client)
    {
        lock(connectedClients)
        {
            connectedClients.Remove(client);

            if(chatGroup != null)
            {
                chatGroup.OnlineCount = connectedClients.Count;
                Task.WhenAny(DbHelper.UpdateChatGroup(chatGroup));
                LogManager.AddLog($"{client.EndPoint} disconnected from group '{chatGroup.ToString(false)}'");
            }

            if(connectedClients.Count == 0)
            {
                disposeAction(this);
                LogManager.AddLog($"Handler of group '{chatGroup?.ToString(false) ?? "(private)"}' auto-disposed");
            }
        }
    }
}