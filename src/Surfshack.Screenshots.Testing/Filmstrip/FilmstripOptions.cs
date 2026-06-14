using SixLabors.ImageSharp.PixelFormats;

namespace Surfshack.Screenshots.Testing.Filmstrip;

/// <summary>
/// Tunables for <see cref="FilmstripCapture.CaptureAsync"/>. Defaults target the
/// typical case — a ~500ms CSS transition captured at 120ms cadence with a generous
/// clip pad so drop shadows aren't cropped.
/// </summary>
public sealed class FilmstripOptions
{
    /// <summary>
    /// Number of frames captured <em>after</em> the trigger fires. The composed
    /// filmstrip contains <c>PostTriggerFrames + 1</c> tiles total (the extra one
    /// being the pre-trigger baseline frame at t=0). Defaults to 5.
    /// </summary>
    public int PostTriggerFrames { get; set; } = 5;

    /// <summary>
    /// Milliseconds between frame captures after the trigger fires. Defaults to
    /// 120ms, which produces 6 tiles spanning a 600ms window — a good fit for a
    /// 500ms CSS transition with a little settling slack.
    /// </summary>
    public int CadenceMs { get; set; } = 120;

    /// <summary>
    /// Pixels added to every side of the target element's bounding box when
    /// clipping each frame. Needed so drop shadows and halos aren't cropped.
    /// Defaults to 90.
    /// </summary>
    public float ShadowPadding { get; set; } = 90f;

    /// <summary>
    /// Background color of the composed canvas. Defaults to a dark near-black
    /// (28, 24, 18) that flatters most UI palettes without drawing the eye.
    /// </summary>
    public Rgba32 BackgroundColor { get; set; } = new(28, 24, 18);

    /// <summary>
    /// Gap in pixels between adjacent frame tiles. Defaults to 14.
    /// </summary>
    public int TileGap { get; set; } = 14;

    /// <summary>
    /// Padding in pixels around the outside of the strip. Defaults to 24, or
    /// 48 when labels are drawn (to leave room for the caption band).
    /// </summary>
    public int CanvasPadding { get; set; } = 24;

    /// <summary>
    /// If true, a time label (e.g. "0 ms", "120 ms"...) is drawn under each tile
    /// using a bundled JetBrains Mono font. Defaults to true.
    /// </summary>
    public bool DrawLabels { get; set; } = true;

    /// <summary>
    /// Font size in pixels for the time labels when <see cref="DrawLabels"/> is
    /// true. Defaults to 18.
    /// </summary>
    public float LabelFontSize { get; set; } = 18f;

    /// <summary>
    /// Color of the time labels. Defaults to a warm parchment hue that reads
    /// well against the default dark background without being loud.
    /// </summary>
    public Rgba32 LabelColor { get; set; } = new(232, 220, 200);
}
