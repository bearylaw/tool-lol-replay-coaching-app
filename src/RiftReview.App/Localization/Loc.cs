using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace RiftReview.App.Localization;

/// <summary>
/// Every user-facing string comes from <c>Strings.resx</c> through here. A missing key shows as
/// <c>⟦Key⟧</c> on screen; <c>LocalizationTests</c> fails the build before that can ship.
/// </summary>
internal static class Loc
{
    private static readonly ResourceManager Resources = new("RiftReview.App.Localization.Strings", typeof(Loc).Assembly);

    /// <summary>The text for <paramref name="key"/> in the current UI language.</summary>
    public static string Get(string key) =>
        Resources.GetString(key, CultureInfo.CurrentUICulture) ?? $"⟦{key}⟧";

    /// <summary>The text for <paramref name="key"/> with its <c>{0}</c> placeholders filled in.</summary>
    public static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}

/// <summary>XAML access to <see cref="Loc"/>: <c>Text="{loc:Text Key}"</c>.</summary>
[MarkupExtensionReturnType(typeof(string))]
internal sealed class TextExtension(string key) : MarkupExtension
{
    public TextExtension()
        : this(string.Empty)
    {
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
