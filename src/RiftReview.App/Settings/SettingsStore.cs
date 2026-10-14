using System.Text.Json;
using System.Text.Json.Nodes;
using RiftReview.Core.Logging;

namespace RiftReview.App.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as versioned JSON. Older versions are migrated step
/// by step; an unreadable file is kept as <c>settings.json.broken</c> and defaults are used.
/// </summary>
internal sealed class SettingsStore(string filePath, AppLog log)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions Sections = new(JsonSerializerDefaults.Web);

    /// <summary>Reads the settings, or returns <paramref name="defaults"/> when there are none yet.</summary>
    public AppSettings Load(AppSettings defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        if (!File.Exists(filePath))
        {
            return defaults;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(filePath))?.AsObject()
                ?? throw new JsonException("settings file is empty");
            return Parse(Migrate(root), defaults).Normalized();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            log.Error("settings.unreadable", exception, ("path", filePath));
            File.Copy(filePath, filePath + ".broken", overwrite: true);
            return defaults;
        }
    }

    /// <summary>Writes the settings atomically (temporary file, then replace).</summary>
    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var root = new JsonObject
        {
            ["version"] = AppSettings.CurrentVersion,
            ["ownReplaysFolder"] = settings.OwnReplaysFolder,
            ["playerReplaysFolder"] = settings.PlayerReplaysFolder,
            ["leagueInstallOverride"] = settings.LeagueInstallOverride,
            ["roster"] = new JsonArray([.. settings.Roster.Select(id => (JsonNode?)JsonValue.Create(id))]),
            ["interfaceScale"] = settings.InterfaceScale,
            ["overlay"] = JsonSerializer.SerializeToNode(settings.Overlay, Sections),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + ".tmp";
        File.WriteAllText(temporary, root.ToJsonString(Json));
        File.Move(temporary, filePath, overwrite: true);
    }

    /// <summary>Brings an older settings document up to <see cref="AppSettings.CurrentVersion"/>.</summary>
    internal static JsonObject Migrate(JsonObject root)
    {
        var version = root["version"]?.GetValue<int>() ?? 0;
        if (version > AppSettings.CurrentVersion)
        {
            throw new FormatException($"settings version {version} is newer than this app ({AppSettings.CurrentVersion})");
        }

        // Version 0 (no version field) had the same keys as version 1. Version 2 added the
        // interface scale and the overlay section; missing ones take their defaults.
        root["version"] = AppSettings.CurrentVersion;
        return root;
    }

    private AppSettings Parse(JsonObject root, AppSettings defaults) => new()
    {
        OwnReplaysFolder = NonEmpty(root["ownReplaysFolder"]) ?? defaults.OwnReplaysFolder,
        PlayerReplaysFolder = NonEmpty(root["playerReplaysFolder"]) ?? defaults.PlayerReplaysFolder,
        LeagueInstallOverride = NonEmpty(root["leagueInstallOverride"]),
        Roster = root["roster"] is JsonArray roster
            ? [.. roster.Select(NonEmpty).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase)]
            : [],
        InterfaceScale = root["interfaceScale"]?.GetValue<double>() ?? defaults.InterfaceScale,
        Overlay = Section(root, "overlay", defaults.Overlay),
    };

    // A section that cannot be read falls back to its defaults alone; the folders and roster stay.
    private T Section<T>(JsonObject root, string name, T fallback)
        where T : class
    {
        try
        {
            return root[name] is { } node ? node.Deserialize<T>(Sections) ?? fallback : fallback;
        }
        catch (JsonException exception)
        {
            log.Error("settings.section_unreadable", exception, ("section", name));
            return fallback;
        }
    }

    private static string? NonEmpty(JsonNode? node) =>
        node?.GetValue<string>() is { } text && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
}
