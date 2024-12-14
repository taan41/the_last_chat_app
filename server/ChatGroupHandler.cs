class ChatGroupHandler
{
    private readonly ChatGroup? chatGroup;
    private readonly List<int>? memberIDs;
    private readonly List<ClientHandler> connectedClients = [];
    private readonly Action<ChatGroupHandler> disposeAction;

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
            else if(memberIDs != null)
            {
                LogManager.AddLog($"{client.EndPoint} connected to private chat of '{memberIDs}");
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
            else if(memberIDs != null)
            {
                LogManager.AddLog($"{client.EndPoint} disconnected from private chat of '{memberIDs}");
            }

            if(connectedClients.Count == 0)
            {
                disposeAction(this);
                LogManager.AddLog($"Handler of group '{chatGroup?.ToString(false) ?? memberIDs!.ToString()}' auto-disposed");
            }
        }
    }
}