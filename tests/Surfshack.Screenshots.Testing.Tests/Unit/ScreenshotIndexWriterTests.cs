using Surfshack.Screenshots.Testing.Tests;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.Unit;

public class ScreenshotIndexWriterTests : IDisposable
{
    private readonly string _root;

    public ScreenshotIndexWriterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "screenshot-index-test-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_root, "desktop"));
        Directory.CreateDirectory(Path.Combine(_root, "mobile"));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Write_NoScreenshots_ProducesEmptyTable()
    {
        ScreenshotIndexWriter.Write(_root);

        var content = File.ReadAllText(Path.Combine(_root, "index.md"));
        Assert.Contains("| Slug | Desktop | Mobile |", content);
    }

    [Fact]
    public void Write_OneScreenshot_ProducesRow()
    {
        File.WriteAllBytes(Path.Combine(_root, "desktop", "home.png"), [0x89, 0x50, 0x4E, 0x47]);

        ScreenshotIndexWriter.Write(_root);

        var content = File.ReadAllText(Path.Combine(_root, "index.md"));
        Assert.Contains("| home | desktop/home.png | mobile/home.png |", content);
    }

    [Fact]
    public void Write_RootDoesNotExist_DoesNothing()
    {
        var bogus = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid());
        ScreenshotIndexWriter.Write(bogus);  // must not throw
    }

    [Fact]
    public void Write_MultipleScreenshots_OrdersAlphabetically()
    {
        File.WriteAllBytes(Path.Combine(_root, "desktop", "zebra.png"), [0x89]);
        File.WriteAllBytes(Path.Combine(_root, "desktop", "apple.png"), [0x89]);
        File.WriteAllBytes(Path.Combine(_root, "desktop", "mango.png"), [0x89]);

        ScreenshotIndexWriter.Write(_root);

        var content = File.ReadAllText(Path.Combine(_root, "index.md"));
        var appleIdx = content.IndexOf("apple");
        var mangoIdx = content.IndexOf("mango");
        var zebraIdx = content.IndexOf("zebra");
        Assert.True(appleIdx < mangoIdx && mangoIdx < zebraIdx);
    }
}
