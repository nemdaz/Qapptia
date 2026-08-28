using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Qapptia.Platform.Windows.UI;

public sealed class ModernTrayMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color HoverColor = Color.FromArgb(234, 234, 234);
    private static readonly Color BackgroundColor = Color.FromArgb(249, 249, 249);
    private static readonly Color SeparatorColor = Color.FromArgb(229, 229, 229);
    private static readonly Color TextColor = Color.Black;

    public ModernTrayMenuRenderer() : base(new ModernColorTable())
    {
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item.Selected && e.Item.Enabled)
        {
            // Windows 11 context menu hover has small margins and rounded corners
            var rect = new Rectangle(4, 2, e.Item.Width - 8, e.Item.Height - 4);
            using var path = GetRoundedRectangle(rect, 4);
            using var brush = new SolidBrush(HoverColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }
        else
        {
            using var brush = new SolidBrush(BackgroundColor);
            e.Graphics.FillRectangle(brush, e.Item.ContentRectangle);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = TextColor;

        // Custom text rendering to support IsDefault (bold)
        var font = e.Item.Font;
        if (e.Item.Tag is Qapptia.Core.Abstractions.TrayMenuActionItem actionItem && actionItem.IsDefault)
        {
            font = new Font(font, FontStyle.Bold);
        }

        e.TextFont = font;

        // Increase text margin to give a modern Windows 11 feel
        var textRect = e.TextRectangle;
        textRect.X += 12;
        e.TextRectangle = textRect;

        e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var y = e.Item.Height / 2;
        using var pen = new Pen(SeparatorColor);
        // Windows 11 separators don't touch the very edges
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // Custom checkmark rendering (fluent style)
        var rect = e.ImageRectangle;
        using var brush = new SolidBrush(TextColor);

        // Segoe Fluent Icons CheckMark
        using var font = new Font("Segoe Fluent Icons", 10, FontStyle.Regular);
        using var stringFormat = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        // uE10B is the checkmark in Segoe Fluent Icons, fallback to standard checkmark
        string checkChar = "\uE10B";

        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        e.Graphics.DrawString(checkChar, font, brush, rect, stringFormat);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Disable the standard ugly left margin 3D gradient
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        // Disable the standard border so DWM handles it
    }

    private static GraphicsPath GetRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        var diameter = radius * 2;
        var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }

    private sealed class ModernColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => BackgroundColor;
        public override Color ImageMarginGradientBegin => BackgroundColor;
        public override Color ImageMarginGradientMiddle => BackgroundColor;
        public override Color ImageMarginGradientEnd => BackgroundColor;
        public override Color MenuBorder => Color.Transparent;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.Transparent;
        public override Color MenuItemSelectedGradientBegin => Color.Transparent;
        public override Color MenuItemSelectedGradientEnd => Color.Transparent;
    }
}
