using System.Security.Cryptography;
using System.Text;

static class MagicNum
{
    public const int
        usernameMin = 6, usernameMax = 50,
        passwordMin = 8, passwordMax = 100,
        nicknameMin = 1, nicknameMax = 100,
        groupnameMin = 6, groupNameMax = 50,
        pwdHashLen = 32, pwdSaltLen = 16,
        inputLimit = 500,
        bufferSize = 2048;
}

static class FriendStatus
{
    public const string
        pending = "Pending",
        confirmed = "Confirmed",
        blocked = "Blocked";
}

static class Utilities
{
    // UTF-16 encoding methods
    public static string DecodeBytes(byte[] data) => Encoding.UTF8.GetString(data);

    public static string DecodeBytes(byte[] data, int index, int length) => Encoding.UTF8.GetString(data, index, length);

    public static byte[] EncodeString(string content) => Encoding.UTF8.GetBytes(content);

    // Hash & verify password
    public static (byte[] PwdHash, byte[] Salt) HashPassword(string pwd)
    {
        byte[] salt = new byte[MagicNum.pwdSaltLen];
        RandomNumberGenerator.Fill(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(EncodeString(pwd), salt, 5000, HashAlgorithmName.SHA256);
        byte[] pwdHash = pbkdf2.GetBytes(MagicNum.pwdHashLen);

        return (pwdHash, salt);
    }

    public static bool VerifyPassword(string pwd, byte[] storedPwdHash, byte[] storedSalt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(EncodeString(pwd), storedSalt, 5000, HashAlgorithmName.SHA256);
        byte[] pwdHash = pbkdf2.GetBytes(MagicNum.pwdHashLen);

        return pwdHash.SequenceEqual(storedPwdHash);
    }

    public static bool VerifyPassword(string pwd, PasswordSet pwdSet)
        => VerifyPassword(pwd, pwdSet.PwdHash, pwdSet.Salt);

}