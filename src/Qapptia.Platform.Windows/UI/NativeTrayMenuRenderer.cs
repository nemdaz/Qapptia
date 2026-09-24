using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Qapptia.Core.Abstractions;
using Qapptia.Core.Extensions;

namespace Qapptia.Platform.Windows.UI;

/// <summary>
/// Responsable exclusivo de la construcción, estilo y renderizado visual del menú contextual de la bandeja de sistema.
/// </summary>
public static class NativeTrayMenuRenderer
{
    private const uint MF_STRING = 0x0000;
    private const uint MF_CHECKED = 0x0008;
    private const uint MF_SEPARATOR = 0x0800;

    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    private const uint WM_NULL = 0x0000;

    /// <summary>
    /// Construye y despliega el menú contextual Win32 en las coordenadas actuales del puntero del ratón.
    /// </summary>
    public static void Show(IntPtr ownerHwnd, TrayMenuDefinition menuDefinition)
    {
        if (ownerHwnd == IntPtr.Zero || menuDefinition == null) return;

        IntPtr hMenu = CreatePopupMenu();
        if (hMenu == IntPtr.Zero) return;

        try
        {
            var actionMap = new Dictionary<uint, TrayMenuActionItem>();
            uint cmdId = 1000;

            foreach (var item in menuDefinition.Items)
            {
                if (item is TrayMenuSeparatorItem)
                {
                    AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, null);
                }
                else if (item is TrayMenuActionItem action)
                {
                    uint currentId = cmdId++;
                    actionMap[currentId] = action;

                    string label = action.Text;
                    if (action.ShortcutTextProvider != null)
                    {
                        var rawShortcut = action.ShortcutTextProvider() ?? "";
                        if (!string.IsNullOrWhiteSpace(rawShortcut))
                        {
                            label = $"{label}\t{rawShortcut.ToShortcutTitleCase()}";
                        }
                    }

                    uint flags = MF_STRING;
                    if (action.IsChecked) flags |= MF_CHECKED;

                    AppendMenu(hMenu, flags, (UIntPtr)currentId, label);

                    if (action.IsDefault)
                    {
                        SetMenuDefaultItem(hMenu, currentId, false);
                    }
                }
            }

            GetCursorPos(out POINT pt);
            SetForegroundWindow(ownerHwnd);

            uint selected = TrackPopupMenuEx(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, ownerHwnd, IntPtr.Zero);
            PostMessage(ownerHwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

            if (selected > 0 && actionMap.TryGetValue(selected, out var selectedAction))
            {
                Task.Run(() => selectedAction.OnClick?.Invoke());
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    /// <summary>
    /// Ejecuta la acción por defecto del menú contextual (e.g. doble clic o clic principal en el icono de bandeja).
    /// </summary>
    public static void ExecuteDefaultAction(TrayMenuDefinition menuDefinition)
    {
        if (menuDefinition == null) return;

        var defaultAction = menuDefinition.Items.OfType<TrayMenuActionItem>().FirstOrDefault(a => a.IsDefault)
            ?? menuDefinition.Items.OfType<TrayMenuActionItem>().FirstOrDefault();

        if (defaultAction != null)
        {
            Task.Run(() => defaultAction.OnClick?.Invoke());
        }
    }

    #region Win32 Menu P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool SetMenuDefaultItem(IntPtr hMenu, uint uItem, bool fByPos);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    #endregion
}
