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

    public TrayMenuActionItem(string text, Action onClick)
    {
        Text = text;
        OnClick = onClick;
    }
}

public sealed class TrayMenuSeparatorItem : TrayMenuItem { }
