namespace Qapptia.Core.Abstractions;

public sealed class TrayMenuDefinition
{
    public List<TrayMenuItem> Items { get; } = new();
}

public abstract class TrayMenuItem { }

public sealed class TrayMenuActionItem : TrayMenuItem
{
    public string Text { get; }
    public Action OnClick { get; }
    public bool IsDefault { get; }
    public bool IsChecked { get; set; }
    public Func<string?>? ShortcutTextProvider { get; }

    public TrayMenuActionItem(string text, Action onClick, bool isDefault = false, bool isChecked = false, Func<string?>? shortcutTextProvider = null)
    {
        Text = text;
        OnClick = onClick;
        IsDefault = isDefault;
        IsChecked = isChecked;
        ShortcutTextProvider = shortcutTextProvider;
    }
}

public sealed class TrayMenuSeparatorItem : TrayMenuItem { }
