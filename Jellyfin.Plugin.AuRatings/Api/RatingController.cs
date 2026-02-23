using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.AuRatings.Helpers;
using Jellyfin.Plugin.AuRatings.Models;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AuRatings.Api;

[ApiController]
[Route("AuRatings")]
[Authorize(Policy = Policies.RequiresElevation)]
[Produces(MediaTypeNames.Application.Json)]
public class RatingController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<RatingController> _logger;

    public RatingController(ILibraryManager libraryManager, IUserManager userManager, ILogger<RatingController> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("Items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<RatingItemsResponse> GetItems(
        [FromQuery] int startIndex = 0,
        [FromQuery] int limit = 50,
        [FromQuery] string? type = null,
        [FromQuery] string? ratingFilter = null,
        [FromQuery] string? rating = null,
        [FromQuery] Guid? visibleToUser = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortOrder = null)
    {
        var itemTypes = GetItemTypes(type);
        var needsPostFilter = !string.IsNullOrEmpty(ratingFilter);

        if (needsPostFilter)
        {
            return GetItemsWithPostFilter(startIndex, limit, itemTypes, ratingFilter!, rating, visibleToUser, searchTerm, sortBy, sortOrder);
        }

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = itemTypes,
            Recursive = true,
            StartIndex = startIndex,
            Limit = limit,
            OrderBy = GetSortOrder(sortBy, sortOrder)
        };

        ApplyCommonFilters(query, visibleToUser, searchTerm, rating);

        var result = _libraryManager.QueryItems(query);

        return Ok(new RatingItemsResponse
        {
            Items = result.Items.Select(MapToDto).ToList(),
            TotalRecordCount = result.TotalRecordCount,
            StartIndex = startIndex
        });
    }

    private ActionResult<RatingItemsResponse> GetItemsWithPostFilter(
        int startIndex,
        int limit,
        BaseItemKind[] itemTypes,
        string ratingFilter,
        string? rating,
        Guid? visibleToUser,
        string? searchTerm,
        string? sortBy,
        string? sortOrder)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = itemTypes,
            Recursive = true,
            OrderBy = GetSortOrder(sortBy, sortOrder)
        };

        ApplyCommonFilters(query, visibleToUser, searchTerm, rating);

        var allItems = _libraryManager.GetItemList(query);

        var filtered = ratingFilter.ToUpperInvariant() switch
        {
            "NONE" => allItems.Where(i => string.IsNullOrEmpty(i.OfficialRating)),
            "NOAU" => allItems.Where(i => !string.IsNullOrEmpty(i.OfficialRating) && !AuRatingHelper.HasAuRating(i.OfficialRating)),
            "HASAU" => allItems.Where(i => AuRatingHelper.HasAuRating(i.OfficialRating)),
            _ => allItems.AsEnumerable()
        };

        var filteredList = filtered.ToList();
        var page = filteredList.Skip(startIndex).Take(limit).ToList();

        return Ok(new RatingItemsResponse
        {
            Items = page.Select(MapToDto).ToList(),
            TotalRecordCount = filteredList.Count,
            StartIndex = startIndex
        });
    }

    [HttpGet("Ratings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<string>> GetRatings()
    {
        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes =
            [
                BaseItemKind.Movie,
                BaseItemKind.Series,
                BaseItemKind.Season,
                BaseItemKind.Episode
            ],
            Recursive = true
        });

        var ratings = items
            .Select(i => i.OfficialRating)
            .Where(r => !string.IsNullOrEmpty(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(ratings);
    }

    [HttpGet("Users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<object>> GetUsers()
    {
        var users = _userManager.Users
            .Select(u => new
            {
                u.Id,
                u.Username,
                HasParentalControls = u.MaxParentalRatingScore.HasValue
            })
            .OrderBy(u => u.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(users);
    }

    [HttpPost("SetRating")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetRating([FromBody] SetRatingRequest request)
    {
        if (!AuRatingHelper.IsValidAuRating(request.Rating))
        {
            return BadRequest(new { Success = false, Message = "Invalid AU rating" });
        }

        var item = _libraryManager.GetItemById<BaseItem>(request.ItemId);
        if (item is null)
        {
            return NotFound();
        }

        await SetRatingOnItem(item, request.Rating).ConfigureAwait(false);

        if (item is Series)
        {
            await PropagateRatingToChildren(item, request.Rating).ConfigureAwait(false);
        }

        _logger.LogInformation("Set rating '{Rating}' on {Type} '{Name}'", request.Rating, item.GetType().Name, item.Name);

        return Ok(new { Success = true });
    }

    [HttpPost("ClearRating")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ClearRating([FromBody] ClearRatingRequest request)
    {
        var item = _libraryManager.GetItemById<BaseItem>(request.ItemId);
        if (item is null)
        {
            return NotFound();
        }

        await ClearRatingOnItem(item).ConfigureAwait(false);

        if (item is Series)
        {
            await PropagateClearToChildren(item).ConfigureAwait(false);
        }

        _logger.LogInformation("Cleared rating on {Type} '{Name}'", item.GetType().Name, item.Name);

        return Ok(new { Success = true });
    }

    private void ApplyCommonFilters(InternalItemsQuery query, Guid? visibleToUser, string? searchTerm, string? rating)
    {
        if (visibleToUser.HasValue)
        {
            var user = _userManager.GetUserById(visibleToUser.Value);
            if (user is not null)
            {
                query.User = user;

                if (user.MaxParentalRatingScore.HasValue)
                {
                    query.MaxParentalRating = new ParentalRatingScore(user.MaxParentalRatingScore.Value, user.MaxParentalRatingSubScore);
                }

                var blockedUnratedItems = user.GetPreferenceValues<UnratedItem>(PreferenceKind.BlockUnratedItems);
                if (blockedUnratedItems.Length > 0)
                {
                    query.BlockUnratedItems = blockedUnratedItems;
                }
            }
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query.SearchTerm = searchTerm;
        }

        if (!string.IsNullOrEmpty(rating))
        {
            query.OfficialRatings = [rating];
        }
    }

    private static BaseItemKind[] GetItemTypes(string? type)
    {
        if (string.IsNullOrEmpty(type))
        {
            return
            [
                BaseItemKind.Movie,
                BaseItemKind.Series,
                BaseItemKind.Season,
                BaseItemKind.Episode
            ];
        }

        return type.ToUpperInvariant() switch
        {
            "MOVIE" => [BaseItemKind.Movie],
            "SERIES" => [BaseItemKind.Series],
            "SEASON" => [BaseItemKind.Season],
            "EPISODE" => [BaseItemKind.Episode],
            _ =>
            [
                BaseItemKind.Movie,
                BaseItemKind.Series,
                BaseItemKind.Season,
                BaseItemKind.Episode
            ]
        };
    }

    private static List<(ItemSortBy SortBy, SortOrder SortOrder)> GetSortOrder(string? sortBy, string? sortOrder)
    {
        var direction = string.Equals(sortOrder, "Descending", StringComparison.OrdinalIgnoreCase)
            ? SortOrder.Descending
            : SortOrder.Ascending;

        var field = sortBy?.ToUpperInvariant() switch
        {
            "OFFICIALRATING" => ItemSortBy.OfficialRating,
            "PRODUCTIONYEAR" => ItemSortBy.ProductionYear,
            "DATECREATED" => ItemSortBy.DateCreated,
            _ => ItemSortBy.SortName
        };

        return [(field, direction)];
    }

    private static RatingItemDto MapToDto(BaseItem item)
    {
        var dto = new RatingItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Type = item.GetBaseItemKind().ToString(),
            OfficialRating = item.OfficialRating,
            SuggestedAuRating = AuRatingHelper.SuggestAuRating(item.OfficialRating),
            HasAuRating = AuRatingHelper.HasAuRating(item.OfficialRating),
            ProductionYear = item.ProductionYear,
            HasPrimaryImage = item.HasImage(ImageType.Primary)
        };

        if (dto.HasPrimaryImage)
        {
            var imageInfo = item.GetImageInfo(ImageType.Primary, 0);
            dto.PrimaryImageTag = imageInfo?.DateModified.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        if (item is Episode episode)
        {
            dto.SeriesName = episode.SeriesName;
        }
        else if (item is Season season)
        {
            dto.SeriesName = season.SeriesName;
        }

        return dto;
    }

    private static async Task SetRatingOnItem(BaseItem item, string rating)
    {
        item.OfficialRating = rating;

        if (!item.LockedFields.Contains(MetadataField.OfficialRating))
        {
            item.LockedFields = item.LockedFields.Append(MetadataField.OfficialRating).ToArray();
        }

        item.OnMetadataChanged();
        await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task ClearRatingOnItem(BaseItem item)
    {
        item.OfficialRating = null;
        item.LockedFields = item.LockedFields.Where(f => f != MetadataField.OfficialRating).ToArray();
        item.OnMetadataChanged();
        await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task PropagateRatingToChildren(BaseItem parent, string rating)
    {
        var children = _libraryManager.GetItemList(new InternalItemsQuery
        {
            AncestorIds = [parent.Id],
            IncludeItemTypes = [BaseItemKind.Season, BaseItemKind.Episode],
            Recursive = true
        });

        foreach (var child in children)
        {
            if (child.LockedFields.Contains(MetadataField.OfficialRating))
            {
                _logger.LogDebug("Skipping {Name} - OfficialRating locked", child.Name);
                continue;
            }

            await SetRatingOnItem(child, rating).ConfigureAwait(false);
        }

        _logger.LogInformation("Propagated rating '{Rating}' to {Count} children of '{Name}'", rating, children.Count, parent.Name);
    }

    private async Task PropagateClearToChildren(BaseItem parent)
    {
        var children = _libraryManager.GetItemList(new InternalItemsQuery
        {
            AncestorIds = [parent.Id],
            IncludeItemTypes = [BaseItemKind.Season, BaseItemKind.Episode],
            Recursive = true
        });

        foreach (var child in children)
        {
            await ClearRatingOnItem(child).ConfigureAwait(false);
        }

        _logger.LogInformation("Cleared rating on {Count} children of '{Name}'", children.Count, parent.Name);
    }
}
