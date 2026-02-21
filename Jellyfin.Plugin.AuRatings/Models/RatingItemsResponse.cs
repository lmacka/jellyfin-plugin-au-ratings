using System.Collections.Generic;

namespace Jellyfin.Plugin.AuRatings.Models;

public class RatingItemsResponse
{
    public IReadOnlyList<RatingItemDto> Items { get; set; } = [];

    public int TotalRecordCount { get; set; }

    public int StartIndex { get; set; }
}
