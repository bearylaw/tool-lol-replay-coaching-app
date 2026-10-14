using System.Windows.Media;
using System.Windows.Media.Imaging;
using RiftReview.Core.Logging;
using RiftReview.GameData;

namespace RiftReview.App.Replays;

/// <summary>Turns Data Dragon icon paths into frozen WPF images, each loaded once per session.</summary>
internal sealed class IconProvider(GameDataService gameData, AppLog log)
{
    // Only touched from the UI thread.
    private readonly Dictionary<string, Task<ImageSource?>> images = new(StringComparer.Ordinal);

    /// <summary>The icon, or <c>null</c> when it cannot be fetched; callers then show text.</summary>
    public Task<ImageSource?> GetAsync(string cdnPath)
    {
        if (!images.TryGetValue(cdnPath, out var image))
        {
            image = LoadAsync(cdnPath);
            images[cdnPath] = image;
        }

        return image;
    }

    /// <summary>The cached file of a Data Dragon picture, downloading it on first use.</summary>
    public Task<string?> GetIconFileAsync(string cdnPath) => gameData.GetIconFileAsync(cdnPath);

    /// <summary>The Data Dragon catalog for a replay's patch.</summary>
    public Task<GameCatalog?> GetCatalogAsync(Core.Matches.GameVersion version) => gameData.GetCatalogAsync(version);


    private async Task<ImageSource?> LoadAsync(string cdnPath)
    {
        var file = await gameData.GetIconFileAsync(cdnPath).ConfigureAwait(false);
        if (file is null)
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(file, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or FileFormatException)
        {
            // A corrupt cached file: delete it so the next session downloads it again.
            log.Warning("icon.corrupt", exception, ("path", file));
            File.Delete(file);
            return null;
        }
    }
}
