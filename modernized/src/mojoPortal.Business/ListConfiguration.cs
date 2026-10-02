namespace mojoPortal.Business;

/// <summary>
/// Port of mojoPortal.Features.UI List/Components/ListConfiguration.cs.
/// Settings arrive as the module's key/value hashtable; every setting keeps the
/// source defaults.
/// </summary>
public sealed class ListConfiguration
{
    public ListConfiguration() { }

    public ListConfiguration(IDictionary<string, string> settings)
        => LoadSettings(settings);

    private void LoadSettings(IDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        useDescription = ParseBool(settings, "LinksShowDescriptionSetting", useDescription);
        descriptionOnly = ParseBool(settings, "LinksShowOnlyDescriptionSetting", descriptionOnly);
        if (descriptionOnly) { useDescription = true; }

        enablePager = ParseBool(settings, "LinksEnablePagingSetting", enablePager);
        pageSize = ParseInt32(settings, "LinksPageSizeSetting", pageSize);

        if (settings.TryGetValue("LinksExtraCssClassSetting", out string? css) && css is not null)
        {
            instanceCssClass = css;
        }

        useAjaxPaging = ParseBool(settings, "UseAjaxPaging", useAjaxPaging);
        showPageLeftContent = ParseBool(settings, "ShowPageLeftContentSetting", showPageLeftContent);
        showPageRightContent = ParseBool(settings, "ShowPageRightContentSetting", showPageRightContent);

        if (settings.TryGetValue("IntroContent", out string? intro) && intro is not null)
        {
            introContent = intro;
        }
    }

    // Mirrors WebUtils.ParseBoolFromHashtable: missing/empty -> current default;
    // "true"/"True" -> true; anything else -> false.
    private static bool ParseBool(IDictionary<string, string> settings, string key, bool current)
    {
        if (!settings.TryGetValue(key, out string? raw) || string.IsNullOrEmpty(raw))
        {
            return current;
        }

        return raw.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    // Mirrors WebUtils.ParseInt32FromHashtable: missing/empty/invalid -> current default.
    private static int ParseInt32(IDictionary<string, string> settings, string key, int current)
    {
        if (!settings.TryGetValue(key, out string? raw) || string.IsNullOrEmpty(raw))
        {
            return current;
        }

        return int.TryParse(raw, out int parsed) ? parsed : current;
    }

    private string introContent = string.Empty;
    public string IntroContent => introContent;

    private bool useDescription = false;
    public bool UseDescription => useDescription;

    private bool descriptionOnly = false;
    public bool DescriptionOnly => descriptionOnly;

    private bool enablePager = false;
    public bool EnablePager => enablePager;

    private bool useAjaxPaging = true;
    public bool UseAjaxPaging => useAjaxPaging;

    private int pageSize = 50;
    public int PageSize => pageSize;

    private string instanceCssClass = string.Empty;
    public string InstanceCssClass => instanceCssClass;

    private bool showPageLeftContent = true;
    public bool ShowPageLeftContent => showPageLeftContent;

    private bool showPageRightContent = true;
    public bool ShowPageRightContent => showPageRightContent;
}
