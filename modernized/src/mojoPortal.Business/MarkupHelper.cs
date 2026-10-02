using System.Text;
using Ganss.Xss;

namespace mojoPortal.Business;

/// <summary>
/// Port of mojoPortal.Core StringExtensions RemoveMarkup / SanitizeMarkup /
/// RemoveMultipleSpaces, driven by NonCmsBasePage.EncodeTextboxControls on
/// every POST: single-line inputs lose all markup (keeping child text),
/// multi-line inputs are whitelist-sanitized (except for Admins).
/// </summary>
public static class MarkupHelper
{
    private static readonly HtmlSanitizer Sanitizer;
    private static readonly HtmlSanitizer MarkupRemover;

    static MarkupHelper()
    {
        Sanitizer = new HtmlSanitizer
        {
            AllowDataAttributes = true,
            AllowCssCustomProperties = true,
        };
        Sanitizer.AllowedAttributes.Add("class");
        Sanitizer.AllowedSchemes.Add("mailto");
        Sanitizer.AllowedSchemes.Add("tel");

        var markupRemoverOptions = new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(),
        };

        MarkupRemover = new HtmlSanitizer(markupRemoverOptions)
        {
            KeepChildNodes = true,
        };
    }

    /// <summary>Strips all tags, keeps child text, collapses spaces, trims.</summary>
    public static string RemoveMarkup(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return MarkupRemover.Sanitize(value).RemoveMultipleSpaces();
    }

    /// <summary>Whitelist HTML sanitization (source _sanitizer defaults).</summary>
    public static string SanitizeMarkup(string? value)
        => Sanitizer.Sanitize(value ?? string.Empty);

    /// <summary>Port: collapses runs of literal ' ' only (other whitespace kept), trims.</summary>
    public static string RemoveMultipleSpaces(this string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return value; }

        var sb = new StringBuilder(value.Length);
        bool lastCharWasSpace = false;

        foreach (char c in value)
        {
            if (c == ' ')
            {
                if (!lastCharWasSpace)
                {
                    sb.Append(c);
                }

                lastCharWasSpace = true;
            }
            else
            {
                sb.Append(c);
                lastCharWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }
}
