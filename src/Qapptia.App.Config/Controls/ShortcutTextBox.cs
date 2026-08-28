using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace Qapptia.App.Config.Controls;

public class ShortcutTextBox : TextBox
{
    private List<string> _recordedKeys = new();
    private string _previousValue = string.Empty;

    protected override System.Type StyleKeyOverride => typeof(TextBox);

    public ShortcutTextBox()
    {
        PlaceholderText = "Presiona combinación (ej. Ctrl+Shift+A)";
        GotFocus += (s, e) =>
        {
            _previousValue = Text ?? string.Empty;
            _recordedKeys.Clear();
            Text = string.Empty;
        };

        LostFocus += (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                Text = _previousValue;
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Tab || e.Key == Key.Enter || e.Key == Key.Escape)
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;

        if (e.Key == Key.Back)
        {
            _recordedKeys.Clear();
            Text = string.Empty;
            return;
        }

        var keyName = NormalizeKey(e.KeyModifiers, e.Key);

        if (!string.IsNullOrEmpty(keyName) && !_recordedKeys.Contains(keyName))
        {
            if (_recordedKeys.Count >= 3) // Max 3 keys
                return;

            _recordedKeys.Add(keyName);
            Text = string.Join("+", _recordedKeys);
        }
    }

    private static string NormalizeKey(KeyModifiers modifiers, Key key)
    {
        // Solo guardar las teclas individuales según llegan, o procesar si es un modificador
        if (key == Key.LeftCtrl || key == Key.RightCtrl) return "ctrl";
        if (key == Key.LeftShift || key == Key.RightShift) return "shift";
        if (key == Key.LeftAlt || key == Key.RightAlt) return "alt";
        if (key == Key.LWin || key == Key.RWin) return "win";

        // Letras y números
        if (key >= Key.A && key <= Key.Z) return key.ToString().ToLowerInvariant();
        if (key >= Key.D0 && key <= Key.D9)
            return (key - Key.D0).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return (key - Key.NumPad0).ToString(System.Globalization.CultureInfo.InvariantCulture);

        return string.Empty;
    }
}
