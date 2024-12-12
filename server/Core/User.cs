class User(int _uid, string _username, string _nickname)
{
    private readonly int uid = _uid;
    private readonly string username = _username;
    private string nickname = _nickname;

    public int UID => uid;

    public string Username => username;

    public string Nickname { get => nickname; set => nickname = value; }

    public override string ToString()
        => $"ID: {UID}, Username: {Username}, Nickname: {Nickname}";
}