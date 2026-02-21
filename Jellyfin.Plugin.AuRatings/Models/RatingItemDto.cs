using System;

namespace Jellyfin.Plugin.AuRatings.Models;

public class RatingItemDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string? OfficialRating { get; set; }

    public string? SuggestedAuRating { get; set; }

    public bool HasAuRating { get; set; }

    public int? ProductionYear { get; set; }

    public bool HasPrimaryImage { get; set; }

    public string? PrimaryImageTag { get; set; }

    public string? SeriesName { get; set; }
}
