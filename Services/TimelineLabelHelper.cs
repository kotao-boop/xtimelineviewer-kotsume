using System;
using XTimelineViewer.Models;

namespace XTimelineViewer.Services
{
    internal static class TimelineLabelHelper
    {
        public static string GetFriendlyName(TimelineConfig config, Func<string, string> resource)
        {
            if (!string.IsNullOrWhiteSpace(config.Name)) return config.Name.Trim();
            if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var uri)) return config.Url;

            var path = uri.AbsolutePath.TrimEnd('/');
            if (path.Equals("/home", StringComparison.OrdinalIgnoreCase)) return resource("Timeline_Home");
            if (path.StartsWith("/notifications", StringComparison.OrdinalIgnoreCase)) return resource("Timeline_Notifications");
            if (path.Equals("/i/bookmarks", StringComparison.OrdinalIgnoreCase)) return resource("Timeline_Bookmarks");
            if (path.EndsWith("/lists", StringComparison.OrdinalIgnoreCase) || config.IsListsIndex) return resource("Timeline_Lists");

            var query = SearchQueryHelper.ExtractQueryFromUrl(config.Url);
            if (!string.IsNullOrWhiteSpace(query))
                return string.Format(resource("Timeline_SearchName"), query);

            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 0 && !segments[0].Equals("i", StringComparison.OrdinalIgnoreCase))
                return "@" + Uri.UnescapeDataString(segments[0]);

            return SearchQueryHelper.DecodeSearchPath(uri.Host + uri.PathAndQuery);
        }
    }
}
