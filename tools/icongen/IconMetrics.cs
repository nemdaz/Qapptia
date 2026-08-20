using SkiaSharp;
using System;
using System.Collections.Generic;

namespace icongen;

// Fuente de verdad para métricas y colores de los iconos (Desktop/Tray).
public static class IconMetrics
{
    public const int AppIconMasterSize = 256;
    
    // IMPORTANT: Orden descendente requerido para evitar pixelación en Windows.
    public static readonly IReadOnlyList<int> AppWindowIconSizes = new[] { 256, 128, 64, 48, 40, 32, 24, 20, 16 };
    
    // Colores corporativos (definición legacy)
    public static readonly SKColor BackgroundColor = SKColor.Parse("#1f2933");
    public static readonly SKColor OutlineColor = SKColor.Parse("#f5f7fa");
    public static readonly SKColor TextColor = SKColor.Parse("#ffffff");
    
    public const string IconText = "Q";

    // Fórmulas geométricas dinámicas basadas en el tamaño del lienzo
    public static int GetPadding(int size) => Math.Max(1, size / 16);
    public static int GetRadius(int size) => Math.Max(4, size / 5);
    public static int GetBorderWidth(int size) => Math.Max(1, size / 18);
    public static int GetFontSize(int size) => (int)(size * 0.62);
    
    // Calcular el Inset del Tray Icon para maximizar visibilidad
    public static int GetTrayIconInset() => 0;
}
