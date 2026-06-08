// [F5] PhotoGridItem DTO — represents a single entry in the photo grid
namespace SnapTime.Client.Models;

public class PhotoGridItem
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string MediaStatus { get; set; } = "Pending";
    public bool HasSuggestion { get; set; }
    public string MediaType { get; set; } = "Image";
}
