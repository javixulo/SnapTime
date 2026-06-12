// E2E test fixture: starts the Server API + Client WASM dev server
// on random ports with a temporary SQLite database, and tears both
// down after all tests. Tests navigate to the Client URL.
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SnapTime.E2ETests;

[SetUpFixture]
public class E2EWebFixture
{
    private static Process? _serverProcess;
    private static Process? _clientProcess;
    private static string? _tempDbPath;
    private static string? _originalClientSettings;
    private static string? _testProjectDir;
    private static string? _clientProjectPath;

    /// <summary>
    /// The dynamically-assigned base URL of the running Client dev server.
    /// Tests navigate here.
    /// </summary>
    public static string BaseUrl { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        _testProjectDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", "..", "tests", "SnapTime.E2ETests"));
        var solutionRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "..", ".."));

        var serverProjectPath = Path.Combine(solutionRoot, "src", "SnapTime.Server");
        _clientProjectPath = Path.Combine(solutionRoot, "src", "SnapTime.Client");

        if (!Directory.Exists(serverProjectPath))
            throw new DirectoryNotFoundException($"Server project not found at {serverProjectPath}");
        if (!Directory.Exists(_clientProjectPath))
            throw new DirectoryNotFoundException($"Client project not found at {_clientProjectPath}");

        // 1. Pick random ports
        var serverPort = GetRandomUnusedPort();
        var clientPort = GetRandomUnusedPort();
        var serverUrl = $"http://127.0.0.1:{serverPort}";
        var clientUrl = $"http://127.0.0.1:{clientPort}";
        BaseUrl = clientUrl;

        // 2. Create a temp server config from the test template
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"snaptime-e2e-{Guid.NewGuid()}.db");

        var e2eConfigPath = Path.Combine(_testProjectDir, "e2e.snaptime.config.json");
        if (!File.Exists(e2eConfigPath))
            throw new FileNotFoundException($"Test config template not found: {e2eConfigPath}");

        var configJson = JsonNode.Parse(File.ReadAllText(e2eConfigPath))!;
        configJson["database"]!["path"] = _tempDbPath;

        var tempConfigPath = Path.Combine(Path.GetTempPath(), $"snaptime-e2e-config-{Guid.NewGuid()}.json");
        var writeOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(tempConfigPath, configJson.ToJsonString(writeOptions));

        // 3. Write client settings to point to the server (Blazor WASM reads from wwwroot)
        var clientSettingsPath = Path.Combine(_clientProjectPath, "wwwroot", "appsettings.Development.json");
        _originalClientSettings = File.Exists(clientSettingsPath) ? File.ReadAllText(clientSettingsPath) : null;

        var e2eClientPath = Path.Combine(_testProjectDir, "e2e.appsettings.json");
        if (!File.Exists(e2eClientPath))
            throw new FileNotFoundException($"Test client settings template not found: {e2eClientPath}");

        var clientSettingsJson = File.ReadAllText(e2eClientPath).Replace("PLACEHOLDER", serverUrl);
        File.WriteAllText(clientSettingsPath, clientSettingsJson);

        // 4. Start the Server API process with env var pointing to temp config
        var serverPsi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{serverProjectPath}\" -- --urls \"{serverUrl}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        serverPsi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
        serverPsi.EnvironmentVariables["SNAPTIME_CONFIG_PATH"] = tempConfigPath;

        _serverProcess = Process.Start(serverPsi);
        if (_serverProcess is null)
            throw new InvalidOperationException("Failed to start the server process.");

        TestContext.WriteLine($"Server process started with PID: {_serverProcess.Id} on {serverUrl}");

        // 5. Start the Client dev server
        var clientPsi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{_clientProjectPath}\" -- --urls \"{clientUrl}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        clientPsi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
        clientPsi.EnvironmentVariables["ApiBaseUrl"] = serverUrl;

        _clientProcess = Process.Start(clientPsi);
        if (_clientProcess is null)
            throw new InvalidOperationException("Failed to start the client dev server.");

        TestContext.WriteLine($"Client process started with PID: {_clientProcess.Id} on {clientUrl}");

        // 6. Wait for both to be ready
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow.AddSeconds(60);

        // Wait for Server (health endpoint)
        var serverReady = false;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await httpClient.GetAsync($"{serverUrl}/api/health");
                if (response.IsSuccessStatusCode) { serverReady = true; break; }
            }
            catch { }
            await Task.Delay(500);
        }

        if (!serverReady)
        {
            KillProcess(_serverProcess);
            KillProcess(_clientProcess);
            var msg = $"Server did not start within 60s at {serverUrl}.";
            if (_serverProcess!.HasExited)
                msg += $" Process exited with code {_serverProcess.ExitCode}.";
            throw new TimeoutException(msg);
        }

        // Wait for Client (any successful HTTP response)
        var clientReady = false;
        deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var response = await httpClient.GetAsync(clientUrl);
                if (response.IsSuccessStatusCode) { clientReady = true; break; }
            }
            catch { }
            await Task.Delay(500);
        }

        if (!clientReady)
        {
            KillProcess(_serverProcess);
            KillProcess(_clientProcess);
            throw new TimeoutException($"Client dev server did not start within 60s at {clientUrl}.");
        }

        TestContext.WriteLine($"E2E infrastructure ready. Server: {serverUrl}, Client: {BaseUrl}, DB: {_tempDbPath}");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        KillProcess(_serverProcess, "Server");
        KillProcess(_clientProcess, "Client");

        // Restore client settings
        if (_clientProjectPath is not null)
        {
            var settingsPath = Path.Combine(_clientProjectPath, "wwwroot", "appsettings.Development.json");
            try
            {
                if (_originalClientSettings is not null)
                    File.WriteAllText(settingsPath, _originalClientSettings);
                else if (File.Exists(settingsPath))
                    File.Delete(settingsPath);
            }
            catch (Exception ex)
            {
                TestContext.WriteLine($"Warning: could not restore client settings: {ex.Message}");
            }
        }

        // Delete temp database
        if (_tempDbPath is not null)
        {
            try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); }
            catch (Exception ex) { TestContext.WriteLine($"Warning: could not delete temp DB: {ex.Message}"); }
        }
    }

    private static void KillProcess(Process? process, string label = "process")
    {
        if (process is null || process.HasExited) return;
        try
        {
            process.Kill(entireProcessTree: true);
            if (!process.WaitForExit(15000))
            {
                TestContext.WriteLine($"Warning: {label} (PID {process.Id}) did not exit after 15s, retrying...");
                Task.Delay(2000).GetAwaiter().GetResult();
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(10000);
                }
            }
        }
        catch (Exception ex)
        {
            TestContext.WriteLine($"Warning: could not kill {label} (PID {process.Id}): {ex.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private static int GetRandomUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
