using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Qapptia.App.Editor.ViewModels;

public partial class CanvasViewportViewModel : ObservableObject
{
    private string _lastCustomZoom = string.Empty;

    public ObservableCollection<string> ZoomOptions { get; } = new()
    {
        "25%", "50%", "75%", "100%", "125%", "150%", "200%", "300%", "400%", "500%", "700%"
    };

    [ObservableProperty]
    private float _zoomLevel = 1.0f;

    [ObservableProperty]
    private string _selectedZoomString = "100%";

    public event EventHandler? FitImageRequested;

    partial void OnZoomLevelChanged(float value)
    {
        var newStr = $"{(int)Math.Round(value * 100)}%";
        if (!ZoomOptions.Contains(newStr))
        {
            if (!string.IsNullOrEmpty(_lastCustomZoom) && ZoomOptions.Contains(_lastCustomZoom))
            {
                ZoomOptions.Remove(_lastCustomZoom);
            }

            ZoomOptions.Add(newStr);
            _lastCustomZoom = newStr;
        }
        SelectedZoomString = newStr;
    }

    partial void OnSelectedZoomStringChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out int percentage) && percentage > 0)
        {
            var newZoom = percentage / 100.0f;

            // Limitamos entre 10% (0.1f) y 9999% (99.99f)
            newZoom = Math.Max(0.1f, Math.Min(newZoom, 99.99f));
            percentage = (int)Math.Round(newZoom * 100);

            if (Math.Abs(newZoom - ZoomLevel) > 0.01f)
            {
                ZoomLevel = newZoom;
            }
            else if (!value.EndsWith("%", StringComparison.Ordinal) || value != $"{percentage}%")
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SelectedZoomString = $"{percentage}%";
                });
            }
        }
    }

    [RelayCommand]
    public void RealSize()
    {
        ZoomLevel = 1.0f;
    }

    [RelayCommand]
    public void FitImage()
    {
        FitImageRequested?.Invoke(this, EventArgs.Empty);
    }
}
