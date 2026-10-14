using System.Globalization;

namespace RiftReview.Core.Matches;

/// <summary>
/// A League of Legends game build such as <c>16.20.824.8524</c>: patch <c>16.20</c>,
/// then build and revision numbers. A replay plays only on the build that recorded it.
/// </summary>
public readonly record struct GameVersion(int Major, int Minor, int Build, int Revision)
{
    /// <summary>The patch as players name it, for example <c>16.20</c>.</summary>
    public string Patch => string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}");

    /// <summary>
    /// Parses a dotted version with two to four numeric parts. Missing parts are zero.
    /// Returns <c>null</c> for anything else.
    /// </summary>
    public static GameVersion? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var parts = text.Trim().Split('.');
        if (parts.Length is < 2 or > 4)
        {
            return null;
        }

        var numbers = new int[4];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return new GameVersion(numbers[0], numbers[1], numbers[2], numbers[3]);
    }

    /// <summary>Whether both versions belong to the same patch, ignoring build and revision.</summary>
    public bool IsSamePatch(GameVersion other) => Major == other.Major && Minor == other.Minor;

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Build}.{Revision}");
}
