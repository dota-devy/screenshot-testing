using Surfshack.Screenshots.Testing.Fixtures;
using Xunit;

namespace Surfshack.Screenshots.Testing.Tests.Unit;

public class ViewportSpecTests
{
    [Fact]
    public void Desktop_Preset_Has_Expected_Dimensions()
    {
        Assert.Equal("desktop", ViewportSpec.Desktop.Name);
        Assert.Equal(1440, ViewportSpec.Desktop.Width);
        Assert.Equal(900, ViewportSpec.Desktop.Height);
    }

    [Fact]
    public void Mobile_Preset_Has_Expected_Dimensions()
    {
        Assert.Equal("mobile", ViewportSpec.Mobile.Name);
        Assert.Equal(390, ViewportSpec.Mobile.Width);
        Assert.Equal(844, ViewportSpec.Mobile.Height);
    }

    [Fact]
    public void Tablet_Preset_Has_Expected_Dimensions()
    {
        Assert.Equal("tablet", ViewportSpec.Tablet.Name);
        Assert.Equal(768, ViewportSpec.Tablet.Width);
        Assert.Equal(1024, ViewportSpec.Tablet.Height);
    }

    [Fact]
    public void Wide_Preset_Has_Expected_Dimensions()
    {
        Assert.Equal("wide", ViewportSpec.Wide.Name);
        Assert.Equal(1920, ViewportSpec.Wide.Width);
        Assert.Equal(1080, ViewportSpec.Wide.Height);
    }

    [Fact]
    public void Custom_Spec_Stores_Provided_Values()
    {
        var custom = new ViewportSpec("ultrawide", 3840, 1600);
        Assert.Equal("ultrawide", custom.Name);
        Assert.Equal(3840, custom.Width);
        Assert.Equal(1600, custom.Height);
    }

    [Fact]
    public void Record_Equality_By_Value()
    {
        var a = new ViewportSpec("desktop", 1440, 900);
        var b = new ViewportSpec("desktop", 1440, 900);
        Assert.Equal(a, b);
    }

    [Fact]
    public void ToPlaywrightSize_Preserves_Width_And_Height()
    {
        var size = BrowserContextHelpers.ToPlaywrightSize(ViewportSpec.Wide);
        Assert.Equal(1920, size.Width);
        Assert.Equal(1080, size.Height);
    }
}
