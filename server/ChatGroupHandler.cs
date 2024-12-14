class ChatGroupHandler
{
    private readonly ChatGroup? chatGroup;
    private readonly List<ClientHandler> connectedClients = [];
    private readonly bool privateChat = false;
    private readonly Action<ChatGroupHandler> disposeAction;

    public ChatGroup? GetGroup => chatGroup;
    public bool PrivateChat => privateChat;

    public ChatGroupHandler(ChatGroup _chatGroup, ClientHandler client, Action<ChatGroupHandler> _disposeAction)
    {
        chatGroup = _chatGroup;
        AddClient(client);
        disposeAction = _disposeAction;
    }

    public ChatGroupHandler(bool _privateChat, ClientHandler client1, Action<ChatGroupHandler> _disposeAction)
    {
        AddClient(client1);
        privateChat = _privateChat;
        disposeAction = _disposeAction;
    }

    public void EchoMessage(Message message)
    {
        lock(connectedClients)
            foreach (ClientHandler client in connectedClients)
                _ = Task.Run(() => client.EchoMessage(message));
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
            }

            if(connectedClients.Count == 0)
                disposeAction(this);
        }
    }
}