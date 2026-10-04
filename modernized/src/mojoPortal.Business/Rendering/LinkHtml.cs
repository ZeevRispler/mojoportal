using System.Net;

namespace mojoPortal.Business.Rendering;

/// <summary>
/// Port of ItemList.ascx.cs CreateLink / GetLinkUrl / GetTarget / GetTitle.
/// Output markup is byte-for-byte compatible with the WebForms renderer,
/// including the raw (unencoded) anchor text quirk.
/// </summary>
public static class LinkHtml
{
    private const string LinkCssClass = "mojolink";

    /// <summary>
    /// Source: empty url renders the empty string. Otherwise emits
    /// &lt;a class='mojolink' href='{resolved}' target title&gt;{raw title}&lt;/a&gt;.
    /// </summary>
    public static string CreateLink(string title, string url, string description, string target, string imageSiteRoot)
    {
        if (string.IsNullOrEmpty(url)) { return string.Empty; }

        return "<a class='" + LinkCssClass + "' href='" + GetLinkUrl(url, imageSiteRoot) + "' "
            + GetTarget(target)
            + GetTitle(title, description)
            + ">"
            + title
            + "</a>";
    }

    private static string GetTitle(string? title, string description)
    {
        if ((title != null) && (title.Length > 0))
        {
            return " title='" + WebUtility.HtmlEncode(title) + "' ";
        }

        return string.Empty;
    }

    private static string GetTarget(string? target)
    {
        if ((target != null) && (target == "_blank"))
        {
            return " target='_blank' ";
        }

        return string.Empty;
    }

    /// <summary>
    /// "~/x" and "/x" resolve against the site root; anything else passes
    /// through unchanged. Equivalent of Page.ResolveUrl for app-rooted paths.
    /// </summary>
    public static string GetLinkUrl(string dbFormatUrl, string imageSiteRoot)
    {
        if (dbFormatUrl.StartsWith("~/"))
        {
            return ResolveUrl(imageSiteRoot + dbFormatUrl.Replace("~/", "/"));
        }

        if (dbFormatUrl.StartsWith("/"))
        {
            return ResolveUrl(imageSiteRoot + dbFormatUrl);
        }

        return dbFormatUrl;
    }

    // imageSiteRoot is already app-relative ("" or "/subsite"); normalization
    // only guards against double slashes.
    private static string ResolveUrl(string path)
    {
        while (path.Contains("//"))
        {
            path = path.Replace("//", "/");
        }

        return path;
    }
}
