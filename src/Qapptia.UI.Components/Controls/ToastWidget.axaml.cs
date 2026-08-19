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
            ToastNotificationType.Success => Brush.Parse("#d1e7dd"),
            ToastNotificationType.Error => Brush.Parse("#f8d7da"),
            ToastNotificationType.Warning => Brush.Parse("#fff3cd"),
            ToastNotificationType.Info => Brush.Parse("#cff4fc"),
            _ => Brush.Parse("#d1e7dd")
        };

        CurrentForegroundBrush = Type switch
        {
            ToastNotificationType.Success => Brush.Parse("#0f5132"),
            ToastNotificationType.Error => Brush.Parse("#842029"),
            ToastNotificationType.Warning => Brush.Parse("#664d03"),
            ToastNotificationType.Info => Brush.Parse("#055160"),
            _ => Brush.Parse("#0f5132")
        };

        CurrentBorderBrush = Type switch
        {
            ToastNotificationType.Success => Brush.Parse("#badbcc"),
            ToastNotificationType.Error => Brush.Parse("#f5c2c7"),
            ToastNotificationType.Warning => Brush.Parse("#ffecb5"),
            ToastNotificationType.Info => Brush.Parse("#b6effb"),
            _ => Brush.Parse("#badbcc")
        };
    }
}
