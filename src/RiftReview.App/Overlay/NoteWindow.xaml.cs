using System.Windows;
using System.Windows.Input;
using RiftReview.App.Replays;

namespace RiftReview.App.Overlay;

/// <summary>What the note box returned: the note and the chosen tag's id.</summary>
internal sealed record NoteResult(string Text, string? Tag);

/// <summary>
/// The note box of a bookmark. Unlike the overlay it takes the keyboard while it is open, so the
/// coach can type; closing it hands the keyboard back to the game.
/// </summary>
internal sealed partial class NoteWindow : Window
{
    private readonly TagChoices tags;
    private bool saved;

    public NoteWindow(string heading, string text, TagChoices tags)
    {
        InitializeComponent();
        this.tags = tags;
        TagList.DataContext = tags;
        Heading.Text = heading;
        Note.Text = text;
        Loaded += (_, _) =>
        {
            Activate();
            Note.Focus();
            Note.CaretIndex = Note.Text.Length;
        };

        // Clicking back into the game ends the note as if saved; nothing typed is lost.
        Deactivated += (_, _) =>
        {
            if (IsVisible)
            {
                Finish(save: true);
            }
        };
    }

    /// <summary>Raised once when the box closes: the note and tag, or <c>null</c> when cancelled.</summary>
    public event EventHandler<NoteResult?>? Done;

    private void OnNoteKey(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is >= Key.D1 and <= Key.D9)
        {
            tags.Toggle(e.Key - Key.D1);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Finish(save: false);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            var at = Note.CaretIndex;
            Note.Text = Note.Text.Insert(at, Environment.NewLine);
            Note.CaretIndex = at + Environment.NewLine.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Finish(save: true);
            e.Handled = true;
        }
    }

    private void OnSave(object sender, RoutedEventArgs e) => Finish(save: true);

    private void OnCancel(object sender, RoutedEventArgs e) => Finish(save: false);

    private void Finish(bool save)
    {
        if (saved)
        {
            return;
        }

        saved = true;
        Done?.Invoke(this, save ? new NoteResult(Note.Text, tags.Selected) : null);
        Close();
    }
}
