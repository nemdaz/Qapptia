using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;
using Avalonia.Input;
using Qapptia.App.Editor.ViewModels;

namespace Qapptia.App.Editor.Controls;

public partial class EditorToolbarWidget : UserControl
{
    public EditorToolbarWidget()
    {
        InitializeComponent();
        InitializeZoomComboBoxHandlers();
    }

    private void InitializeZoomComboBoxHandlers()
    {
        var zoomCombo = this.FindControl<ComboBox>("ZoomComboBox");
        if (zoomCombo == null) return;

        zoomCombo.AddHandler(InputElement.TextInputEvent, (s, e) =>
        {
            if (string.IsNullOrEmpty(e.Text)) return;

            var tb = zoomCombo.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (tb != null)
            {
                int currentDigits = tb.Text?.Count(char.IsDigit) ?? 0;
                int incomingDigits = e.Text.Count(char.IsDigit);
                string selectedText = tb.SelectedText ?? "";
                int selectedDigits = selectedText.Count(char.IsDigit);
                
                if (currentDigits - selectedDigits + incomingDigits > 4)
                {
                    e.Handled = true;
                    return;
                }
            }

            foreach (char c in e.Text)
            {
                if (!char.IsDigit(c) && c != '%')
                {
                    e.Handled = true;
                    return;
                }
            }
        }, RoutingStrategies.Tunnel);

        // Al presionar Enter, forzamos la actualización
        zoomCombo.AddHandler(InputElement.KeyDownEvent, (s, e) =>
        {
            if (e.Key == Key.Enter && s is ComboBox cb)
            {
                var tb = cb.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
                if (tb != null && DataContext is EditorViewModel vm)
                {
                    vm.SelectedZoomString = tb.Text ?? string.Empty;
                    
                    // Cerrar el combo si está desplegado
                    cb.IsDropDownOpen = false;
                    
                    // Remover el foco para confirmar la selección visualmente
                    this.Focusable = true;
                    this.Focus();
                    e.Handled = true;
                }
            }
        }, RoutingStrategies.Tunnel);
    }
}
