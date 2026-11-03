namespace NTSW_CENOPS.Components.Common;

/// <summary>
/// Public presentation metadata for the current CENOPS product release.
/// The product name, version number and release codename are distinct concepts.
/// </summary>
public static class CenopsRelease
{
    public const string Product = "C.E.N.O.P.S.";
    public const string PersianName = "گمرک / سیناپس";
    public const string Version = "2.67";
    public const string Codename = "GLASSWORK";
    public const string CopyrightYear = "2026";

    public static string FullName => $"{Product} {Version} — {Codename}";
}
