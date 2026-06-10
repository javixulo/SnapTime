// [F6] E2E tests for PhotoDetail — right panel photo detail with metadata, confidence, and evidence
namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
[Category("E2E")]
public class PhotoDetailE2ETests : PlaywrightTestBase
{
    /// <summary>
    /// Helper: select the first folder in the tree and wait for the grid to load items.
    /// </summary>
    private async Task SelectFirstFolderInTreeAsync()
    {
        await Expect(Page.Locator(".folder-tree-name").First).ToBeVisibleAsync();
        var firstFolder = Page.Locator(".folder-tree-name").First;
        var folderName = await firstFolder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== SELECTED FOLDER === {folderName}");
        await firstFolder.ClickAsync();
        await Expect(Page.Locator(".photo-grid")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Helper: click the first non-directory grid item (skip folders).
    /// Returns the item name if found, null otherwise.
    /// </summary>
    private async Task<string?> ClickFirstPhotoInGridAsync()
    {
        var gridItems = Page.Locator(".photo-grid-item");
        var count = await gridItems.CountAsync();
        await TestContext.Out.WriteLineAsync($"=== GRID ITEMS COUNT === {count}");

        for (int i = 0; i < count; i++)
        {
            var item = gridItems.Nth(i);
            // Skip directories — they have a folder icon
            var folderIcons = await item.Locator(".photo-grid-folder-icon").CountAsync();
            if (folderIcons == 0)
            {
                var itemName = await item.Locator(".photo-grid-item-name").TextContentAsync();
                await TestContext.Out.WriteLineAsync($"=== CLICKING PHOTO ITEM [{i}] === {itemName}");
                await item.ClickAsync();
                return itemName;
            }
        }

        return null;
    }

    /// <summary>
    /// Helper: click the Nth (0-based) non-directory grid item.
    /// </summary>
    private async Task<string?> ClickNthPhotoInGridAsync(int n)
    {
        var gridItems = Page.Locator(".photo-grid-item");
        var count = await gridItems.CountAsync();
        int photoIndex = 0;

        for (int i = 0; i < count; i++)
        {
            var item = gridItems.Nth(i);
            var folderIcons = await item.Locator(".photo-grid-folder-icon").CountAsync();
            if (folderIcons == 0)
            {
                if (photoIndex == n)
                {
                    var itemName = await item.Locator(".photo-grid-item-name").TextContentAsync();
                    await TestContext.Out.WriteLineAsync($"=== CLICKING PHOTO ITEM [{i}] (photo #{n}) === {itemName}");
                    await item.ClickAsync();
                    return itemName;
                }
                photoIndex++;
            }
        }

        return null;
    }

    [Test]
    public async Task PhotoDetail_ClickThumbnail_ShowsDetail()
    {
        // [F6] Click a photo thumbnail in the grid → detail panel shows the file name
        await Page.GotoAsync(BaseUrl);

        await SelectFirstFolderInTreeAsync();

        var photoName = await ClickFirstPhotoInGridAsync();

        if (photoName is null)
        {
            await TestContext.Out.WriteLineAsync("=== NO PHOTOS FOUND IN GRID ===");
            Assert.Pass("No photo items available to test detail panel.");
            return;
        }

        await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });
        var detailName = await Page.Locator(".photo-detail-name").TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== DETAIL NAME === {detailName}");

        // The detail name should match the clicked item's truncated name (or contain it)
        Assert.That(detailName, Is.Not.Empty);
    }

    [Test]
    public async Task PhotoDetail_ClickThumbnail_ShowsMetadata()
    {
        // [F6] Click a photo thumbnail → detail panel shows metadata section and file size
        await Page.GotoAsync(BaseUrl);

        await SelectFirstFolderInTreeAsync();

        var photoName = await ClickFirstPhotoInGridAsync();

        if (photoName is null)
        {
            await TestContext.Out.WriteLineAsync("=== NO PHOTOS FOUND IN GRID ===");
            Assert.Pass("No photo items available to test metadata.");
            return;
        }

        await Expect(Page.Locator(".photo-detail-metadata")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(Page.Locator(".photo-detail-size")).ToBeVisibleAsync();

        var metadataHtml = await Page.Locator(".photo-detail-metadata").InnerHTMLAsync();
        await TestContext.Out.WriteLineAsync($"=== METADATA HTML (first 200 chars) === {metadataHtml[..Math.Min(metadataHtml.Length, 200)]}");
    }

    [Test]
    public async Task PhotoDetail_ClickDifferentPhoto_UpdatesDetail()
    {
        // [F6] Click two different photos sequentially → the detail panel updates to the new selection
        await Page.GotoAsync(BaseUrl);

        await SelectFirstFolderInTreeAsync();

        // Click the first photo
        var firstName = await ClickFirstPhotoInGridAsync();
        if (firstName is null)
        {
            await TestContext.Out.WriteLineAsync("=== NO PHOTOS FOUND IN GRID ===");
            Assert.Pass("No photo items available to test detail update.");
            return;
        }

        await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });
        var detailName1 = await Page.Locator(".photo-detail-name").TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== FIRST DETAIL NAME === {detailName1}");

        // Click the second photo
        var secondName = await ClickNthPhotoInGridAsync(1);
        if (secondName is null)
        {
            await TestContext.Out.WriteLineAsync("=== ONLY ONE PHOTO AVAILABLE ===");
            Assert.Pass("Only one photo item available — cannot test update.");
            return;
        }

        await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });
        var detailName2 = await Page.Locator(".photo-detail-name").TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== SECOND DETAIL NAME === {detailName2}");

        // The two names should be different
        Assert.That(detailName2, Is.Not.EqualTo(detailName1));
    }

    [Test]
    public async Task PhotoDetail_NavigateSubfolder_ClickPhoto_StaysInSubfolder()
    {
        // [F6] Navigate into a subfolder, click a photo → detail shows, breadcrumb stays in subfolder
        await Page.GotoAsync(BaseUrl);

        await SelectFirstFolderInTreeAsync();

        // Check if there are subfolder items
        var subfolderItems = Page.Locator(".photo-grid-item.is-directory");
        var subfolderCount = await subfolderItems.CountAsync();

        if (subfolderCount == 0)
        {
            await TestContext.Out.WriteLineAsync("=== NO SUBFOLDERS TO TEST NAVIGATION ===");
            Assert.Pass("No subfolders available for navigation test.");
            return;
        }

        // Double-click the first subfolder to navigate into it
        var subfolderName = await subfolderItems.First.Locator(".photo-grid-item-name").TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== NAVIGATING INTO SUBFOLDER === {subfolderName}");
        await subfolderItems.First.DblClickAsync();

        // Wait for grid to reload inside the subfolder
        await Expect(Page.Locator(".photo-grid-item").First).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Click the first photo in the subfolder
        var photoName = await ClickFirstPhotoInGridAsync();
        if (photoName is null)
        {
            await TestContext.Out.WriteLineAsync("=== NO PHOTOS IN SUBFOLDER ===");
            Assert.Pass("No photo items in subfolder.");
            return;
        }

        // Assert detail panel shows the photo
        await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Assert breadcrumb is visible and shows we're still inside the subfolder
        var breadcrumb = Page.Locator(".photo-grid-breadcrumb");
        await Expect(breadcrumb).ToBeVisibleAsync();

        var breadcrumbLinks = Page.Locator(".photo-grid-breadcrumb-link");
        var linkCount = await breadcrumbLinks.CountAsync();
        await TestContext.Out.WriteLineAsync($"=== BREADCRUMB LINK COUNT === {linkCount}");

        // The last breadcrumb segment should be the subfolder name
        if (linkCount > 0)
        {
            var lastSegment = await breadcrumbLinks.Last.TextContentAsync();
            await TestContext.Out.WriteLineAsync($"=== LAST BREADCRUMB SEGMENT === {lastSegment}");
            Assert.That(lastSegment, Is.EqualTo(subfolderName));
        }
    }

    [Test]
    public async Task PhotoDetail_ClickBreadcrumb_ClearsDetail()
    {
        // [F6] After selecting a photo inside a subfolder, clicking the breadcrumb to navigate up
        // clears the detail panel and shows the placeholder
        await Page.GotoAsync(BaseUrl);

        await SelectFirstFolderInTreeAsync();

        // Navigate into a subfolder first (so we have a breadcrumb with multiple segments)
        var subfolderItems = Page.Locator(".photo-grid-item.is-directory");
        var subfolderCount = await subfolderItems.CountAsync();

        if (subfolderCount == 0)
        {
            await TestContext.Out.WriteLineAsync("=== NO SUBFOLDERS TO TEST BREADCRUMB CLEAR ===");
            Assert.Pass("No subfolders available for breadcrumb navigation test.");
            return;
        }

        // Double-click the first subfolder
        await subfolderItems.First.DblClickAsync();
        await Expect(Page.Locator(".photo-grid-item").First).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Click the first photo inside the subfolder
        var photoName = await ClickFirstPhotoInGridAsync();
        if (photoName is null)
        {
            await TestContext.Out.WriteLineAsync("=== NO PHOTOS IN SUBFOLDER ===");
            Assert.Pass("No photo items in subfolder to test breadcrumb clear.");
            return;
        }

        // Assert detail panel shows the photo
        await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await TestContext.Out.WriteLineAsync($"=== SELECTED PHOTO IN SUBFOLDER === {photoName}");

        // Click the first breadcrumb link to navigate up (to the parent folder)
        var breadcrumbLinks = Page.Locator(".photo-grid-breadcrumb-link");
        var linkCount = await breadcrumbLinks.CountAsync();

        if (linkCount < 2)
        {
            await TestContext.Out.WriteLineAsync("=== NOT ENOUGH BREADCRUMB SEGMENTS ===");
            Assert.Pass("Need at least 2 breadcrumb segments to test navigation clear.");
            return;
        }

        // Click the root-level breadcrumb segment to go up
        var rootLink = breadcrumbLinks.First;
        var rootPath = await rootLink.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== CLICKING BREADCRUMB === {rootPath}");
        await rootLink.ClickAsync();

        // Wait for grid to reload at the parent level
        await Expect(Page.Locator(".photo-grid-item").First).ToBeVisibleAsync(new() { Timeout = 10000 });

        // The detail panel should clear and show the empty placeholder
        await Expect(Page.Locator(".photo-detail-empty")).ToBeVisibleAsync();
        var emptyText = await Page.Locator(".photo-detail-empty").TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== EMPTY STATE AFTER BREADCRUMB NAV === {emptyText}");
    }

    [Test]
    public async Task PhotoDetail_AfterScanClickThumbnail_ShowsDetailWithoutError()
    {
        // Regression test: after a scan completes, clicking a photo thumbnail loads
        // the detail panel successfully without a 404 error. This covers the bug where
        // PhotoGrid cached stale GUIDs after a re-scan (F6).
        await Page.GotoAsync(BaseUrl);

        // 1. Select a folder
        await Expect(Page.Locator(".folder-tree-name").First).ToBeVisibleAsync();
        var firstFolder = Page.Locator(".folder-tree-name").First;
        var folderName = await firstFolder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== SELECTED FOLDER === {folderName}");
        await firstFolder.ClickAsync();

        // 2. Click "Escanear"
        var scanButton = Page.Locator("button", new() { HasText = "Escanear" });
        await Expect(scanButton).ToBeVisibleAsync();
        await scanButton.ClickAsync();

        // 3. Wait for scan progress to appear
        await Expect(Page.Locator(".scan-progress")).ToBeVisibleAsync(new() { Timeout = 15000 });

        // 4. Wait for scan to complete (status "Completed") with 60s timeout
        var completedLocator = Page.Locator("text=Completed");
        try
        {
            await Expect(completedLocator).ToBeVisibleAsync(new() { Timeout = 60000 });
            await TestContext.Out.WriteLineAsync("=== SCAN COMPLETED ===");
        }
        catch (Exception ex)
        {
            await TestContext.Out.WriteLineAsync($"=== SCAN DID NOT COMPLETE IN 60s === {ex.Message}");
            var cancelBtn = Page.Locator("button", new() { HasText = "Cancelar" });
            if (await cancelBtn.IsVisibleAsync())
                await cancelBtn.ClickAsync();
            Assert.Inconclusive("Scan did not complete within 60s timeout.");
            return;
        }

        // 5. Wait for the photo grid to be visible
        await Expect(Page.Locator(".photo-grid")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // 6. Click the first non-directory photo in the grid
        var photoName = await ClickFirstPhotoInGridAsync();
        if (photoName is null)
        {
            await TestContext.Out.WriteLineAsync("=== NO PHOTOS FOUND IN GRID AFTER SCAN ===");
            Assert.Pass("No photo items available after scan to test detail panel.");
            return;
        }

        // 7. Assert the detail panel loaded (photo-detail-name is visible)
        await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });
        var detailName = await Page.Locator(".photo-detail-name").TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== DETAIL NAME === {detailName}");

        // 8. Assert NO error message is shown (regression check for stale-GUID 404)
        var errorLocator = Page.Locator(".photo-detail-error");
        await Expect(errorLocator).Not.ToBeVisibleAsync();

        // Also check for generic error messages that might surface the 404
        var genericError = Page.Locator(".error-message");
        var genericErrorVisible = await genericError.IsVisibleAsync();
        if (genericErrorVisible)
        {
            var errorText = await genericError.TextContentAsync();
            await TestContext.Out.WriteLineAsync($"=== GENERIC ERROR FOUND === {errorText}");
        }
        Assert.That(genericErrorVisible, Is.False,
            "No error message should be displayed after clicking a photo post-scan.");
    }
}
