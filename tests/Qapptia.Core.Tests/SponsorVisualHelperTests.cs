using System;
using System.Linq;
using Qapptia.Core.Services;
using Xunit;

namespace Qapptia.Core.Tests;

public class SponsorVisualHelperTests
{
    [Fact]
    public void CatalogHasExactlyFourIconsAndFourTexts()
    {
        Assert.Equal(4, SponsorVisualHelper.SponsorIconKeys.Length);
        Assert.Equal(4, SponsorVisualHelper.SponsorTexts.Length);
        Assert.Equal(4, SponsorVisualHelper.SponsorStyleKeys.Length);

        // Iconos no nulos ni vacíos y únicos
        Assert.All(SponsorVisualHelper.SponsorIconKeys, icon => Assert.False(string.IsNullOrWhiteSpace(icon)));
        Assert.Equal(4, SponsorVisualHelper.SponsorIconKeys.Distinct().Count());

        // Textos no nulos ni vacíos y únicos
        Assert.All(SponsorVisualHelper.SponsorTexts, text => Assert.False(string.IsNullOrWhiteSpace(text)));
        Assert.Equal(4, SponsorVisualHelper.SponsorTexts.Distinct().Count());

        // Estilos no nulos ni vacíos y únicos
        Assert.All(SponsorVisualHelper.SponsorStyleKeys, style => Assert.False(string.IsNullOrWhiteSpace(style)));
        Assert.Equal(4, SponsorVisualHelper.SponsorStyleKeys.Distinct().Count());
    }

    [Fact]
    public void SponsorTextsComplyWithWordCountLimits()
    {
        // El requerimiento especifica textos concisos de 1 a 3 palabras
        foreach (var text in SponsorVisualHelper.SponsorTexts)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.InRange(words.Length, 1, 3);
        }
    }

    [Fact]
    public void SponsorTextsDoNotMentionRestrictedTerms()
    {
        foreach (var text in SponsorVisualHelper.SponsorTexts)
        {
            Assert.DoesNotContain("Qapptia", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Donar", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("propina", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GetRandomCombinationProducesDiverseCombinations()
    {
        var icons = new System.Collections.Generic.HashSet<string>();
        var texts = new System.Collections.Generic.HashSet<string>();
        var styles = new System.Collections.Generic.HashSet<string>();

        for (int i = 0; i < 50; i++)
        {
            var (iconKey, text, styleKey) = SponsorVisualHelper.GetRandomCombination();
            icons.Add(iconKey);
            texts.Add(text);
            styles.Add(styleKey);
        }

        // Verifica que la aleatoriedad distribuya y cubra múltiples variantes
        Assert.True(icons.Count > 1, "Debe generar más de un icono distinto en 50 muestras");
        Assert.True(texts.Count > 1, "Debe generar más de un texto distinto en 50 muestras");
        Assert.True(styles.Count > 1, "Debe generar más de un estilo distinto en 50 muestras");
    }

    [Fact]
    public void GetRandomCombinationReturnsValidCatalogElements()
    {
        for (int i = 0; i < 50; i++)
        {
            var (iconKey, text, styleKey) = SponsorVisualHelper.GetRandomCombination();

            Assert.Contains(iconKey, SponsorVisualHelper.SponsorIconKeys);
            Assert.Contains(text, SponsorVisualHelper.SponsorTexts);
            Assert.Contains(styleKey, SponsorVisualHelper.SponsorStyleKeys);
        }
    }

}
