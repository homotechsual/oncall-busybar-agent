using System.Text.Json;
using System.Text.Json.Nodes;

namespace OncallAgent.Config;

/// <summary>The settings the settings window edits. A null value means "leave it as it is", which is
/// how fields an admin manages through policy stay untouched. An empty password clears it.</summary>
public sealed record UserSettings(string? Address, string? HttpAccessPassword, bool? MicEnabled);

/// <summary>Writes <see cref="UserSettings"/> into the per-user appsettings.json, keeping every other
/// key in the file as it was. Comments in the file don't survive a save.</summary>
public static class UserSettingsStore
{
    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <exception cref="JsonException">The existing file isn't valid JSON. It's left alone rather
    /// than overwritten, so nothing else in it is lost.</exception>
    public static void Save(string path, UserSettings settings)
    {
        var root = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path), documentOptions: ReadOptions) as JsonObject
              ?? throw new JsonException($"{path} doesn't contain a JSON object.")
            : new JsonObject();

        if (settings.Address is not null)
        {
            Set(Section(root, BusyBarOptions.SectionName), "Address", settings.Address);
        }

        if (settings.HttpAccessPassword is not null)
        {
            var busyBar = Section(root, BusyBarOptions.SectionName);
            Set(busyBar, "HttpAccessPassword", "");
            Set(busyBar, "HttpAccessPasswordProtected", settings.HttpAccessPassword.Length == 0
                ? null
                : HttpAccessPasswordProtector.Protect(settings.HttpAccessPassword));
        }

        if (settings.MicEnabled is { } micEnabled)
        {
            Set(Section(root, MicOptions.SectionName), "Enabled", micEnabled);
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write alongside and swap in, so a failed write can't leave a half-written file behind.
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, root.ToJsonString(WriteOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    // Configuration keys are case-insensitive but JSON property names aren't, so match existing
    // keys ignoring case rather than adding a second "busybar" next to "BusyBar".
    private static JsonObject Section(JsonObject root, string name)
    {
        var existing = root.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase));
        if (existing.Value is JsonObject section)
        {
            return section;
        }

        section = new JsonObject();
        Set(root, name, section);
        return section;
    }

    private static void Set(JsonObject obj, string key, JsonNode? value)
    {
        var existingKey = obj.Select(p => p.Key)
            .FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        if (existingKey is not null)
        {
            obj.Remove(existingKey);
        }

        if (value is not null)
        {
            obj[key] = value;
        }
    }
}
