using System.Text.Json;
using System.Text.Json.Nodes;
using RiftReview.Core.Logging;

namespace RiftReview.App.Settings;

/// <summary>
/// The coach's notes per replay, in <c>%APPDATA%\RiftReview\notes.json</c>. A note is keyed by the
/// replay's platform and game id when the file has the client's name, else by file name, so it
/// follows the replay when the file moves.
/// </summary>
internal sealed class NotesStore
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string filePath;
    private readonly AppLog log;
    private readonly Dictionary<string, string> notes;

    public NotesStore(string filePath, AppLog log)
    {
        this.filePath = filePath;
        this.log = log;
        notes = Load();
    }

    /// <summary>The note for a replay, empty when there is none.</summary>
    public string Get(string key) => notes.GetValueOrDefault(key, string.Empty);

    /// <summary>Stores a note and writes the file; an empty note removes the entry.</summary>
    public void Set(string key, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (!notes.Remove(key))
            {
                return;
            }
        }
        else if (notes.GetValueOrDefault(key) == text)
        {
            return;
        }
        else
        {
            notes[key] = text;
        }

        var root = new JsonObject
        {
            ["version"] = CurrentVersion,
            ["notes"] = new JsonObject(notes.Select(n => KeyValuePair.Create(n.Key, (JsonNode?)JsonValue.Create(n.Value)))),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + ".tmp";
        File.WriteAllText(temporary, root.ToJsonString(Json));
        File.Move(temporary, filePath, overwrite: true);
    }

    private Dictionary<string, string> Load()
    {
        var loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
        {
            return loaded;
        }

        try
        {
            var root = JsonNode.Parse(File.ReadAllText(filePath))?.AsObject();
            if (root?["notes"] is JsonObject saved)
            {
                foreach (var (key, value) in saved)
                {
                    if (value?.GetValue<string>() is { } text)
                    {
                        loaded[key] = text;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            // Keep the unreadable file for the user; never overwrite their notes with an empty set.
            log.Error("notes.unreadable", exception, ("path", filePath));
            File.Copy(filePath, filePath + ".broken", overwrite: true);
        }

        return loaded;
    }
}
