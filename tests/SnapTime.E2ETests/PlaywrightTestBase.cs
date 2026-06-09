// Base class for all E2E Playwright tests.
// Provides access to the dynamic server URL from E2EWebFixture.
using Microsoft.Playwright.NUnit;

namespace SnapTime.E2ETests;

/// <summary>
/// Base class that all SnapTime E2E Playwright test classes should inherit from.
/// Provides a <see cref="BaseUrl"/> property pointing to the running server instance
/// started by <see cref="E2EWebFixture"/>.
/// </summary>
public class PlaywrightTestBase : PageTest
{
    /// <summary>
    /// The base URL of the running server instance.
    /// </summary>
    protected string BaseUrl => E2EWebFixture.BaseUrl;

    [SetUp]
    public async Task Setup()
    {
        // Increase default timeout to 15s for Blazor WASM cold start
        Page.SetDefaultTimeout(15_000);
        await Task.CompletedTask;
    }
}
