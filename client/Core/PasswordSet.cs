using System.Text.Json;

[Serializable]
class PasswordSet(byte[] pwdHash, byte[] salt)
{
    public byte[] PwdHash { get; set; } = pwdHash;
    public byte[] Salt { get; set; } = salt;

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static PasswordSet? Deserialize(string data) =>
        JsonSerializer.Deserialize<PasswordSet>(data);
}