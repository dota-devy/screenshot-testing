namespace Surfshack.Screenshots.Testing.Tests;

/// <summary>
/// Writes a markdown table of contents listing every screenshot in a screenshot root.
/// Called from <c>ScreenshotFixtureBase.DisposeAsync</c> at the end of a test run,
/// so the resulting <c>index.md</c> ships in the CI artifact alongside the PNGs.
/// </summary>
public static class ScreenshotIndexWriter
{
    public static void Write(string screenshotRoot)
    {
        if (string.IsNullOrEmpty(screenshotRoot) || !Directory.Exists(screenshotRoot))
            return;

        var rows = new List<string>
        {
            "# Screenshot Run",
            "",
            "| Slug | Desktop | Mobile |",
            "| --- | --- | --- |",
        };

        var desktopDir = Path.Combine(screenshotRoot, "desktop");
        if (Directory.Exists(desktopDir))
        {
            foreach (var file in Directory.EnumerateFiles(desktopDir, "*.png").OrderBy(f => f))
            {
                var slug = Path.GetFileNameWithoutExtension(file);
                rows.Add($"| {slug} | desktop/{slug}.png | mobile/{slug}.png |");
            }
        }

        File.WriteAllText(
            Path.Combine(screenshotRoot, "index.md"),
            string.Join(Environment.NewLine, rows));
    }
}
