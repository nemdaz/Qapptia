using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace Qapptia.UI.Components.Controls;

public enum ToastNotificationType
{
    Success,
    Error,
    Warning,
    Info
}

public partial class ToastWidget : UserControl
{
    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<ToastWidget, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<ToastNotificationType> TypeProperty =
        AvaloniaProperty.Register<ToastWidget, ToastNotificationType>(nameof(Type), ToastNotificationType.Success);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ToastWidget, bool>(nameof(IsOpen), false);

    public static readonly StyledProperty<IBrush> CurrentBackgroundBrushProperty =
        AvaloniaProperty.Register<ToastWidget, IBrush>(nameof(CurrentBackgroundBrush), Brushes.White);

    public static readonly StyledProperty<IBrush> CurrentForegroundBrushProperty =
        AvaloniaProperty.Register<ToastWidget, IBrush>(nameof(CurrentForegroundBrush), Brushes.Black);

    public static readonly StyledProperty<IBrush> CurrentBorderBrushProperty =
        AvaloniaProperty.Register<ToastWidget, IBrush>(nameof(CurrentBorderBrush), Brushes.Gray);

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public ToastNotificationType Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public IBrush CurrentBackgroundBrush
    {
        get => GetValue(CurrentBackgroundBrushProperty);
        private set => SetValue(CurrentBackgroundBrushProperty, value);
    }

    public IBrush CurrentForegroundBrush
    {
        get => GetValue(CurrentForegroundBrushProperty);
        private set => SetValue(CurrentForegroundBrushProperty, value);
    }

    public IBrush CurrentBorderBrush
    {
        get => GetValue(CurrentBorderBrushProperty);
        private set => SetValue(CurrentBorderBrushProperty, value);
    }

    public ToastWidget()
    {
        InitializeComponent();
        UpdateColors();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TypeProperty)
        {
            UpdateColors();
        }
    }

    private void UpdateColors()
    {
        CurrentBackgroundBrush = Type switch
        {
            ToastNotificationType.Success => Brush.Parse("#43a047"), // Material Green 600
            ToastNotificationType.Error => Brush.Parse("#e53935"),   // Material Red 600
            ToastNotificationType.Warning => Brush.Parse("#ffb300"), // Material Amber 600
            ToastNotificationType.Info => Brush.Parse("#1e88e5"),    // Material Blue 600
            _ => Brush.Parse("#43a047")
        };

        CurrentForegroundBrush = Type switch
        {
            ToastNotificationType.Success => Brush.Parse("#ffffff"),
            ToastNotificationType.Error => Brush.Parse("#ffffff"),
            ToastNotificationType.Warning => Brush.Parse("#1a1a1a"), // Dark text for amber bg
            ToastNotificationType.Info => Brush.Parse("#ffffff"),
            _ => Brush.Parse("#ffffff")
        };

        CurrentBorderBrush = Type switch
        {
            ToastNotificationType.Success => Brush.Parse("#2e7d32"), // Material Green 800
            ToastNotificationType.Error => Brush.Parse("#c62828"),   // Material Red 800
            ToastNotificationType.Warning => Brush.Parse("#ff8f00"), // Material Amber 800
            ToastNotificationType.Info => Brush.Parse("#1565c0"),    // Material Blue 800
            _ => Brush.Parse("#2e7d32")
        };
    }
}
