using Microsoft.Playwright;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Surfshack.Screenshots.Testing.Filmstrip;

/// <summary>
/// Captures an animation as a horizontal filmstrip PNG: a pre-trigger baseline
/// frame, then N frames at fixed cadence after a caller-supplied trigger. The
/// frames are composed onto a single canvas with optional time labels drawn in
/// a bundled JetBrains Mono font.
/// </summary>
/// <remarks>
/// <para>Why class-toggle over Playwright's <c>HoverAsync</c>: headless Chromium
/// doesn't reliably fire <c>:hover</c> for layout-driven transitions, so the
/// recommended pattern is to define the hover rule against both <c>:hover</c>
/// and a helper class (e.g. <c>.is-hovered</c>), then pass a trigger that does
/// <c>locator.EvaluateAsync("el =&gt; el.classList.add('is-hovered')")</c>.</para>
/// <para>If your <see cref="Fixtures.IScreenshotFixture"/> injects a global
/// <c>* { transition-duration: 0s !important }</c> stylesheet to make static
/// screenshots deterministic, strip it before calling this helper — the cleanest
/// path is removing the fixture's injected <c>&lt;style&gt;</c> element entirely
/// rather than fighting its specificity.</para>
/// </remarks>
public static class FilmstripCapture
{
    private static readonly Lazy<FontFamily> BundledFont = new(LoadBundledFont);

    /// <summary>
    /// Capture a filmstrip of a single animated interaction on <paramref name="page"/>.
    /// </summary>
    /// <param name="page">The live Playwright page. Caller is responsible for
    /// navigation, fixture-style stripping, and any pre-capture settling.</param>
    /// <param name="target">Locator for the element whose bounding box defines
    /// the clip. Frames are captured at page level (not <c>locator.Screenshot</c>)
    /// so drop shadows extending beyond the element are preserved via the
    /// <see cref="FilmstripOptions.ShadowPadding"/> expansion.</param>
    /// <param name="trigger">Delegate that mutates the page into the animated
    /// state — typically a <c>classList.add</c> via <c>EvaluateAsync</c>. The
    /// baseline frame is captured <em>before</em> this runs.</param>
    /// <param name="options">Optional tunables; defaults produce a 6-frame strip
    /// over 600ms with labels enabled.</param>
    /// <returns>The composed filmstrip as a PNG byte array.</returns>
    public static async Task<byte[]> CaptureAsync(
        IPage page,
        ILocator target,
        Func<Task> trigger,
        FilmstripOptions? options = null)
    {
        options ??= new FilmstripOptions();

        var bounds = await target.BoundingBoxAsync()
            ?? throw new InvalidOperationException(
                "FilmstripCapture target has no bounding box — is the element visible?");

        var clip = new Clip
        {
            X = (float)Math.Max(0, bounds.X - options.ShadowPadding),
            Y = (float)Math.Max(0, bounds.Y - options.ShadowPadding),
            Width = (float)(bounds.Width + options.ShadowPadding * 2),
            Height = (float)(bounds.Height + options.ShadowPadding * 2),
        };

        var screenshotOpts = new PageScreenshotOptions { Clip = clip };

        var frames = new List<byte[]>(options.PostTriggerFrames + 1)
        {
            await page.ScreenshotAsync(screenshotOpts),
        };

        await trigger();

        for (int i = 1; i <= options.PostTriggerFrames; i++)
        {
            await page.WaitForTimeoutAsync(options.CadenceMs);
            frames.Add(await page.ScreenshotAsync(screenshotOpts));
        }

        var labels = Enumerable.Range(0, frames.Count)
            .Select(i => $"{i * options.CadenceMs} ms")
            .ToArray();

        return Compose(frames, labels, options);
    }

    private static byte[] Compose(
        IReadOnlyList<byte[]> frameBytes,
        string[] labels,
        FilmstripOptions options)
    {
        var frames = frameBytes.Select(b => Image.Load<Rgba32>(b)).ToList();
        try
        {
            var frameWidth = frames[0].Width;
            var frameHeight = frames[0].Height;

            foreach (var f in frames)
            {
                if (f.Width != frameWidth || f.Height != frameHeight)
                    throw new InvalidOperationException(
                        $"Frame size mismatch: expected {frameWidth}x{frameHeight}, got {f.Width}x{f.Height}. " +
                        "This usually means the viewport reflowed mid-capture.");
            }

            var pad = options.DrawLabels ? Math.Max(options.CanvasPadding, 48) : options.CanvasPadding;
            var labelBand = options.DrawLabels ? (int)Math.Ceiling(options.LabelFontSize * 1.8) : 0;

            var totalWidth = frameWidth * frames.Count + options.TileGap * (frames.Count - 1) + pad * 2;
            var totalHeight = frameHeight + labelBand + pad * 2;

            var canvas = new Image<Rgba32>(totalWidth, totalHeight, options.BackgroundColor);

            Font? labelFont = options.DrawLabels
                ? BundledFont.Value.CreateFont(options.LabelFontSize, FontStyle.Regular)
                : null;

            for (int i = 0; i < frames.Count; i++)
            {
                var tileX = pad + i * (frameWidth + options.TileGap);
                var tileY = pad;
                var frame = frames[i];
                canvas.Mutate(c => c.DrawImage(frame, new Point(tileX, tileY), 1f));

                if (labelFont is not null)
                {
                    var label = labels[i];
                    var textOptions = new RichTextOptions(labelFont)
                    {
                        Origin = new PointF(tileX + frameWidth / 2f, tileY + frameHeight + labelBand * 0.55f),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    canvas.Mutate(c => c.DrawText(textOptions, label, Brushes.Solid(options.LabelColor)));
                }
            }

            using var ms = new MemoryStream();
            canvas.SaveAsPng(ms);
            canvas.Dispose();
            return ms.ToArray();
        }
        finally
        {
            foreach (var f in frames) f.Dispose();
        }
    }

    private static FontFamily LoadBundledFont()
    {
        const string resourceName =
            "Surfshack.Screenshots.Testing.Filmstrip.Fonts.JetBrainsMono-Regular.ttf";

        var assembly = typeof(FilmstripCapture).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Bundled font resource not found: {resourceName}. " +
                "Check the csproj EmbeddedResource entry and logical name.");

        var collection = new FontCollection();
        return collection.Add(stream);
    }
}
