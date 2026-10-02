using mojoPortal.Business;
using Xunit;

namespace mojoPortal.Tests.Unit;

/// <summary>
/// Source-derived assertions for StringExtensions.RemoveMarkup /
/// SanitizeMarkup / RemoveMultipleSpaces (NonCmsBasePage POST encoding).
/// </summary>
public sealed class MarkupHelperTests
{
    [Fact]
    public void RemoveMarkup_strips_tags_keeps_text()
        => Assert.Equal("hello world", MarkupHelper.RemoveMarkup("<b>hello</b> <i>world</i>"));

    [Fact]
    public void RemoveMarkup_collapses_space_runs_and_trims()
        => Assert.Equal("a b", MarkupHelper.RemoveMarkup("  a    b  "));

    [Fact]
    public void RemoveMarkup_does_not_collapse_non_space_whitespace()
    {
        // Source RemoveMultipleSpaces only matches literal ' ' runs.
        var result = MarkupHelper.RemoveMarkup("a\t\tb");
        Assert.Equal("a\t\tb", result);
    }

    [Fact]
    public void SanitizeMarkup_keeps_whitelist_strips_script()
    {
        var result = MarkupHelper.SanitizeMarkup("<p>ok</p><script>alert(1)</script>");
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>", result);
    }

    [Fact]
    public void SanitizeMarkup_allows_data_attributes_and_mailto()
    {
        var result = MarkupHelper.SanitizeMarkup(
            "<a data-x=\"1\" href=\"mailto:a@b.c\" onclick=\"evil()\">m</a>");
        Assert.Contains("data-x", result);
        Assert.Contains("mailto:", result);
        Assert.DoesNotContain("onclick", result);
    }
}
