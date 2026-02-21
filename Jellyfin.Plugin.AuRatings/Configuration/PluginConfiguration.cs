using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AuRatings.Configuration;

public enum ViewMode
{
    Table,
    Cards
}

public class PluginConfiguration : BasePluginConfiguration
{
    public PluginConfiguration()
    {
        ItemsPerPage = 50;
        DefaultView = ViewMode.Table;
    }

    public int ItemsPerPage { get; set; }

    public ViewMode DefaultView { get; set; }
}
