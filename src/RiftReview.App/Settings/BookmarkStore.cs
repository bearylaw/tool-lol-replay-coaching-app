using System.Text.Json;
using RiftReview.Core.Logging;

namespace RiftReview.App.Settings;

/// <summary>A moment of a replay the coach marked, with a note.</summary>
/// <param name="Id">Stable identity, for editing and deleting.</param>
/// <param name="Time">Game time in seconds.</param>
/// <param name="Text">The note; may be empty.</param>
/// <param name="CreatedUtc">When the bookmark was made.</param>
/// <param name="Drawing">What the coach had drawn when making it, shown again on a jump to it.</param>
internal sealed record Bookmark(Guid Id, double Time, string Text, DateTimeOffset CreatedUtc, BookmarkDrawing? Drawing = null);

/// <summary>
/// One stroke of a saved drawing. The points are x, y pairs as shares of the overlay's width and
/// height, so the drawing fits a game window of another size; the width is a share of the height.
/// </summary>
/// <param name="Points">x0, y0, x1, y1, … between 0 and 1.</param>
/// <param name="Colour">#AARRGGBB; the highlighter's is translucent.</param>
/// <param name="Width">Line width as a share of the overlay's height.</param>
internal sealed record SavedStroke(double[] Points, string Colour, double Width);

/// <summary>A drawing kept with a bookmark, and where League's camera was so it lines up again.</summary>
internal sealed record BookmarkDrawing(SavedStroke[] Strokes, RiftReview.Capture.CameraView? Camera);

/// <summary>
/// The coach's bookmarks per replay, in <c>%APPDATA%\RiftReview\bookmarks.json</c>, keyed like the
/// notes (platform and game id, else file name). Each change writes the file at once, through a
/// temporary file, so a crash never loses more than the bookmark being made.
/// </summary>
internal sealed class BookmarkStore
{
    private const int CurrentVersion = 1;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string filePath;
    private readonly AppLog log;
    private readonly Dictionary<string, List<Bookmark>> replays;

    public BookmarkStore(string filePath, AppLog log)
    {
        this.filePath = filePath;
        this.log = log;
        replays = Load();
    }

    /// <summary>Raised after the bookmarks of a replay changed, with the replay's key.</summary>
    public event EventHandler<string>? Changed;

    /// <summary>A replay's bookmarks in time order.</summary>
    public IReadOnlyList<Bookmark> Get(string key) => [.. replays.GetValueOrDefault(key, []).OrderBy(b => b.Time)];

    /// <summary>Adds a bookmark or replaces the one with the same id.</summary>
    public void Put(string key, Bookmark bookmark)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        if (!replays.TryGetValue(key, out var list))
        {
            list = [];
            replays[key] = list;
        }

        list.RemoveAll(b => b.Id == bookmark.Id);
        list.Add(bookmark);
        Save(key);
    }

    /// <summary>Removes a bookmark.</summary>
    public void Remove(string key, Guid id)
    {
        if (replays.TryGetValue(key, out var list) && list.RemoveAll(b => b.Id == id) > 0)
        {
            if (list.Count == 0)
            {
                replays.Remove(key);
            }

            Save(key);
        }
    }

    private void Save(string key)
    {
        var file = new BookmarkFile(CurrentVersion, replays.ToDictionary(r => r.Key, r => r.Value.OrderBy(b => b.Time).ToArray()));
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(file, Json));
        File.Move(temporary, filePath, overwrite: true);
        Changed?.Invoke(this, key);
    }

    private Dictionary<string, List<Bookmark>> Load()
    {
        var loaded = new Dictionary<string, List<Bookmark>>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(filePath))
        {
            return loaded;
        }

        try
        {
            var file = JsonSerializer.Deserialize<BookmarkFile>(File.ReadAllText(filePath), Json);
            foreach (var (key, list) in file?.Replays ?? [])
            {
                loaded[key] = [.. list.Where(b => b is not null && double.IsFinite(b.Time)).Select(b => b with { Text = b.Text ?? string.Empty })];
            }
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            // Keep the unreadable file for the user; never overwrite their bookmarks with an empty set.
            log.Error("bookmarks.unreadable", exception, ("path", filePath));
            File.Copy(filePath, filePath + ".broken", overwrite: true);
        }

        return loaded;
    }

    private sealed record BookmarkFile(int Version, Dictionary<string, Bookmark[]> Replays);
}
