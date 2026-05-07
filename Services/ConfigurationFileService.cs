using System;
using System.IO;
using System.Text.Json;

namespace Clock;

public static class ConfigurationFileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static void Export(Configuration configuration, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Export path is empty.", nameof(path));

        path = EnsureTextExtension(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(configuration, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static Configuration Import(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Import path is empty.", nameof(path));

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Configuration>(json, JsonOptions)
               ?? throw new InvalidOperationException("Imported configuration file is empty or invalid.");
    }

    private static string EnsureTextExtension(string path)
    {
        return string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.ChangeExtension(path, ".txt");
    }
}
