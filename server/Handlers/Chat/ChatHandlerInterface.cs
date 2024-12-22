abstract class ChatHandler
{
    protected readonly List<ClientHandler> connectedClients = [];
    protected readonly Action<ChatHandler> serverDisposeAction;
    protected readonly CancellationTokenSource disposeTokenSource = new();

    protected ChatHandler(ClientHandler client, Action<ChatHandler> _disposeAction)
    {
        serverDisposeAction = _disposeAction;

        AddClient(client);
    }

    public abstract void EchoMessage(Message message, ClientHandler sourceClient);

    public virtual void EchoCmd(Command cmd, ClientHandler sourceClient)
    {
        foreach (var client in connectedClients)
            if (client != sourceClient)
                client.EchoCmd(cmd, disposeTokenSource.Token);
    }

    public virtual void EchoFile(FileData file, ClientHandler sourceClient)
    {
        if (file.FileBytes == null)
            return;
            
        EchoCmd(new Command(CommandType.SendFile, file.Serialize()), sourceClient);
        
        foreach (var client in connectedClients)
            if (client != sourceClient)
                client.EchoByte(file.FileBytes, disposeTokenSource.Token);
    }

    public virtual void AddClient(ClientHandler client)
    {
        client.SetUpGroupHandler(this);

        lock (connectedClients)
        {
            connectedClients.Add(client);
            LogManager.AddLog($"{client} connected", this);
        }
    }

    public virtual void RemoveClient(ClientHandler client)
    {
        lock (connectedClients)
        {
            connectedClients.Remove(client);
            LogManager.AddLog($"{client} disconnected", this);

            if (connectedClients.Count == 0)
            {
                serverDisposeAction(this);
                LogManager.AddLog($"Auto-disposed", this);
            }
        }
    }

    public virtual void Dispose()
    {
        lock (connectedClients)
        {
            connectedClients.ForEach(client => client.SetUpGroupHandler(null));
            connectedClients.Clear();
        }

        disposeTokenSource.Cancel();
        serverDisposeAction(this);
        LogManager.AddLog($"Manually disposed", this);
    }

    public override string ToString()
        => "Base ChatHandler class";
}