namespace Qapptia.Core.Abstractions;

public sealed class TrayMenuDefinition
{
    public List<TrayMenuItem> Items { get; } = new();
}

public abstract class TrayMenuItem { }

public sealed class TrayMenuActionItem : TrayMenuItem
{
    private readonly string _text;
    private readonly Func<string?>? _textProvider;

    public string Text => _textProvider?.Invoke() ?? _text;
    public Action OnClick { get; }
    public bool IsDefault { get; }
    public bool IsChecked { get; set; }
    public Func<string?>? ShortcutTextProvider { get; }

    public TrayMenuActionItem(
        string text,
        Action onClick,
        bool isDefault = false,
        bool isChecked = false,
        Func<string?>? shortcutTextProvider = null,
        Func<string?>? textProvider = null)
    {
        _text = text;
        OnClick = onClick;
        IsDefault = isDefault;
        IsChecked = isChecked;
        ShortcutTextProvider = shortcutTextProvider;
        _textProvider = textProvider;
    }
}

public sealed class TrayMenuSeparatorItem : TrayMenuItem { }
