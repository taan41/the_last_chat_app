using System.Security.Cryptography;
using System.Text;

class MagicNumbers
{
    public const int
        usernameLimit = 50,
        passwordLimit = 100,
        nicknameLimit = 100,
        groupNameLimit = 50,
        inputLimit = 500,
        bufferSize = 2048,
        pwdHashLen = 32,
        pwdSaltLen = 16;
}

class Utilities
{
    // UTF-16 encoding methods
    public static string DecodeBytes(byte[] data) => Encoding.Unicode.GetString(data);

    public static string DecodeBytes(byte[] data, int index, int length) => Encoding.Unicode.GetString(data, index, length);

    public static byte[] EncodeString(string content) => Encoding.Unicode.GetBytes(content);

    // Hash & verify password
    public static (byte[] Hash, byte[] Salt) HashPassword(string password)
    {
        byte[] salt = new byte[MagicNumbers.pwdSaltLen];
        RandomNumberGenerator.Fill(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(EncodeString(password), salt, 5000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(MagicNumbers.pwdHashLen);

        return (hash, salt);
    }

    public static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(EncodeString(password), storedSalt, 5000, HashAlgorithmName.SHA256);
        byte[] hash = pbkdf2.GetBytes(MagicNumbers.pwdHashLen);

        return hash.SequenceEqual(storedHash);
    }

}