using System.Text.Json;

[Serializable]
class PasswordSet(byte[] pwdHash, byte[] salt)
{
    public byte[] PwdHash { get; set; } = pwdHash;
    public byte[] Salt { get; set; } = salt;

    public static string Serialize(PasswordSet pwdSet) =>
        JsonSerializer.Serialize(pwdSet);

    public static PasswordSet? Deserialize(string data) =>
        JsonSerializer.Deserialize<PasswordSet>(data);
}