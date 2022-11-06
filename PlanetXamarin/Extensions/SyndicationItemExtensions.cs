using System.ServiceModel.Syndication;

namespace PlanetXamarin.Extensions;

public static class SyndicationItemExtensions
{
    public static bool ApplyDefaultFilter(this SyndicationItem item)
    {
        var hasXamarinCategory = false;
        var hasXamarinKeywords = false;

        if (item.Categories.Count > 0)
        {
            hasXamarinCategory = item.Categories.Any(category =>
                category.Name.ToLowerInvariant().Contains("xamarin"));
        }

        if (item.ElementExtensions.Count > 0)
        {
            var element = item.ElementExtensions.FirstOrDefault(e => e.OuterName == "keywords");
            if (element != null)
            {
                var keywords = element.GetObject<string>();
                hasXamarinKeywords = keywords.ToLowerInvariant().Contains("xamarin");
            }
        }

        var hasXamarinTitle = item.Title?.Text.ToLowerInvariant().Contains("xamarin") ?? false;

        return hasXamarinTitle || hasXamarinCategory || hasXamarinKeywords;
    }

	public static string ToHtml(this SyndicationContent content) =>
        content is TextSyndicationContent textSyndicationContent
            ? textSyndicationContent.Text
            : content.ToString() ?? string.Empty;
}
