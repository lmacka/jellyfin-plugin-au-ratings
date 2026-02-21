using System;

namespace Jellyfin.Plugin.AuRatings.Models;

public class SetRatingRequest
{
    public Guid ItemId { get; set; }

    public string Rating { get; set; } = string.Empty;
}
