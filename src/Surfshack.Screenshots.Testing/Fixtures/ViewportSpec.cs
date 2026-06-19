namespace Surfshack.Screenshots.Testing.Fixtures;

/// <summary>
/// Viewport dimensions for a screenshot capture. Consumers pass well-known
/// presets (<see cref="Desktop"/>, <see cref="Mobile"/>, <see cref="Tablet"/>,
/// <see cref="Wide"/>) or declare custom sizes inline
/// (<c>new ViewportSpec("ultrawide", 3840, 1600)</c>).
/// </summary>
/// <param name="Name">Filename-safe identifier used in screenshot paths (e.g., <c>desktop</c>).</param>
/// <param name="Width">Viewport width in CSS pixels.</param>
/// <param name="Height">Viewport height in CSS pixels.</param>
public sealed record ViewportSpec(string Name, int Width, int Height)
{
    /// <summary>Desktop preset — 1440×900.</summary>
    public static readonly ViewportSpec Desktop = new("desktop", 1440, 900);

    /// <summary>Mobile preset — 390×844 (iPhone-class portrait).</summary>
    public static readonly ViewportSpec Mobile  = new("mobile",  390,  844);

    /// <summary>Tablet preset — 768×1024 (iPad-class portrait).</summary>
    public static readonly ViewportSpec Tablet  = new("tablet",  768,  1024);

    /// <summary>Wide preset — 1920×1080 (full-HD landscape).</summary>
    public static readonly ViewportSpec Wide    = new("wide",   1920, 1080);
}
