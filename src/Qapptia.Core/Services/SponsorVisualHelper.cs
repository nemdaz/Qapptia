using System;

namespace Qapptia.Core.Services;

/// <summary>
/// Información visual de apoyo y contribución generada aleatoriamente (icono, texto y clave de estilo).
/// </summary>
public readonly record struct SponsorVisualInfo(string IconKey, string Text, string StyleKey);

/// <summary>
/// Catálogo y despachador de combinaciones aleatorias de iconos, textos y estilos de apoyo.
/// Produce combinaciones dinámicas (4 iconos x 4 textos x 4 estilos = 64 combinaciones) al iniciar las vistas.
/// </summary>
public static class SponsorVisualHelper
{
    public const string StyleA = Constants.SponsorStyleA;
    public const string StyleB = Constants.SponsorStyleB;
    public const string StyleC = Constants.SponsorStyleC;
    public const string StyleD = Constants.SponsorStyleD;

    public static readonly string[] SponsorIconKeys =
    [
        Constants.SponsorIcon1,
        Constants.SponsorIcon2,
        Constants.SponsorIcon3,
        Constants.SponsorIcon4
    ];

    public static readonly string[] SponsorTexts =
    [
        Constants.SponsorText1,
        Constants.SponsorText2,
        Constants.SponsorText3,
        Constants.SponsorText4
    ];

    public static readonly string[] SponsorStyleKeys =
    [
        StyleA,
        StyleB,
        StyleC,
        StyleD
    ];

    /// <summary>
    /// Selecciona aleatoriamente una combinación de icono, texto y set de estilo de apoyo.
    /// </summary>
    public static SponsorVisualInfo GetRandomCombination()
    {
        var random = Random.Shared;
        var iconKey = SponsorIconKeys[random.Next(SponsorIconKeys.Length)];
        var text = SponsorTexts[random.Next(SponsorTexts.Length)];
        var styleKey = SponsorStyleKeys[random.Next(SponsorStyleKeys.Length)];
        return new SponsorVisualInfo(iconKey, text, styleKey);
    }
}
