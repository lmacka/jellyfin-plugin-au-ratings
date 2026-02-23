using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.AuRatings.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AuRatings;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override string Name => "AU Ratings";

    public override Guid Id => Guid.Parse("b4c7d8e9-2345-6789-bcde-fa0123456789");

    public static Plugin? Instance { get; private set; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;

        return
        [
            new PluginPageInfo
            {
                Name = "auratings",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.Web.auRatings.html", ns),
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "verified_user",
                DisplayName = "AU Ratings"
            },
            new PluginPageInfo
            {
                Name = "auratingsjs",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.Web.auRatings.js", ns)
            },
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", ns)
            }
        ];
    }
}
