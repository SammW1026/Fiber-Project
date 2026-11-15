using System.Text.Json;
using System.Text.Json.Serialization;

namespace FiberBlueprint;

// Reading and writing network JSON files.
public static class NetworkFile
{
    static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Throws FileNotFoundException / DirectoryNotFoundException / JsonException.
    public static Network? Load(string path) =>
        JsonSerializer.Deserialize<Network>(File.ReadAllText(path), ReadOptions);

    public static void Save(Network net, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(net, WriteOptions));
    }
}
