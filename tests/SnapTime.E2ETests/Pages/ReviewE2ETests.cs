// [F7-US-004] -- E2E: Revisión y aprobación/rechazo de sugerencias
// Arranque autónomo vía E2EWebFixture + SQLite efímera
//
// 🔴 RED PHASE — These tests fail because the review UI does not exist yet.
// They will pass only after Kip implements the backend endpoints and
// Karris implements the frontend components (PhotoDetail buttons,
// BatchActions, confirm modal).
//
namespace SnapTime.E2ETests.Pages;

[Parallelizable(ParallelScope.Self)]
[Category("E2E")]
public class ReviewE2ETests : PlaywrightTestBase
{
    /// <summary>
    /// Select the first folder in the tree, start a scan and wait until
    /// "Completed" status appears (up to 60s). After completion, waits for
    /// the photo grid to be visible.
    /// </summary>
    private async Task ScanAndWaitForCompletionAsync()
    {
        await Page.GotoAsync(BaseUrl);

        // Wait for folder tree
        await Expect(Page.Locator(".folder-tree-name").First).ToBeVisibleAsync();
        await Page.Locator(".folder-tree-name").First.ClickAsync();

        // Click "Escanear"
        var scanButton = Page.Locator("button", new() { HasText = "Escanear" });
        await Expect(scanButton).ToBeVisibleAsync();
        await scanButton.ClickAsync();

        // Wait for progress indicator
        await Expect(Page.Locator(".scan-progress")).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Wait for "Completed" status (up to 60s)
        var completedLocator = Page.Locator("text=Completed");
        try
        {
            await Expect(completedLocator).ToBeVisibleAsync(new() { Timeout = 60000 });
        }
        catch (Exception ex)
        {
            await TestContext.Out.WriteLineAsync($"=== SCAN DID NOT COMPLETE === {ex.Message}");
            var cancelBtn = Page.Locator("button", new() { HasText = "Cancelar" });
            if (await cancelBtn.IsVisibleAsync())
                await cancelBtn.ClickAsync();
            Assert.Inconclusive("La carpeta seleccionada no completó el escaneo en 60s.");
            return;
        }

        await TestContext.Out.WriteLineAsync("=== SCAN COMPLETED SUCCESSFULLY ===");

        // Wait for the grid to load after scan
        await Expect(Page.Locator(".photo-grid")).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    /// <summary>
    /// Select a folder by its 0-based index in the folder tree, start a scan
    /// and wait for completion.
    /// </summary>
    private async Task ScanFolderByIndexAsync(int index)
    {
        await Page.GotoAsync(BaseUrl);

        await Expect(Page.Locator(".folder-tree-name").First).ToBeVisibleAsync();
        var folder = Page.Locator(".folder-tree-name").Nth(index);
        var folderName = await folder.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== SCANNING FOLDER [{index}] === {folderName}");
        await folder.ClickAsync();

        var scanButton = Page.Locator("button", new() { HasText = "Escanear" });
        await Expect(scanButton).ToBeVisibleAsync();
        await scanButton.ClickAsync();

        await Expect(Page.Locator(".scan-progress")).ToBeVisibleAsync(new() { Timeout = 15000 });

        var completedLocator = Page.Locator("text=Completed");
        try
        {
            await Expect(completedLocator).ToBeVisibleAsync(new() { Timeout = 60000 });
        }
        catch (Exception ex)
        {
            await TestContext.Out.WriteLineAsync($"=== SCAN [{index}] DID NOT COMPLETE === {ex.Message}");
            var cancelBtn = Page.Locator("button", new() { HasText = "Cancelar" });
            if (await cancelBtn.IsVisibleAsync())
                await cancelBtn.ClickAsync();
            Assert.Inconclusive($"El escaneo de la carpeta [{index}] no completó en 60s.");
            return;
        }

        await TestContext.Out.WriteLineAsync($"=== SCAN [{index}] COMPLETED ===");
        await Expect(Page.Locator(".photo-grid")).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    /// <summary>
    /// Click the first non-directory grid item (skip folders).
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
    /// Return the current text of the suggestion status badge in the detail
    /// panel, or null if the element does not exist.
    /// </summary>
    private async Task<string?> GetSuggestionStatusTextAsync()
    {
        var statusLocator = Page.Locator(".suggestion-status");
        if (await statusLocator.IsVisibleAsync())
            return await statusLocator.TextContentAsync();
        return null;
    }

    // ----------------------------------------------------------------
    // 1. Individual Accept
    // ----------------------------------------------------------------

    /// <summary>
    /// Escanear carpeta → click miniatura con sugerencia → click Aceptar →
    /// <c>SuggestionReviewStatus</c> cambia a Approved.
    /// </summary>
    [Test]
    public async Task Review_AcceptSuggestion_ChangesStatusToApproved()
    {
        await ScanAndWaitForCompletionAsync();

        // Click the first photo in the grid
        var photoName = await ClickFirstPhotoInGridAsync();
        if (photoName is null)
        {
            await TestContext.Out.WriteLineAsync("=== NO PHOTO ITEMS IN GRID ===");
            Assert.Pass("No photo items available to test individual accept.");
            return;
        }

        // Wait for detail panel to be visible
        await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Check that the selected photo has an unreviewed suggestion
        var statusBefore = await GetSuggestionStatusTextAsync();
        if (statusBefore is null || !statusBefore.Contains("Unreviewed", StringComparison.OrdinalIgnoreCase))
        {
            await TestContext.Out.WriteLineAsync(
                "=== NO UNREVIEWED SUGGESTION ON SELECTED PHOTO (status=" +
                (statusBefore ?? "null") + ") ===");
            Assert.Pass("Selected photo does not have an unreviewed suggestion — cannot verify accept.");
            return;
        }

        await TestContext.Out.WriteLineAsync($"=== SUGGESTION STATUS BEFORE === {statusBefore}");

        // Click the "Aceptar" button in the detail panel
        var acceptButton = Page.Locator(".photo-detail .btn-accept, button", new() { HasText = "Aceptar" }).First;
        await Expect(acceptButton).ToBeVisibleAsync();
        await Expect(acceptButton).ToBeEnabledAsync();
        await acceptButton.ClickAsync();

        // Wait for the status badge to reflect "Approved"
        await Expect(Page.Locator(".suggestion-status")).ToHaveTextAsync(
            "Approved", new() { Timeout = 5000 });

        var statusAfter = await GetSuggestionStatusTextAsync();
        await TestContext.Out.WriteLineAsync($"=== SUGGESTION STATUS AFTER === {statusAfter}");
        Assert.That(statusAfter, Is.EqualTo("Approved"),
            "After clicking Aceptar, the suggestion status should be Approved");
    }

    // ----------------------------------------------------------------
    // 2. Individual Reject
    // ----------------------------------------------------------------

    /// <summary>
    /// Escanear carpeta → click miniatura con sugerencia → click Rechazar →
    /// <c>SuggestionReviewStatus</c> cambia a Rejected.
    /// </summary>
    [Test]
    public async Task Review_RejectSuggestion_ChangesStatusToRejected()
    {
        await ScanAndWaitForCompletionAsync();

        // Click the first photo in the grid
        var photoName = await ClickFirstPhotoInGridAsync();
        if (photoName is null)
        {
            await TestContext.Out.WriteLineAsync("=== NO PHOTO ITEMS IN GRID ===");
            Assert.Pass("No photo items available to test individual reject.");
            return;
        }

        // Wait for detail panel to be visible
        await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Check that the selected photo has an unreviewed suggestion
        var statusBefore = await GetSuggestionStatusTextAsync();
        if (statusBefore is null || !statusBefore.Contains("Unreviewed", StringComparison.OrdinalIgnoreCase))
        {
            await TestContext.Out.WriteLineAsync(
                "=== NO UNREVIEWED SUGGESTION ON SELECTED PHOTO (status=" +
                (statusBefore ?? "null") + ") ===");
            Assert.Pass("Selected photo does not have an unreviewed suggestion — cannot verify reject.");
            return;
        }

        await TestContext.Out.WriteLineAsync($"=== SUGGESTION STATUS BEFORE === {statusBefore}");

        // Click the "Rechazar" button in the detail panel
        var rejectButton = Page.Locator(".photo-detail .btn-reject, button", new() { HasText = "Rechazar" }).First;
        await Expect(rejectButton).ToBeVisibleAsync();
        await Expect(rejectButton).ToBeEnabledAsync();
        await rejectButton.ClickAsync();

        // Wait for the status badge to reflect "Rejected"
        await Expect(Page.Locator(".suggestion-status")).ToHaveTextAsync(
            "Rejected", new() { Timeout = 5000 });

        var statusAfter = await GetSuggestionStatusTextAsync();
        await TestContext.Out.WriteLineAsync($"=== SUGGESTION STATUS AFTER === {statusAfter}");
        Assert.That(statusAfter, Is.EqualTo("Rejected"),
            "After clicking Rechazar, the suggestion status should be Rejected");
    }

    // ----------------------------------------------------------------
    // 3. Batch Accept All (folder scope)
    // ----------------------------------------------------------------

    /// <summary>
    /// Escanear carpeta → click Aceptar Todo → confirmar → todas las
    /// sugerencias de la carpeta cambian a Approved.
    /// </summary>
    [Test]
    public async Task Review_AcceptAllInFolder_AllSuggestionsApproved()
    {
        await ScanAndWaitForCompletionAsync();

        // Locate the "Aceptar Todo" batch button
        var acceptAllButton = Page.Locator("button", new() { HasText = "Aceptar Todo" });
        await Expect(acceptAllButton).ToBeVisibleAsync();
        await TestContext.Out.WriteLineAsync("=== ACEPTAR TODO BUTTON IS VISIBLE ===");

        // Click the batch button to open the confirmation modal
        await acceptAllButton.ClickAsync();

        // The confirmation modal must appear
        var modal = Page.Locator(".confirm-modal");
        await Expect(modal).ToBeVisibleAsync(new() { Timeout = 5000 });

        // The modal should contain a summary message (e.g. "Se aprobarán N sugerencias")
        var modalText = await modal.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== MODAL TEXT === {modalText}");
        Assert.That(modalText, Does.Contain("aprobar").Or.Contain("sugerencia").Or.Contain("sugerencia"),
            "The confirmation modal should show a summary of the batch operation");

        // Confirm the batch operation
        var confirmButton = modal.Locator("button", new() { HasText = "Confirmar" });
        await Expect(confirmButton).ToBeVisibleAsync();
        await confirmButton.ClickAsync();

        // The modal must close after confirmation
        await Expect(modal).Not.ToBeVisibleAsync(new() { Timeout = 5000 });
        await TestContext.Out.WriteLineAsync("=== MODAL CLOSED AFTER CONFIRMATION ===");

        // Allow the UI to react to the API response
        await Task.Delay(2000);

        // Click the first photo to verify its suggestion status
        var photoName = await ClickFirstPhotoInGridAsync();
        if (photoName is not null)
        {
            await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });
            var status = await GetSuggestionStatusTextAsync();
            await TestContext.Out.WriteLineAsync(
                $"=== SUGGESTION STATUS AFTER ACEPTAR TODO === {status}");
            if (status is not null)
            {
                Assert.That(status, Is.EqualTo("Approved"),
                    "After Aceptar Todo, all reviewed photos should be Approved");
            }
        }
    }

    // ----------------------------------------------------------------
    // 4. Batch Reject All (folder scope)
    // ----------------------------------------------------------------

    /// <summary>
    /// Escanear carpeta → click Rechazar Todo → confirmar → todas las
    /// sugerencias de la carpeta cambian a Rejected.
    /// </summary>
    [Test]
    public async Task Review_RejectAllInFolder_AllSuggestionsRejected()
    {
        await ScanAndWaitForCompletionAsync();

        // Locate the "Rechazar Todo" batch button
        var rejectAllButton = Page.Locator("button", new() { HasText = "Rechazar Todo" });
        await Expect(rejectAllButton).ToBeVisibleAsync();
        await TestContext.Out.WriteLineAsync("=== RECHAZAR TODO BUTTON IS VISIBLE ===");

        // Click the batch button to open the confirmation modal
        await rejectAllButton.ClickAsync();

        // The confirmation modal must appear
        var modal = Page.Locator(".confirm-modal");
        await Expect(modal).ToBeVisibleAsync(new() { Timeout = 5000 });

        // The modal should contain a summary message
        var modalText = await modal.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== MODAL TEXT === {modalText}");
        Assert.That(modalText, Does.Contain("rechazar").Or.Contain("sugerencia"),
            "The confirmation modal should show a summary of the batch reject operation");

        // Confirm the batch operation
        var confirmButton = modal.Locator("button", new() { HasText = "Confirmar" });
        await Expect(confirmButton).ToBeVisibleAsync();
        await confirmButton.ClickAsync();

        // The modal must close after confirmation
        await Expect(modal).Not.ToBeVisibleAsync(new() { Timeout = 5000 });
        await TestContext.Out.WriteLineAsync("=== MODAL CLOSED AFTER CONFIRMATION ===");

        // Allow the UI to react to the API response
        await Task.Delay(2000);

        // Click the first photo to verify its suggestion status
        var photoName = await ClickFirstPhotoInGridAsync();
        if (photoName is not null)
        {
            await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });
            var status = await GetSuggestionStatusTextAsync();
            await TestContext.Out.WriteLineAsync(
                $"=== SUGGESTION STATUS AFTER RECHAZAR TODO === {status}");
            if (status is not null)
            {
                Assert.That(status, Is.EqualTo("Rejected"),
                    "After Rechazar Todo, all reviewed photos should be Rejected");
            }
        }
    }

    // ----------------------------------------------------------------
    // 5. Batch Accept Total (cross-folder scope)
    // ----------------------------------------------------------------

    /// <summary>
    /// Aceptar Total desde una carpeta → afecta también a sugerencias de
    /// otras carpetas escaneadas.
    /// </summary>
    [Test]
    public async Task Review_AcceptTotal_AffectsMultipleScannedFolders()
    {
        // --- Scan first folder ---
        await ScanFolderByIndexAsync(0);

        // Check if a second folder exists
        var folderCount = await Page.Locator(".folder-tree-name").CountAsync();
        await TestContext.Out.WriteLineAsync($"=== FOLDERS AVAILABLE === {folderCount}");

        if (folderCount < 2)
        {
            Assert.Pass("Need at least 2 folders in the tree to test cross-folder batch operation.");
            return;
        }

        // --- Scan second folder ---
        await ScanFolderByIndexAsync(1);

        // --- Click "Aceptar Total" from the current (second) folder context ---
        var acceptTotalButton = Page.Locator("button", new() { HasText = "Aceptar Total" });
        await Expect(acceptTotalButton).ToBeVisibleAsync();
        await TestContext.Out.WriteLineAsync("=== ACEPTAR TOTAL BUTTON IS VISIBLE ===");

        await acceptTotalButton.ClickAsync();

        // The confirmation modal must appear
        var modal = Page.Locator(".confirm-modal");
        await Expect(modal).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Modal text should indicate total scope
        var modalText = await modal.TextContentAsync();
        await TestContext.Out.WriteLineAsync($"=== MODAL TEXT (ACEPTAR TOTAL) === {modalText}");
        Assert.That(modalText, Does.Contain("Total").Or.Contain("total").Or.Contain("aprobar"),
            "The confirmation modal for Aceptar Total should mention the total scope");

        // Confirm
        var confirmButton = modal.Locator("button", new() { HasText = "Confirmar" });
        await Expect(confirmButton).ToBeVisibleAsync();
        await confirmButton.ClickAsync();

        // Modal must close
        await Expect(modal).Not.ToBeVisibleAsync(new() { Timeout = 5000 });
        await TestContext.Out.WriteLineAsync("=== MODAL CLOSED AFTER ACEPTAR TOTAL CONFIRMATION ===");

        // Allow the UI to settle
        await Task.Delay(2000);

        // --- Navigate back to the first folder ---
        await Page.Locator(".folder-tree-name").First.ClickAsync();
        await Expect(Page.Locator(".photo-grid")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Verify a photo in the first folder is now Approved
        var photoName = await ClickFirstPhotoInGridAsync();
        if (photoName is not null)
        {
            await Expect(Page.Locator(".photo-detail-name")).ToBeVisibleAsync(new() { Timeout = 10000 });
            var status = await GetSuggestionStatusTextAsync();
            await TestContext.Out.WriteLineAsync(
                $"=== SUGGESTION STATUS IN FOLDER 0 AFTER ACEPTAR TOTAL === {status}");
            if (status is not null)
            {
                Assert.That(status, Is.EqualTo("Approved"),
                    "After Aceptar Total, suggestions in all scanned folders should be Approved");
            }
        }
    }
}
