namespace Surfshack.Screenshots.Testing.Tests;

/// <summary>
/// Writes a markdown table of contents listing every screenshot in a screenshot root.
/// Called from <c>ScreenshotFixtureBase.DisposeAsync</c> at the end of a test run,
/// so the resulting <c>index.md</c> ships in the CI artifact alongside the PNGs.
/// </summary>
public static class ScreenshotIndexWriter
{
    /// <summary>
    /// Writes (or overwrites) <c>index.md</c> in <paramref name="screenshotRoot"/>: a markdown
    /// table with one column per viewport subdirectory actually present and one row per slug,
    /// linking each captured PNG. Viewports are discovered from the subdirectories on disk, so
    /// any viewport set — the presets, custom sizes, or a subset — is listed correctly. A
    /// missing or empty root is a no-op.
    /// </summary>
    /// <param name="screenshotRoot">Absolute path to the screenshot root directory.</param>
    public static void Write(string screenshotRoot)
    {
        if (string.IsNullOrEmpty(screenshotRoot) || !Directory.Exists(screenshotRoot))
            return;

        // Each immediate subdirectory is a viewport (desktop, mobile, tablet, custom, …).
        var viewports = Directory.EnumerateDirectories(screenshotRoot)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        // Union of every slug captured in any viewport, so a route present in only some
        // viewports still gets a row (with blanks where it wasn't captured).
        var slugs = viewports
            .SelectMany(vp => Directory.EnumerateFiles(Path.Combine(screenshotRoot, vp!), "*.png"))
            .Select(Path.GetFileNameWithoutExtension)
            .Distinct()
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .ToList();

        var rows = new List<string>
        {
            "# Screenshot Run",
            "",
            $"| Slug | {string.Join(" | ", viewports)} |",
            $"| --- | {string.Join(" | ", viewports.Select(_ => "---"))} |",
        };

        foreach (var slug in slugs)
        {
            var cells = viewports.Select(vp =>
            {
                var relative = $"{vp}/{slug}.png";
                return File.Exists(Path.Combine(screenshotRoot, vp!, $"{slug}.png")) ? relative : string.Empty;
            });
            rows.Add($"| {slug} | {string.Join(" | ", cells)} |");
        }

        File.WriteAllText(
            Path.Combine(screenshotRoot, "index.md"),
            string.Join(Environment.NewLine, rows));
    }
}
