﻿namespace EmmyLua.CodeAnalysis.Document.Version;

public readonly record struct VersionNumber(ushort Major, ushort Minor, ushort Patch, ushort Build)
{
    private long Combined => ((long)Major << 48) | ((long)Minor << 32) | ((long)Patch << 16) | Build;

    public static VersionNumber Parse(string version)
    {
        var parts = version.Split('.');
        if (parts.Length is > 4 or 0)
        {
            throw new FormatException("Invalid version format.");
        }

        try
        {
            var major = ushort.Parse(parts[0]);
            var minor = parts.Length > 1 ? ushort.Parse(parts[1]) : (ushort)0;
            var patch = parts.Length > 2 ? ushort.Parse(parts[2]) : (ushort)0;
            var build = parts.Length > 3 ? ushort.Parse(parts[3]) : (ushort)0;
            return new VersionNumber(major, minor, patch, build);
        }
        catch (Exception)
        {
            return new VersionNumber(0, 0, 0, 0);
        }
    }

    public override string ToString()
    {
        if (Patch == 0 && Build == 0)
        {
            return $"{Major}.{Minor}";
        }
        else if (Build == 0)
        {
            return $"{Major}.{Minor}.{Patch}";
        }
        else
        {
            return $"{Major}.{Minor}.{Patch}.{Build}";
        }
    }

    public static bool operator <(VersionNumber left, VersionNumber right)
    {
        return left.Combined < right.Combined;
    }

    public static bool operator >(VersionNumber left, VersionNumber right)
    {
        return left.Combined > right.Combined;
    }

    public static bool operator <=(VersionNumber left, VersionNumber right)
    {
        return left.Combined <= right.Combined;
    }

    public static bool operator >=(VersionNumber left, VersionNumber right)
    {
        return left.Combined >= right.Combined;
    }
}
