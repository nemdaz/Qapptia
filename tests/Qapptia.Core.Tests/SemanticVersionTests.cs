using System;
using Qapptia.Core.Services;
using Xunit;

namespace Qapptia.Core.Tests;

public class SemanticVersionTests
{
    [Theory]
    [InlineData("1.0.0", 1, 0, 0, null)]
    [InlineData("v2.1.3", 2, 1, 3, null)]
    [InlineData("2.0.0-beta", 2, 0, 0, "beta")]
    [InlineData("2.0.0-rc.1+build.123", 2, 0, 0, "rc.1")]
    [InlineData("0.9.15-alpha.2", 0, 9, 15, "alpha.2")]
    public void TryParseValidSemverSucceeds(string input, int major, int minor, int patch, string? prerelease)
    {
        var success = SemanticVersion.TryParse(input, out var semver);

        Assert.True(success);
        Assert.NotNull(semver);
        Assert.Equal(major, semver.Major);
        Assert.Equal(minor, semver.Minor);
        Assert.Equal(patch, semver.Patch);
        Assert.Equal(prerelease, semver.PreRelease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("a.b.c")]
    public void TryParseInvalidSemverFails(string? input)
    {
        var success = SemanticVersion.TryParse(input, out var semver);

        Assert.False(success);
        Assert.Null(semver);
    }

    [Theory]
    [InlineData("2.0.1", "2.0.0", 1)]
    [InlineData("2.1.0", "2.0.5", 1)]
    [InlineData("3.0.0", "2.9.9", 1)]
    [InlineData("2.0.0", "2.0.0", 0)]
    [InlineData("1.5.0", "1.5.0", 0)]
    [InlineData("1.0.0", "2.0.0", -1)]
    [InlineData("2.0.0", "2.0.0-beta", 1)] // Versión normal es mayor que pre-release
    [InlineData("2.0.0-beta", "2.0.0-alpha", 1)]
    [InlineData("2.0.0-rc.1", "2.0.0-beta.2", 1)]
    [InlineData("2.0.0-alpha", "2.0.0-beta", -1)]
    public void CompareToExpectedRelation(string versionA, string versionB, int expectedSign)
    {
        var parsedA = SemanticVersion.Parse(versionA);
        var parsedB = SemanticVersion.Parse(versionB);

        var comparison = parsedA.CompareTo(parsedB);
        var actualSign = Math.Sign(comparison);

        Assert.Equal(expectedSign, actualSign);
        if (expectedSign > 0)
        {
            Assert.True(parsedA > parsedB);
            Assert.False(parsedA < parsedB);
        }
        else if (expectedSign < 0)
        {
            Assert.True(parsedA < parsedB);
            Assert.False(parsedA > parsedB);
        }
        else
        {
            Assert.Equal(parsedA, parsedB);
        }
    }
}
