using Qapptia.Core.Theme;
using Xunit;

namespace Qapptia.Core.Tests;

public class ThemeConstantsTests
{
    [Theory]
    [InlineData("dark", ThemeConstants.Dark)]
    [InlineData("DARK", ThemeConstants.Dark)]
    [InlineData("light", ThemeConstants.Light)]
    [InlineData("LIGHT", ThemeConstants.Light)]
    [InlineData("system", ThemeConstants.System)]
    [InlineData("SYSTEM", ThemeConstants.System)]
    [InlineData("", ThemeConstants.System)]
    [InlineData(null, ThemeConstants.System)]
    [InlineData("unknown", ThemeConstants.System)]
    public void NormalizeReturnsExpectedTheme(string? input, string expected)
    {
        var result = ThemeConstants.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ThemeConstants.Dark, ThemeConstants.DisplayNameDark)]
    [InlineData(ThemeConstants.Light, ThemeConstants.DisplayNameLight)]
    [InlineData(ThemeConstants.System, ThemeConstants.DisplayNameSystem)]
    [InlineData(null, ThemeConstants.DisplayNameSystem)]
    [InlineData("other", ThemeConstants.DisplayNameSystem)]
    public void ToDisplayNameReturnsExpectedDisplayName(string? code, string expected)
    {
        var result = ThemeConstants.ToDisplayName(code);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(ThemeConstants.DisplayNameDark, ThemeConstants.Dark)]
    [InlineData(ThemeConstants.DisplayNameLight, ThemeConstants.Light)]
    [InlineData(ThemeConstants.DisplayNameSystem, ThemeConstants.System)]
    [InlineData("", ThemeConstants.System)]
    [InlineData(null, ThemeConstants.System)]
    [InlineData("Otro", ThemeConstants.System)]
    public void FromDisplayNameReturnsExpectedCode(string? displayName, string expected)
    {
        var result = ThemeConstants.FromDisplayName(displayName);
        Assert.Equal(expected, result);
    }
}
