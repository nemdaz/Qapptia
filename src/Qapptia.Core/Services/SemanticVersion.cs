using System;
using System.Diagnostics.CodeAnalysis;

namespace Qapptia.Core.Services;

/// <summary>
/// Representación inmutable y parser de versiones bajo la especificación Semantic Versioning 2.0.0.
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }

    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    public SemanticVersion(int major, int minor, int patch, string? preRelease = null)
    {
        if (major < 0) throw new ArgumentOutOfRangeException(nameof(major), "Major no puede ser negativo.");
        if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor), "Minor no puede ser negativo.");
        if (patch < 0) throw new ArgumentOutOfRangeException(nameof(patch), "Patch no puede ser negativo.");

        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = string.IsNullOrWhiteSpace(preRelease) ? null : preRelease.Trim();
    }

    public static bool TryParse(string? versionText, [NotNullWhen(true)] out SemanticVersion? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(versionText)) return false;

        var text = versionText.Trim();
        if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            text = text[1..];
        }

        // SemVer 2.0.0: los metadatos de compilación comienzan con '+' y se descartan para precedencia
        var plusIndex = text.IndexOf('+');
        if (plusIndex >= 0)
        {
            text = text[..plusIndex];
        }

        string? preRelease = null;
        var dashIndex = text.IndexOf('-');
        if (dashIndex >= 0)
        {
            preRelease = text[(dashIndex + 1)..];
            text = text[..dashIndex];
        }

        var parts = text.Split('.');
        if (parts.Length != 3) return false;

        if (!int.TryParse(parts[0], out var major) || major < 0) return false;
        if (!int.TryParse(parts[1], out var minor) || minor < 0) return false;
        if (!int.TryParse(parts[2], out var patch) || patch < 0) return false;

        result = new SemanticVersion(major, minor, patch, preRelease);
        return true;
    }

    public static SemanticVersion Parse(string versionText)
    {
        if (!TryParse(versionText, out var semver))
        {
            throw new FormatException($"El formato de versión '{versionText}' no cumple con SemVer 2.0.0.");
        }
        return semver;
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;
        if (ReferenceEquals(this, other)) return 0;

        var majorCompare = Major.CompareTo(other.Major);
        if (majorCompare != 0) return majorCompare;

        var minorCompare = Minor.CompareTo(other.Minor);
        if (minorCompare != 0) return minorCompare;

        var patchCompare = Patch.CompareTo(other.Patch);
        if (patchCompare != 0) return patchCompare;

        // Regla SemVer: Una versión sin pre-release tiene mayor precedencia que una con pre-release
        if (string.IsNullOrEmpty(PreRelease) && !string.IsNullOrEmpty(other.PreRelease)) return 1;
        if (!string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease)) return -1;
        if (string.IsNullOrEmpty(PreRelease) && string.IsNullOrEmpty(other.PreRelease)) return 0;

        return ComparePreReleases(PreRelease!, other.PreRelease!);
    }

    private static int ComparePreReleases(string pre1, string pre2)
    {
        var parts1 = pre1.Split('.');
        var parts2 = pre2.Split('.');
        var minLength = Math.Min(parts1.Length, parts2.Length);

        for (var i = 0; i < minLength; i++)
        {
            var p1 = parts1[i];
            var p2 = parts2[i];

            var isNum1 = int.TryParse(p1, out var num1);
            var isNum2 = int.TryParse(p2, out var num2);

            if (isNum1 && isNum2)
            {
                var numCompare = num1.CompareTo(num2);
                if (numCompare != 0) return numCompare;
            }
            else if (isNum1)
            {
                return -1; // Los numéricos tienen menor precedencia que los alfanuméricos
            }
            else if (isNum2)
            {
                return 1;
            }
            else
            {
                var textCompare = string.Compare(p1, p2, StringComparison.OrdinalIgnoreCase);
                if (textCompare != 0) return textCompare;
            }
        }

        return parts1.Length.CompareTo(parts2.Length);
    }

    public bool Equals(SemanticVersion? other) => CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease?.ToLowerInvariant());

    public static bool operator ==(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(SemanticVersion? left, SemanticVersion? right) => !(left == right);
    public static bool operator <(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(SemanticVersion? left, SemanticVersion? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(SemanticVersion? left, SemanticVersion? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    public override string ToString() =>
        string.IsNullOrEmpty(PreRelease) ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";
}
