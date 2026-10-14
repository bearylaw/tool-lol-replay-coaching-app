namespace RiftReview.Core.Rofl;

/// <summary>Why a file could not be read as a replay.</summary>
public enum RoflErrorKind
{
    /// <summary>The file does not start with Riot's replay signature.</summary>
    NotAReplay,

    /// <summary>A replay in a container version this app does not read, such as the pre-ROFL2 format.</summary>
    UnsupportedFormat,

    /// <summary>The file ends before the parts the format requires.</summary>
    Truncated,

    /// <summary>The header or metadata is present but does not have the expected content.</summary>
    InvalidMetadata,

    /// <summary>The file could not be opened, for example because it is still being downloaded.</summary>
    Unreadable,
}

/// <summary>A replay read failure with an English detail for the log.</summary>
public sealed record RoflError(RoflErrorKind Kind, string Detail);
