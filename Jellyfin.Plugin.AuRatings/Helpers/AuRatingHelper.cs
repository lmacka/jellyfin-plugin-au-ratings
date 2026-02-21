using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.AuRatings.Helpers;

public static class AuRatingHelper
{
    public static readonly IReadOnlyList<string> ValidAuRatings =
    [
        "AU-G",
        "AU-PG",
        "AU-M",
        "AU-MA 15+",
        "AU-R 18+",
        "AU-X 18+"
    ];

    public static readonly IReadOnlyDictionary<string, string> RatingLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["AU-G"] = "G",
        ["AU-PG"] = "PG",
        ["AU-M"] = "M",
        ["AU-MA 15+"] = "MA 15+",
        ["AU-R 18+"] = "R 18+",
        ["AU-X 18+"] = "X 18+"
    };

    private static readonly Dictionary<string, string> MappingTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // AU-G
        ["G"] = "AU-G",
        ["TV-Y"] = "AU-G",
        ["TV-Y7"] = "AU-G",
        ["TV-G"] = "AU-G",
        ["U"] = "AU-G",

        // AU-PG
        ["PG"] = "AU-PG",
        ["TV-PG"] = "AU-PG",
        ["12"] = "AU-PG",
        ["12A"] = "AU-PG",

        // AU-M
        ["PG-13"] = "AU-M",
        ["TV-14"] = "AU-M",
        ["M"] = "AU-M",

        // AU-MA 15+
        ["R"] = "AU-MA 15+",
        ["TV-MA"] = "AU-MA 15+",
        ["15"] = "AU-MA 15+",
        ["MA15+"] = "AU-MA 15+",
        ["R13"] = "AU-MA 15+",
        ["R16"] = "AU-MA 15+",

        // AU-R 18+
        ["NC-17"] = "AU-R 18+",
        ["18"] = "AU-R 18+",
        ["R18"] = "AU-R 18+",

        // AU-X 18+
        ["X"] = "AU-X 18+",
        ["X18+"] = "AU-X 18+"
    };

    private static readonly string[] KnownPrefixes =
    [
        "US-", "GB-", "NZ-", "DE-", "FR-", "JP-", "KR-", "CA-", "AU-"
    ];

    public static bool IsValidAuRating(string rating)
    {
        foreach (var valid in ValidAuRatings)
        {
            if (string.Equals(valid, rating, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasAuRating([NotNullWhen(true)] string? rating)
    {
        return !string.IsNullOrEmpty(rating)
            && rating.StartsWith("AU-", StringComparison.OrdinalIgnoreCase);
    }

    public static string? SuggestAuRating(string? currentRating)
    {
        if (string.IsNullOrEmpty(currentRating))
        {
            return null;
        }

        if (HasAuRating(currentRating))
        {
            return null;
        }

        var stripped = StripPrefix(currentRating);

        if (MappingTable.TryGetValue(stripped, out var mapped))
        {
            return mapped;
        }

        return null;
    }

    private static string StripPrefix(string rating)
    {
        foreach (var prefix in KnownPrefixes)
        {
            if (rating.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return rating.Substring(prefix.Length);
            }
        }

        return rating;
    }
}
