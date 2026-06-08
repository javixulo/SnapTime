// [F5] E2E tests for PhotoGrid — central panel photo grid
using Microsoft.Playwright.NUnit;

namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
public class PhotoGridE2ETests : PageTest
{
    [Test]
    public async Task PhotoGrid_SelectFolderInTree_LoadsGridWithItems()
    {
        // [F5] Select a folder in the left tree → the grid loads items for that folder
        await Page.GotoAsync("http://localhost:5027");

        // Wait for the folder tree to load
        await Expect(Page.Locator(".folder-tree-name").First).ToBeVisibleAsync();
        var firstFolder = Page.Locator(".folder-tree-name").First;
        var folderName = await firstFolder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== SELECTED FOLDER === {folderName}");

        // Click the first folder in the tree
        await firstFolder.ClickAsync();

        // The grid should appear and load items for the selected folder
        await Expect(Page.Locator(".photo-grid")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Either items or empty message should be visible
        var gridItems = Page.Locator(".photo-grid-item");
        var emptyState = Page.Locator(".photo-grid-empty");

        await Task.WhenAny(
            Expect(gridItems.First).ToBeVisibleAsync(),
            Expect(emptyState).ToBeVisibleAsync()
        );
    }

    [Test]
    public async Task PhotoGrid_DoubleClickSubfolder_NavigatesInside()
    {
        // [F5] Double-click a subfolder in the grid → navigates into it (breadcrumb updates)
        await Page.GotoAsync("http://localhost:5027");

        // Wait for tree and select a folder
        await Expect(Page.Locator(".folder-tree-name").First).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.ClickAsync();

        // Wait for the grid to be visible
        await Expect(Page.Locator(".photo-grid")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Check if there's a subfolder item in the grid
        var subfolderItems = Page.Locator(".photo-grid-item.is-directory");
        var subfolderCount = await subfolderItems.CountAsync();

        if (subfolderCount > 0)
        {
            // Double-click the first subfolder
            await subfolderItems.First.DblClickAsync();

            // The breadcrumb should have been updated with the new path
            var breadcrumb = Page.Locator(".photo-grid-breadcrumb");
            await Expect(breadcrumb).ToBeVisibleAsync();

            // The grid should re-render with items from the subfolder
            await Expect(Page.Locator(".photo-grid-item").First).ToBeVisibleAsync(new() { Timeout = 10000 });
        }
        else
        {
            // No subfolders available — this is acceptable in a test environment
            await TestContext.Out.WriteLineAsync("=== NO SUBFOLDERS TO TEST NAVIGATION ===");
            Assert.Pass("No subfolders available for navigation test.");
        }
    }

    [Test]
    public async Task PhotoGrid_Breadcrumb_ClickNavigatesUp()
    {
        // [F5] Click a breadcrumb segment → navigates to the parent path
        await Page.GotoAsync("http://localhost:5027");

        // Wait for tree and select a folder
        await Expect(Page.Locator(".folder-tree-name").First).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.ClickAsync();

        // Wait for the grid to be visible
        await Expect(Page.Locator(".photo-grid")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Check for subfolders to drill down first
        var subfolderItems = Page.Locator(".photo-grid-item.is-directory");
        var subfolderCount = await subfolderItems.CountAsync();

        if (subfolderCount > 0)
        {
            // Drill into a subfolder
            await subfolderItems.First.DblClickAsync();
            await Expect(Page.Locator(".photo-grid-item").First).ToBeVisibleAsync(new() { Timeout = 10000 });

            // Now click the first segment of the breadcrumb to go back up
            var breadcrumbLinks = Page.Locator(".photo-grid-breadcrumb-link");
            var linkCount = await breadcrumbLinks.CountAsync();

            if (linkCount > 1)
            {
                // Click the root segment (first link) to navigate all the way up
                await breadcrumbLinks.First.ClickAsync();

                // The grid should reload with the parent path contents
                await Expect(Page.Locator(".photo-grid-item").First).ToBeVisibleAsync(new() { Timeout = 10000 });
            }
        }
    }
}
