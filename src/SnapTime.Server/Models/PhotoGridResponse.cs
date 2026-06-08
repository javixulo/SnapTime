namespace SnapTime.Server.Models;

/// <summary>
/// Paginated response from GET /api/photos.
/// </summary>
/// <param name="Items">List of photo grid items for the current page.</param>
/// <param name="TotalCount">Total number of items across all pages.</param>
/// <param name="Page">Current page number (1-indexed).</param>
public record PhotoGridResponse(
    List<PhotoGridItem> Items,
    int TotalCount,
    int Page
);
