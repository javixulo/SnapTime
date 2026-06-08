// [F5] ApiConfig — exposes the API base URL for building absolute URLs
namespace SnapTime.Client.Services;

public class ApiConfig
{
    public string BaseUrl { get; init; } = "http://localhost:5000";
}
