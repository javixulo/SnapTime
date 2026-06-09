// [F5] Unit tests for PhotoClient service
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using SnapTime.Client.Models;
using SnapTime.Client.Services;

namespace SnapTime.Client.Tests.Services;

public class PhotoClientTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task photoClient_GetPhotos_returnsItems()
    {
        // [F5] GetPhotosAsync returns the list of items from the API
        var expected = new PhotoGridResponse
        {
            Items =
            [
                new() { Name = "photo1.jpg", Path = "/test/photo1.jpg", IsDirectory = false },
                new() { Name = "photo2.jpg", Path = "/test/photo2.jpg", IsDirectory = false }
            ],
            TotalCount = 2,
            Page = 1
        };

        var httpClient = CreateMockHttpClient(expected, HttpStatusCode.OK);
        var client = new PhotoClient(httpClient);

        var result = await client.GetPhotosAsync("/test", 1, 50);

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(i => i.Name == "photo1.jpg");
    }

    [Fact]
    public async Task photoClient_GetPhotos_passesPagination()
    {
        // [F5] Verify that page and pageSize are sent as query parameters
        var expected = new PhotoGridResponse { Items = [], TotalCount = 0, Page = 2 };
        HttpRequestMessage? capturedRequest = null;

        var handler = new MockHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var client = new PhotoClient(httpClient);

        await client.GetPhotosAsync("/test", page: 2, pageSize: 25);

        capturedRequest.Should().NotBeNull();
        capturedRequest!.RequestUri!.Query.Should().Contain("page=2");
        capturedRequest.RequestUri.Query.Should().Contain("pageSize=25");
    }

    [Fact]
    public async Task photoClient_GetPhotos_handlesError()
    {
        // [F5] When the API returns an error, the client throws or returns an empty response
        var httpClient = CreateMockHttpClient("Server error", HttpStatusCode.InternalServerError);
        var client = new PhotoClient(httpClient);

        Func<Task<PhotoGridResponse>> act = () => client.GetPhotosAsync("/test", 1, 50);

        // Expect an exception (HttpRequestException) when the status code is not success
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task photoClient_GetAssetDetail_ReturnsDetail()
    {
        // [F6] GetAssetDetailAsync returns the media asset detail from the API
        var assetId = Guid.NewGuid();
        var expected = new MediaAssetDetailDto
        {
            Id = assetId,
            FilePath = "/test/photo.jpg",
            FileName = "photo.jpg",
            MediaType = "Image",
            FileSize = 2048,
            DateTimeOriginal = new DateTime(2024, 1, 15, 10, 0, 0),
            SubSecDateTimeOriginal = null,
            CreateDate = new DateTime(2024, 1, 15, 10, 0, 0),
            ModifyDate = new DateTime(2024, 1, 16, 12, 0, 0),
            FileCreatedAt = new DateTime(2024, 1, 15, 10, 0, 0),
            FileModifiedAt = new DateTime(2024, 1, 16, 12, 0, 0),
            ConfidenceScore = 90,
            SuggestedDate = new DateTime(2024, 1, 15),
            SuggestedByHeuristic = "FilenameHeuristic",
            Evidence =
            [
                new EvidenceDto
                {
                    HeuristicId = "H006",
                    HeuristicName = "Filename heuristic",
                    Weight = 0.9,
                    Direction = "positive",
                    Description = "Filename contains date"
                }
            ]
        };

        var httpClient = CreateMockHttpClient(expected, HttpStatusCode.OK);
        var client = new PhotoClient(httpClient);

        var result = await client.GetAssetDetailAsync(assetId);

        result.Should().NotBeNull();
        result.Id.Should().Be(assetId);
        result.FileName.Should().Be("photo.jpg");
        result.Evidence.Should().HaveCount(1);
    }

    [Fact]
    public async Task photoClient_GetAssetDetail_HandlesError()
    {
        // [F6] When the API returns an error, GetAssetDetailAsync throws
        var assetId = Guid.NewGuid();
        var httpClient = CreateMockHttpClient("Server error", HttpStatusCode.InternalServerError);
        var client = new PhotoClient(httpClient);

        Func<Task<MediaAssetDetailDto>> act = () => client.GetAssetDetailAsync(assetId);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    // ──────────────────────────────────────────────
    // File metadata (F6 fallback for unscanned files)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task photoClient_GetFileMetadata_ReturnsMetadata()
    {
        // [F6] GetFileMetadataAsync returns FileMetadataDto from the API
        var expected = new FileMetadataDto
        {
            FilePath = "/test/photo.jpg",
            FileName = "photo.jpg",
            FileSize = 2048,
            DateTimeOriginal = new DateTime(2024, 1, 15, 10, 0, 0),
            SubSecDateTimeOriginal = null,
            CreateDate = new DateTime(2024, 1, 15, 10, 0, 0),
            ModifyDate = new DateTime(2024, 1, 16, 12, 0, 0),
            FileCreatedAt = new DateTime(2024, 1, 15, 10, 0, 0),
            FileModifiedAt = new DateTime(2024, 1, 16, 12, 0, 0)
        };

        var httpClient = CreateMockHttpClient(expected, HttpStatusCode.OK);
        var client = new PhotoClient(httpClient);

        var result = await client.GetFileMetadataAsync("/test/photo.jpg");

        result.Should().NotBeNull();
        result.FileName.Should().Be("photo.jpg");
        result.FilePath.Should().Be("/test/photo.jpg");
        result.FileSize.Should().Be(2048);
        result.DateTimeOriginal.Should().Be(new DateTime(2024, 1, 15, 10, 0, 0));
        result.FileCreatedAt.Should().Be(new DateTime(2024, 1, 15, 10, 0, 0));
    }

    [Fact]
    public async Task photoClient_GetFileMetadata_HandlesError()
    {
        // [F6] When the API returns an error, GetFileMetadataAsync throws
        var httpClient = CreateMockHttpClient("Server error", HttpStatusCode.InternalServerError);
        var client = new PhotoClient(httpClient);

        Func<Task<FileMetadataDto>> act = () => client.GetFileMetadataAsync("/test/photo.jpg");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static HttpClient CreateMockHttpClient(object responseContent, HttpStatusCode statusCode)
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(responseContent)
            });
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
    }
}
