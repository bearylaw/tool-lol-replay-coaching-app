using RiftReview.App.Settings;
using RiftReview.Core.Logging;

namespace RiftReview.App.Tests;

public sealed class NotesStoreTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "riftreview-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string FilePath => Path.Combine(folder, "notes.json");

    [Fact]
    public void KeepsNotesAcrossSessions()
    {
        Store().Set("sample-4", "Jungle path at 3:10 gave up the scuttle.");

        Assert.Equal("Jungle path at 3:10 gave up the scuttle.", Store().Get("sample-4"));
    }

    [Fact]
    public void RemovesANoteWhenItIsCleared()
    {
        Store().Set("EUW1-1", "first");
        Store().Set("EUW1-1", "  ");

        Assert.Equal(string.Empty, Store().Get("EUW1-1"));
    }

    [Fact]
    public void NeverOverwritesAnUnreadableFile()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, "{ broken");

        Store().Set("EUW1-2", "new note");

        Assert.Equal("{ broken", File.ReadAllText(FilePath + ".broken"));
        Assert.Equal("new note", Store().Get("EUW1-2"));
    }

    private NotesStore Store() => new(FilePath, new AppLog(new StringWriter(), TimeProvider.System));
}
