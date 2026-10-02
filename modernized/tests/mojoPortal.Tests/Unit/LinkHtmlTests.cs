using mojoPortal.Business.Rendering;
using Xunit;

namespace mojoPortal.Tests.Unit;

/// <summary>
/// Source-derived assertions for ItemList.ascx.cs markup emission:
/// byte-for-byte output including the unencoded-anchor-text quirk.
/// </summary>
public sealed class LinkHtmlTests
{
    [Fact]
    public void Empty_url_yields_empty_string()
        => Assert.Equal(string.Empty, LinkHtml.CreateLink("t", "", "d", "", ""));

    [Fact]
    public void Root_tilde_resolves_against_image_site_root()
    {
        Assert.Equal("/sub/page.htm", LinkHtml.GetLinkUrl("~/page.htm", "/sub"));
        Assert.Equal("/page.htm", LinkHtml.GetLinkUrl("~/page.htm", ""));
    }

    [Fact]
    public void Leading_slash_resolves_against_image_site_root()
    {
        Assert.Equal("/sub/x/y", LinkHtml.GetLinkUrl("/x/y", "/sub"));
    }

    [Fact]
    public void Other_urls_pass_through()
    {
        Assert.Equal("https://example.com/a", LinkHtml.GetLinkUrl("https://example.com/a", "/sub"));
        Assert.Equal("http://x", LinkHtml.GetLinkUrl("http://x", ""));
        Assert.Equal("mailto:a@b.c", LinkHtml.GetLinkUrl("mailto:a@b.c", ""));
    }

    [Fact]
    public void Anchor_markup_matches_source_shape()
    {
        var html = LinkHtml.CreateLink("My Title", "~/p", "desc", "_blank", "");
        Assert.Equal("<a class='mojolink' href='/p'  target='_blank'  title='My Title' >My Title</a>", html);
    }

    [Fact]
    public void Target_non_blank_emits_no_target()
    {
        var html = LinkHtml.CreateLink("T", "/p", "", "", "");
        Assert.Equal("<a class='mojolink' href='/p'  title='T' >T</a>", html);
    }

    [Fact]
    public void Empty_title_omits_title_attribute()
    {
        var html = LinkHtml.CreateLink("", "/p", "", "", "");
        Assert.Equal("<a class='mojolink' href='/p' ></a>", html);
    }

    [Fact]
    public void Title_attribute_html_encoded_but_anchor_text_raw()
    {
        var html = LinkHtml.CreateLink("a<b", "/p", "", "", "");
        Assert.Equal("<a class='mojolink' href='/p'  title='a&lt;b' >a<b</a>", html);
    }
}
