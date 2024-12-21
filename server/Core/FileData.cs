using System.Text.Json;
using System.Text.Json.Serialization;

[Serializable]
class FileData
{
    public string? FileName { get; set; }
    public int FileSize { get; set; }

    [JsonIgnore]
    public byte[]? FileBytes { get; set; }

    public FileData() {}

    public FileData(string fileName, byte[] fileBytes)
    {
        FileName = fileName;
        FileBytes = fileBytes;
        FileSize = FileBytes.Length;
    }

    public string Serialize()
        => JsonSerializer.Serialize(this);

    public static FileData? Deserialize(string data)
        => JsonSerializer.Deserialize<FileData>(data);
}