using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.AuRatings.Helpers;

public static class AuRatingHelper
{
    public static readonly IReadOnlyList<string> ValidAuRatings =
    [
        "G",
        "PG",
        "M",
        "MA 15+",
        "R 18+",
        "X 18+"
    ];

    private static readonly HashSet<string> ValidAuRatingsSet = new HashSet<string>(ValidAuRatings, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> MappingTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        // G
        ["TV-Y"] = "G",
        ["TV-Y7"] = "G",
        ["TV-G"] = "G",
        ["U"] = "G",

        // PG
        ["TV-PG"] = "PG",
        ["12"] = "PG",
        ["12A"] = "PG",

        // M
        ["PG-13"] = "M",
        ["TV-14"] = "M",

        // MA 15+
        ["R"] = "MA 15+",
        ["TV-MA"] = "MA 15+",
        ["15"] = "MA 15+",
        ["MA15+"] = "MA 15+",
        ["R13"] = "MA 15+",
        ["R16"] = "MA 15+",

        // R 18+
        ["NC-17"] = "R 18+",
        ["18"] = "R 18+",
        ["R18"] = "R 18+",

        // X 18+
        ["X"] = "X 18+",
        ["X18+"] = "X 18+"
    };

    private static readonly string[] KnownPrefixes =
    [
        "US-", "GB-", "NZ-", "DE-", "FR-", "JP-", "KR-", "CA-", "AU-"
    ];

    public static bool IsValidAuRating(string rating)
    {
        return ValidAuRatingsSet.Contains(rating);
    }

    public static bool HasAuRating([NotNullWhen(true)] string? rating)
    {
        return !string.IsNullOrEmpty(rating) && ValidAuRatingsSet.Contains(rating);
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
