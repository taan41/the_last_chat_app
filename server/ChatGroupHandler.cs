class ChatGroupHandler(ChatGroup? _chatGroup)
{
    private readonly ChatGroup? chatGroup = _chatGroup;
    private readonly List<ClientHandler> connectedClients = [];

    public ChatGroup? GetGroup => chatGroup;

    public async Task SendMessageToRoom(int senderID, string message)
    {
        // foreach(ClientHandler client in connectedClients)
            // await client.SendMessage(message);
    }

    public void AddClientToRoom(ClientHandler client)
    {
        lock(connectedClients)
        {
            connectedClients.Add(client);
            if(chatGroup != null)
                chatGroup.ConnectedNum = connectedClients.Count;
        }
    }

    public void RemoveClientFromRoom(ClientHandler client)
    {
        lock(connectedClients)
        {
            connectedClients.Remove(client);
            if(chatGroup != null)
                chatGroup.ConnectedNum = connectedClients.Count;
        }
    }
}