namespace VedaMatrix;

/// <summary>
/// Static application metadata for VEDA Matrix.
/// </summary>
/// <remarks>
/// Author: eGovernment
/// Copyright: © 2026 eGovernment
/// Product: Buchungsvorgänge im Veda Horizon – Entscheidungsmatrix
/// </remarks>
public static class AppInfo
{
    /// <summary>Display name of the application.</summary>
    public const string Name = "VEDA Matrix";

    /// <summary>Semantic version string.</summary>
    public const string Version = "0.0.1";

    /// <summary>Author / organization responsible for this tool.</summary>
    public const string Author = "Lenny Kunz";

    /// <summary>Short product description (German UI).</summary>
    public const string Description = "Buchungsvorgänge im Veda Horizon – Entscheidungsmatrix";

    /// <summary>Copyright notice.</summary>
    public const string Copyright = "© 2026 Lenny Kunz";

    /// <summary>Company or publisher name.</summary>
    public const string Company = "eGovernment";

    /// <summary>Combined footer text for the main window.</summary>
    public static string FooterText => $"{Name} v{Version}  ·  {Author}  ·  {Copyright}";
}
