using System.Security.Cryptography;
using System.Text;

class MagicNumbers
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

class Utilities
{
    // UTF-16 encoding methods
    public static string DecodeBytes(byte[] data) => Encoding.Unicode.GetString(data);

    public static string DecodeBytes(byte[] data, int index, int length) => Encoding.Unicode.GetString(data, index, length);

    public static byte[] EncodeString(string content) => Encoding.Unicode.GetBytes(content);

    // Hash & verify password
    public static (byte[] PwdHash, byte[] Salt) HashPassword(string pwd)
    {
        byte[] salt = new byte[MagicNumbers.pwdSaltLen];
        RandomNumberGenerator.Fill(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(EncodeString(pwd), salt, 5000, HashAlgorithmName.SHA256);
        byte[] pwdHash = pbkdf2.GetBytes(MagicNumbers.pwdHashLen);

        return (pwdHash, salt);
    }

    public static bool VerifyPassword(string pwd, byte[] storedPwdHash, byte[] storedSalt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(EncodeString(pwd), storedSalt, 5000, HashAlgorithmName.SHA256);
        byte[] pwdHash = pbkdf2.GetBytes(MagicNumbers.pwdHashLen);

        return pwdHash.SequenceEqual(storedPwdHash);
    }

    public static bool VerifyPassword(string pwd, PasswordSet pwdSet)
        => VerifyPassword(pwd, pwdSet.PwdHash, pwdSet.Salt);

}