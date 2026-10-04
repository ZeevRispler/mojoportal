using mojoPortal.Business;
using Xunit;

namespace mojoPortal.Tests.Unit;

/// <summary>
/// Source-derived assertions for ListConfiguration defaults and
/// WebUtils.ParseBool/ParseInt32FromHashtable semantics.
/// </summary>
public sealed class ListConfigurationTests
{
    [Fact]
    public void Defaults_match_source_code()
    {
        var c = new ListConfiguration(new Dictionary<string, string>());
        Assert.Equal(string.Empty, c.IntroContent);
        Assert.False(c.UseDescription);
        Assert.False(c.DescriptionOnly);
        Assert.False(c.EnablePager);
        Assert.True(c.UseAjaxPaging);
        Assert.Equal(50, c.PageSize);
        Assert.Equal(string.Empty, c.InstanceCssClass);
        Assert.True(c.ShowPageLeftContent);
        Assert.True(c.ShowPageRightContent);
    }

    [Fact]
    public void DescriptionOnly_implies_UseDescription()
    {
        var c = new ListConfiguration(new Dictionary<string, string>
        {
            ["LinksShowOnlyDescriptionSetting"] = "true",
        });
        Assert.True(c.DescriptionOnly);
        Assert.True(c.UseDescription);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("yes", false)]   // only literal "true" parses true
    [InlineData("1", false)]
    public void ParseBool_semantics(string raw, bool expected)
    {
        var c = new ListConfiguration(new Dictionary<string, string>
        {
            ["LinksEnablePagingSetting"] = raw,
        });
        Assert.Equal(expected, c.EnablePager);
    }

    [Theory]
    [InlineData("10", 10)]
    [InlineData("abc", 50)]      // invalid -> keeps default
    [InlineData("", 50)]         // empty -> keeps default
    public void ParseInt32_semantics(string raw, int expected)
    {
        var c = new ListConfiguration(new Dictionary<string, string>
        {
            ["LinksPageSizeSetting"] = raw,
        });
        Assert.Equal(expected, c.PageSize);
    }

    [Fact]
    public void Settings_from_seeded_strings_load()
    {
        var c = new ListConfiguration(new Dictionary<string, string>
        {
            ["IntroContent"] = "<p>hi</p>",
            ["LinksExtraCssClassSetting"] = "custom",
            ["UseAjaxPaging"] = "false",
            ["ShowPageLeftContentSetting"] = "false",
        });
        Assert.Equal("<p>hi</p>", c.IntroContent);
        Assert.Equal("custom", c.InstanceCssClass);
        Assert.False(c.UseAjaxPaging);
        Assert.False(c.ShowPageLeftContent);
    }
}
