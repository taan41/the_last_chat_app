class ChatGroup(int _id, string _name)
{
    private readonly List<ClientHandler> connectedClients = [];
    
    private int id = _id;
    private string name = _name;

    public int ID { get => id; set => id = value; }
    public string Name { get => name; set => name = value; }

    public async Task SendMessageToRoom(int senderID, string message)
    {
        // foreach(ClientHandler client in connectedClients)
            // await client.SendMessage(message);
    }

    public void AddClientToRoom(ClientHandler client)
    {
        lock(connectedClients)
            connectedClients.Add(client);
    }

    public void RemoveClientFromRoom(ClientHandler client)
    {
        lock(connectedClients)
            connectedClients.Remove(client);
    }

    public override string ToString()
        => ToString(false);

    public string ToString(bool showConnectedCount)
        => $"{Name}{(showConnectedCount ? $" ({connectedClients.Count} connected)" : "")}";
}