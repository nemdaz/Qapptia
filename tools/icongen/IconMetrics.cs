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
    
    // Colores corporativos
    public static readonly SKColor BackgroundColor = SKColor.Parse("#1a365d"); // Azul marino oscuro (claramente azul)
    public static readonly SKColor OutlineColor = SKColor.Parse("#f5f7fa");
    public static readonly SKColor TextColor = SKColor.Parse("#ffffff");
    
    public const string IconText = "Q";

    // Fórmulas geométricas dinámicas basadas en el tamaño del lienzo
    public static int GetPadding(int size) => 0; // Sin padding para aprovechar al máximo
    public static int GetRadius(int size) => Math.Max(4, (int)(size * 0.25)); // Ligeramente más redondeado
    public static int GetBorderWidth(int size) => Math.Max(1, size / 64);
    public static int GetQSize(int size) => (int)(size * 0.90);
    
    // Calcular el Inset del Tray Icon para maximizar visibilidad
    public static int GetTrayIconInset() => 0;

    public static void DrawCustomQ(SKCanvas canvas, float cx, float cy, float size, SKColor color, float outlineThickness = 0f, SKColor? outlineColor = null)
    {
        // El grosor de la línea será 18% del tamaño (para que quede bold y moderno)
        float strokeWidth = size * 0.18f;
        // Radio del círculo principal (escalado al 75% para dejar margen respecto a los bordes)
        float radius = ((size - strokeWidth) / 2f) * 0.75f;

        // Longitud de la muesca en proporción al radio del círculo.
        float tailLength = radius * 0.78f;
        
        // Posicionar la muesca para que el círculo la intercepte exactamente en su mitad (distancia media = radius)
        float startDist = radius - (tailLength / 2f);
        float endDist = radius + (tailLength / 2f);

        double angle = Math.PI / 4;
        float startX = cx + (float)(startDist * Math.Cos(angle));
        float startY = cy + (float)(startDist * Math.Sin(angle));
        
        float endX = cx + (float)(endDist * Math.Cos(angle));
        float endY = cy + (float)(endDist * Math.Sin(angle));

        // Función local para dibujar la Q con un Paint específico
        void RenderQ(SKPaint paint)
        {
            canvas.DrawCircle(cx, cy, radius, paint);
            canvas.DrawLine(startX, startY, endX, endY, paint);
        }

        // Si se especificó un contorno, lo dibujamos primero (más grueso y por debajo)
        if (outlineThickness > 0 && outlineColor.HasValue)
        {
            using var outlinePaint = new SKPaint
            {
                Color = outlineColor.Value,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth + outlineThickness,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };
            RenderQ(outlinePaint);
        }

        // Dibujar la Q principal (encima del contorno, si lo hay)
        using var qPaint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };
        
        RenderQ(qPaint);
    }
}
