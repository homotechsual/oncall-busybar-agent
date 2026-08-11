# MSIX Packaging and Intune-Managed Config Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `oncall-busybar-agent` packageable as MSIX for free-signed Microsoft Store distribution, without breaking the existing unpackaged self-contained exe path, and add an IT-manageable (Intune/GPO) configuration override layer so a Store install doesn't require every organization to hand-edit config files.

**Architecture:** A runtime "packaged vs. unpackaged" detection selects between two `IStartupShortcutManager` implementations (existing Startup-folder shortcut vs. a new WinRT `StartupTask`-backed one). Configuration becomes a four-layer `IConfiguration` build (bundled defaults → env vars → per-user LocalAppData file → HKLM registry policy), with a small custom `ConfigurationProvider` for the registry layer. The actual MSIX packaging project is added via Visual Studio's wizard (not automatable from the CLI) as the final task.

**Tech Stack:** .NET 10, WinForms, `Microsoft.Extensions.Hosting`/`Configuration`, WinRT (`Windows.ApplicationModel.StartupTask`), xunit.

## Global Constraints

- The existing unpackaged, self-contained single-file publish path (`dotnet publish ... -p:PublishSingleFile=true`, documented in README.md's "Run (production)" section) must keep working unmodified.
- No new NuGet package dependencies. The registry provider is hand-written against `Microsoft.Win32.Registry`, already available on `net*-windows` TFMs.
- `src/OncallAgent/OncallAgent.csproj`'s `TargetFramework` moves from `net10.0-windows` to `net10.0-windows10.0.19041.0` (required for the `Windows.ApplicationModel.StartupTask` WinRT projection). `test/OncallAgent.Tests/OncallAgent.Tests.csproj` stays on plain `net10.0-windows` — it never calls WinRT APIs directly.
- Registry policy root: `HKLM\SOFTWARE\Policies\OncallBusyBarAgent`, one subkey per `appsettings.json` section (`Graph`, `Mic`, `BusyBar`), one registry value per config key within it.
- Configuration precedence, lowest to highest: bundled `appsettings.json` → environment variables (existing default host behavior, unchanged) → `%LOCALAPPDATA%\oncall-busybar-agent\appsettings.json` → `HKLM\SOFTWARE\Policies\OncallBusyBarAgent`.
- MSIX packaging targets x64 only. ARM64 and CI/CD submission automation are out of scope for this plan.
- No changes to `OnCallStateEvaluator`, `GraphPresenceClient`, `MicrophoneActivityDetector`, or `BusyBarTimerController` — none of this touches on-call detection logic.
- Existing `appsettings.json`'s `Graph:ClientId` value is already populated and does not need to change — it's already the shared default app referenced by the README's setup instructions.

---

### Task 1: Packaged-app detection and a packaged startup-task manager

**Files:**
- Create: `src/OncallAgent/IStartupShortcutManager.cs`
- Modify: `src/OncallAgent/StartupShortcutManager.cs` (remove the interface, now in its own file)
- Create: `src/OncallAgent/Packaging/IPackagedAppContext.cs`
- Create: `src/OncallAgent/Packaging/PackagedAppContext.cs`
- Create: `src/OncallAgent/Packaging/PackagedStartupTaskManager.cs`
- Modify: `src/OncallAgent/OncallAgent.csproj` (TargetFramework bump)
- Modify: `src/OncallAgent/Program.cs` (DI wiring)

**Interfaces:**
- Produces: `OncallAgent.IStartupShortcutManager` (unchanged shape: `bool IsEnabled(); void Enable(); void Disable();`), now with two implementations: `OncallAgent.StartupShortcutManager` (unpackaged) and `OncallAgent.Packaging.PackagedStartupTaskManager` (packaged).
- Produces: `OncallAgent.Packaging.IPackagedAppContext` with `bool IsPackaged { get; }`, implemented by `OncallAgent.Packaging.PackagedAppContext`.
- Consumes: nothing from other tasks in this plan.

This task is not TDD'd — like the existing `StartupShortcutManager`, package-identity detection and the WinRT `StartupTask` API are only verifiable against real Windows package identity, not a fake. Verification here is "still builds, still passes the existing test suite" (no regression), matching how `StartupShortcutManager` itself is treated today (see this plan's spec, `docs/superpowers/specs/2026-08-11-msix-packaging-design.md`, Testing section).

- [ ] **Step 1: Extract the interface into its own file**

Create `src/OncallAgent/IStartupShortcutManager.cs`:

```csharp
namespace OncallAgent;

public interface IStartupShortcutManager
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
```

Then edit `src/OncallAgent/StartupShortcutManager.cs`, removing lines 6-11 (the `public interface IStartupShortcutManager { ... }` block) so the file starts:

```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace OncallAgent;

/// <summary>Toggles a Windows Startup-folder shortcut rather than a registry Run key: registry
```

The rest of the file (the `StartupShortcutManager` class and the COM interop declarations below it) is unchanged.

- [ ] **Step 2: Build to confirm the extraction didn't break anything**

Run: `dotnet build OncallAgent.sln`
Expected: Build succeeds, no errors about `IStartupShortcutManager` being defined twice or missing.

- [ ] **Step 3: Add `IPackagedAppContext`**

Create `src/OncallAgent/Packaging/IPackagedAppContext.cs`:

```csharp
namespace OncallAgent.Packaging;

public interface IPackagedAppContext
{
    bool IsPackaged { get; }
}
```

- [ ] **Step 4: Add `PackagedAppContext`**

Create `src/OncallAgent/Packaging/PackagedAppContext.cs`:

```csharp
using System.Runtime.InteropServices;

namespace OncallAgent.Packaging;

/// <summary>Detects whether this process has MSIX package identity (installed via the Microsoft
/// Store or sideloaded) versus running as a plain unpackaged exe. Uses the same plain Win32
/// P/Invoke style StartupShortcutManager already uses rather than a WinRT projection: reading
/// Windows.ApplicationModel.Package.Current would throw when unpackaged and needs a WinRT type
/// just to answer a yes/no question this kernel32 export answers directly.</summary>
public sealed class PackagedAppContext : IPackagedAppContext
{
    private const int ApplicationModelErrorNoPackage = 15700;

    public bool IsPackaged
    {
        get
        {
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);
            return result != ApplicationModelErrorNoPackage;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, char[]? packageFullName);
}
```

- [ ] **Step 5: Bump the TargetFramework**

Edit `src/OncallAgent/OncallAgent.csproj`, changing:

```xml
    <TargetFramework>net10.0-windows</TargetFramework>
```

to:

```xml
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
```

- [ ] **Step 6: Build to confirm the TFM bump alone doesn't break anything**

Run: `dotnet build OncallAgent.sln`
Expected: Build succeeds. If it fails asking for a Windows SDK version to be installed, install the "Windows 10 SDK (10.0.19041.0)" component (via Visual Studio Installer or `winget install Microsoft.WindowsSDK.10.0.19041`) and retry.

- [ ] **Step 7: Add `PackagedStartupTaskManager`**

Create `src/OncallAgent/Packaging/PackagedStartupTaskManager.cs`:

```csharp
using Windows.ApplicationModel;

namespace OncallAgent.Packaging;

/// <summary>Packaged-app equivalent of StartupShortcutManager: toggles the windows.startupTask
/// extension declared in Package.appxmanifest (TaskId below must match that declaration exactly)
/// instead of writing a Startup-folder shortcut, since an MSIX install folder is read-only and
/// gets replaced on every update — a hand-written .lnk file there wouldn't survive an update, and
/// Store certification expects the native extension for this anyway.</summary>
public sealed class PackagedStartupTaskManager : IStartupShortcutManager
{
    public const string TaskId = "OncallBusyBarAgentStartup";

    public bool IsEnabled()
    {
        var task = GetTask();
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }

    public void Enable()
    {
        GetTask().RequestEnableAsync().AsTask().GetAwaiter().GetResult();
    }

    public void Disable()
    {
        GetTask().Disable();
    }

    private static StartupTask GetTask() =>
        StartupTask.GetAsync(TaskId).AsTask().GetAwaiter().GetResult();
}
```

`TaskId` is `public const` (not private) because Task 5 (the packaging project's `Package.appxmanifest`) must declare the exact same string, and a reviewer of that task should be able to find it here rather than trust a copy-pasted literal.

- [ ] **Step 8: Wire both new types into `Program.cs`**

Edit `src/OncallAgent/Program.cs`. Add to the `using` block at the top:

```csharp
using OncallAgent.Packaging;
```

Then replace this existing line:

```csharp
builder.Services.AddSingleton<IStartupShortcutManager, StartupShortcutManager>();
```

with:

```csharp
builder.Services.AddSingleton<IPackagedAppContext, PackagedAppContext>();
builder.Services.AddSingleton<IStartupShortcutManager>(provider =>
    provider.GetRequiredService<IPackagedAppContext>().IsPackaged
        ? new PackagedStartupTaskManager()
        : new StartupShortcutManager());
```

(There is no existing line registering `PackagedAppContext` or `IPackagedAppContext` — this replacement adds both registrations in one place.)

- [ ] **Step 9: Build and run the full existing test suite**

Run: `dotnet build OncallAgent.sln && dotnet test OncallAgent.sln`
Expected: Build succeeds, all existing tests still pass (this task adds no new tests of its own — see rationale above).

- [ ] **Step 10: Commit**

```bash
git add src/OncallAgent/IStartupShortcutManager.cs src/OncallAgent/StartupShortcutManager.cs src/OncallAgent/Packaging/ src/OncallAgent/OncallAgent.csproj src/OncallAgent/Program.cs
git commit -m "Add packaged-app detection and a WinRT StartupTask-backed startup manager"
```

---

### Task 2: Registry-backed configuration provider for IT/Intune-managed overrides

**Files:**
- Create: `src/OncallAgent/Config/IPolicyRegistryReader.cs`
- Create: `src/OncallAgent/Config/Win32PolicyRegistryReader.cs`
- Create: `src/OncallAgent/Config/RegistryConfigurationProvider.cs`
- Create: `src/OncallAgent/Config/RegistryConfigurationSource.cs`
- Test: `test/OncallAgent.Tests/Config/FakePolicyRegistryReader.cs`
- Test: `test/OncallAgent.Tests/Config/RegistryConfigurationProviderTests.cs`

**Interfaces:**
- Produces: `OncallAgent.Config.IPolicyRegistryReader.ReadValues(): IReadOnlyList<PolicyRegistryValue>`, `PolicyRegistryValue(string SectionName, string ValueName, object Value)`, `OncallAgent.Config.RegistryConfigurationProvider(IPolicyRegistryReader reader)`, and the extension method `IConfigurationBuilder.AddOncallAgentPolicyOverrides()` — Task 3 calls this last when building the host's configuration.
- Consumes: nothing from other tasks in this plan.

- [ ] **Step 1: Add the reader interface and its data type**

Create `src/OncallAgent/Config/IPolicyRegistryReader.cs`:

```csharp
namespace OncallAgent.Config;

public interface IPolicyRegistryReader
{
    IReadOnlyList<PolicyRegistryValue> ReadValues();
}

public sealed record PolicyRegistryValue(string SectionName, string ValueName, object Value);
```

- [ ] **Step 2: Write the failing tests for `RegistryConfigurationProvider`**

Create `test/OncallAgent.Tests/Config/FakePolicyRegistryReader.cs`:

```csharp
using OncallAgent.Config;

namespace OncallAgent.Tests.Config;

internal sealed class FakePolicyRegistryReader : IPolicyRegistryReader
{
    public List<PolicyRegistryValue> Values { get; } = new();

    public IReadOnlyList<PolicyRegistryValue> ReadValues() => Values;
}
```

Create `test/OncallAgent.Tests/Config/RegistryConfigurationProviderTests.cs`:

```csharp
using OncallAgent.Config;
using Xunit;

namespace OncallAgent.Tests.Config;

public class RegistryConfigurationProviderTests
{
    [Fact]
    public void Load_PopulatesDataWithSectionColonKeyFormat()
    {
        var reader = new FakePolicyRegistryReader();
        reader.Values.Add(new PolicyRegistryValue("BusyBar", "Address", "10.0.5.99"));
        var provider = new RegistryConfigurationProvider(reader);

        provider.Load();

        Assert.True(provider.TryGet("BusyBar:Address", out var value));
        Assert.Equal("10.0.5.99", value);
    }

    [Fact]
    public void Load_ConvertsNonStringValuesToString()
    {
        var reader = new FakePolicyRegistryReader();
        reader.Values.Add(new PolicyRegistryValue("Graph", "PollIntervalSeconds", 45));
        var provider = new RegistryConfigurationProvider(reader);

        provider.Load();

        Assert.True(provider.TryGet("Graph:PollIntervalSeconds", out var value));
        Assert.Equal("45", value);
    }

    [Fact]
    public void Load_ProducesNoEntries_WhenReaderReturnsNoValues()
    {
        var provider = new RegistryConfigurationProvider(new FakePolicyRegistryReader());

        provider.Load();

        Assert.False(provider.TryGet("Graph:ClientId", out _));
    }

    [Fact]
    public void Load_KeysAreCaseInsensitive()
    {
        var reader = new FakePolicyRegistryReader();
        reader.Values.Add(new PolicyRegistryValue("Mic", "Enabled", "false"));
        var provider = new RegistryConfigurationProvider(reader);

        provider.Load();

        Assert.True(provider.TryGet("mic:enabled", out var value));
        Assert.Equal("false", value);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test test/OncallAgent.Tests/OncallAgent.Tests.csproj --filter RegistryConfigurationProviderTests`
Expected: FAIL to compile — `RegistryConfigurationProvider` does not exist yet.

- [ ] **Step 4: Implement `RegistryConfigurationProvider`**

Create `src/OncallAgent/Config/RegistryConfigurationProvider.cs`:

```csharp
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace OncallAgent.Config;

/// <summary>Turns the raw registry values IPolicyRegistryReader finds into the "Section:Key"
/// config keys Microsoft.Extensions.Configuration expects, so GraphOptions/MicOptions/
/// BusyBarOptions bind to them exactly the same as an appsettings.json section.</summary>
public sealed class RegistryConfigurationProvider(IPolicyRegistryReader reader) : ConfigurationProvider
{
    public override void Load()
    {
        Data = reader.ReadValues().ToDictionary(
            v => $"{v.SectionName}:{v.ValueName}",
            v => Convert.ToString(v.Value, CultureInfo.InvariantCulture),
            StringComparer.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test test/OncallAgent.Tests/OncallAgent.Tests.csproj --filter RegistryConfigurationProviderTests`
Expected: PASS, all four tests.

- [ ] **Step 6: Add the real registry reader**

Create `src/OncallAgent/Config/Win32PolicyRegistryReader.cs`:

```csharp
using Microsoft.Win32;

namespace OncallAgent.Config;

/// <summary>Reads IT-managed policy overrides from HKLM\SOFTWARE\Policies\OncallBusyBarAgent, one
/// subkey per appsettings.json section (Graph, Mic, BusyBar) and one registry value per config key
/// within it — the standard "machine policy" registry convention (matching how e.g. Chrome and
/// Edge expose IT-managed settings), so any MDM tool (Intune Settings Catalog, a plain deployment
/// script, Group Policy) can populate it without this app caring how the values got there.</summary>
public sealed class Win32PolicyRegistryReader : IPolicyRegistryReader
{
    private const string RootKeyPath = @"SOFTWARE\Policies\OncallBusyBarAgent";

    public IReadOnlyList<PolicyRegistryValue> ReadValues()
    {
        using var root = Registry.LocalMachine.OpenSubKey(RootKeyPath);
        if (root is null)
        {
            return [];
        }

        var values = new List<PolicyRegistryValue>();
        foreach (var sectionName in root.GetSubKeyNames())
        {
            using var sectionKey = root.OpenSubKey(sectionName);
            if (sectionKey is null)
            {
                continue;
            }

            foreach (var valueName in sectionKey.GetValueNames())
            {
                var value = sectionKey.GetValue(valueName);
                if (value is not null)
                {
                    values.Add(new PolicyRegistryValue(sectionName, valueName, value));
                }
            }
        }

        return values;
    }
}
```

This class is not unit tested — same category as `Win32MicrophoneRegistryReader`, real registry behavior verified against a real Windows machine at Task 5's manual verification step, not a fake.

- [ ] **Step 7: Add the `IConfigurationSource` and builder extension**

Create `src/OncallAgent/Config/RegistryConfigurationSource.cs`:

```csharp
using Microsoft.Extensions.Configuration;

namespace OncallAgent.Config;

public sealed class RegistryConfigurationSource : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new RegistryConfigurationProvider(new Win32PolicyRegistryReader());
}

public static class RegistryConfigurationExtensions
{
    /// <summary>Adds the HKLM\SOFTWARE\Policies\OncallBusyBarAgent registry policy layer as the
    /// highest-precedence configuration source — call this last, after every other
    /// AddJsonFile/AddEnvironmentVariables call.</summary>
    public static IConfigurationBuilder AddOncallAgentPolicyOverrides(this IConfigurationBuilder builder) =>
        builder.Add(new RegistryConfigurationSource());
}
```

- [ ] **Step 8: Build and run the full test suite**

Run: `dotnet build OncallAgent.sln && dotnet test OncallAgent.sln`
Expected: Build succeeds, all tests pass (existing tests plus this task's four new ones).

- [ ] **Step 9: Commit**

```bash
git add src/OncallAgent/Config/IPolicyRegistryReader.cs src/OncallAgent/Config/Win32PolicyRegistryReader.cs src/OncallAgent/Config/RegistryConfigurationProvider.cs src/OncallAgent/Config/RegistryConfigurationSource.cs test/OncallAgent.Tests/Config/
git commit -m "Add a registry-backed configuration provider for IT/Intune-managed overrides"
```

---

### Task 3: Per-user config bootstrap and full config-layer wiring

**Files:**
- Create: `src/OncallAgent/Config/UserConfigBootstrapper.cs`
- Test: `test/OncallAgent.Tests/Config/UserConfigBootstrapperTests.cs`
- Modify: `src/OncallAgent/Program.cs`

**Interfaces:**
- Consumes: `OncallAgent.Config.RegistryConfigurationExtensions.AddOncallAgentPolicyOverrides()` (Task 2).
- Produces: `OncallAgent.Config.UserConfigBootstrapper.EnsureUserConfigExists(string bundledConfigPath, string userConfigPath): void`.

- [ ] **Step 1: Write the failing tests for `UserConfigBootstrapper`**

Create `test/OncallAgent.Tests/Config/UserConfigBootstrapperTests.cs`:

```csharp
using OncallAgent.Config;
using Xunit;

namespace OncallAgent.Tests.Config;

public class UserConfigBootstrapperTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"oncall-agent-tests-{Guid.NewGuid()}");

    [Fact]
    public void EnsureUserConfigExists_CopiesBundledFile_WhenUserConfigMissing()
    {
        Directory.CreateDirectory(_tempDir);
        var bundledPath = Path.Combine(_tempDir, "bundled-appsettings.json");
        var userPath = Path.Combine(_tempDir, "userdir", "appsettings.json");
        File.WriteAllText(bundledPath, "{\"Graph\":{}}");

        UserConfigBootstrapper.EnsureUserConfigExists(bundledPath, userPath);

        Assert.True(File.Exists(userPath));
        Assert.Equal("{\"Graph\":{}}", File.ReadAllText(userPath));
    }

    [Fact]
    public void EnsureUserConfigExists_DoesNotOverwrite_WhenUserConfigAlreadyExists()
    {
        Directory.CreateDirectory(_tempDir);
        var bundledPath = Path.Combine(_tempDir, "bundled-appsettings.json");
        var userPath = Path.Combine(_tempDir, "userdir", "appsettings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(userPath)!);
        File.WriteAllText(bundledPath, "{\"Graph\":{}}");
        File.WriteAllText(userPath, "{\"BusyBar\":{\"Address\":\"192.168.1.1\"}}");

        UserConfigBootstrapper.EnsureUserConfigExists(bundledPath, userPath);

        Assert.Equal("{\"BusyBar\":{\"Address\":\"192.168.1.1\"}}", File.ReadAllText(userPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test test/OncallAgent.Tests/OncallAgent.Tests.csproj --filter UserConfigBootstrapperTests`
Expected: FAIL to compile — `UserConfigBootstrapper` does not exist yet.

- [ ] **Step 3: Implement `UserConfigBootstrapper`**

Create `src/OncallAgent/Config/UserConfigBootstrapper.cs`:

```csharp
namespace OncallAgent.Config;

/// <summary>Ensures a per-user, writable copy of appsettings.json exists before configuration is
/// built. The bundled appsettings.json next to the exe becomes read-only once packaged as MSIX
/// (and is overwritten on every republish even when unpackaged), so user/IT edits — BusyBar:Address
/// above all — need a location outside both of those constraints.</summary>
public static class UserConfigBootstrapper
{
    public static void EnsureUserConfigExists(string bundledConfigPath, string userConfigPath)
    {
        if (File.Exists(userConfigPath))
        {
            return;
        }

        var userConfigDirectory = Path.GetDirectoryName(userConfigPath);
        if (!string.IsNullOrEmpty(userConfigDirectory))
        {
            Directory.CreateDirectory(userConfigDirectory);
        }

        File.Copy(bundledConfigPath, userConfigPath);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test test/OncallAgent.Tests/OncallAgent.Tests.csproj --filter UserConfigBootstrapperTests`
Expected: PASS, both tests.

- [ ] **Step 5: Wire the bootstrap and both new configuration layers into `Program.cs`**

Edit `src/OncallAgent/Program.cs`. Add to the `using` block at the top:

```csharp
using OncallAgent.Config;
```

Then, immediately after the existing `var builder = Host.CreateApplicationBuilder(...)` block and before the `builder.Services.AddOptions<GraphOptions>()` line, insert:

```csharp
// The bundled appsettings.json (loaded above via ContentRootPath) ships factory defaults and is
// read-only once packaged as MSIX; user/IT overrides layer on top of it here, highest precedence
// last: a per-user writable copy, then an IT-managed registry policy (see RegistryConfigurationProvider).
var userConfigPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "oncall-busybar-agent",
    "appsettings.json");
UserConfigBootstrapper.EnsureUserConfigExists(
    Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
    userConfigPath);
builder.Configuration
    .AddJsonFile(userConfigPath, optional: true, reloadOnChange: false)
    .AddOncallAgentPolicyOverrides();
```

- [ ] **Step 6: Build and run the full test suite**

Run: `dotnet build OncallAgent.sln && dotnet test OncallAgent.sln`
Expected: Build succeeds, all tests pass (existing tests plus Task 2's and this task's new ones).

- [ ] **Step 7: Manual smoke test**

Run: `dotnet run --project src/OncallAgent/OncallAgent.csproj`
Expected: App starts as before (tray icon appears, no crash). Check that `%LOCALAPPDATA%\oncall-busybar-agent\appsettings.json` now exists and matches the bundled `src/OncallAgent/appsettings.json` contents — confirming the first-run bootstrap ran. Stop the app (tray menu → Exit).

- [ ] **Step 8: Commit**

```bash
git add src/OncallAgent/Config/UserConfigBootstrapper.cs test/OncallAgent.Tests/Config/UserConfigBootstrapperTests.cs src/OncallAgent/Program.cs
git commit -m "Bootstrap a per-user config file and wire the full config-layer precedence"
```

---

### Task 4: Documentation

**Files:**
- Modify: `README.md`

**Interfaces:** None — this task changes no code.

- [ ] **Step 1: Replace the "Configure" section**

In `README.md`, replace the existing `## Configure` section (the paragraph starting "Edit `src/OncallAgent/appsettings.json`..." through the end of its settings table) with:

```markdown
## Configure

Settings load in four layers, each overriding the one before it:

1. The bundled `src/OncallAgent/appsettings.json` — factory defaults, including the shared
   `Graph:ClientId` described above.
2. Environment variables (double underscore nesting, for example `Graph__ClientId`).
3. A per-user, writable copy at `%LOCALAPPDATA%\oncall-busybar-agent\appsettings.json`, created
   automatically from the bundled defaults the first time the app runs. Edit this file — not the
   one under `src/OncallAgent` — to change settings after that first run; it survives updates,
   which the bundled file's location does not once the app is installed as an MSIX package.
4. An IT-managed registry policy, for organizations deploying this via Intune or another MDM tool
   — see "Managing settings via Intune or Group Policy" below. This always wins over the file
   above.

| Key | Default | Description |
| --- | --- | --- |
| `Graph:ClientId` | (shared default) | The Entra app's (public) client ID. |
| `Graph:TenantId` | `organizations` | Which tenants can sign in; matches the app registration's account type. |
| `Graph:PollIntervalSeconds` | `30` | How often to poll presence and, when on a call, the calendar. |
| `Mic:Enabled` | `true` | Whether the local microphone fallback runs at all. |
| `Mic:PollIntervalSeconds` | `5` | How often to check local microphone activity. |
| `BusyBar:Address` | `10.0.4.20` | Network address of the BUSY Bar device. |

## Managing settings via Intune or Group Policy

Any of the settings above can be locked down centrally by setting registry values under
`HKLM\SOFTWARE\Policies\OncallBusyBarAgent`, one subkey per section:

```
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\Graph      (values: ClientId, TenantId, PollIntervalSeconds)
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\Mic         (values: Enabled, PollIntervalSeconds)
HKLM\SOFTWARE\Policies\OncallBusyBarAgent\BusyBar     (values: Address)
```

Set only the values you want to lock down — anything unset falls through to the user's own
`appsettings.json`. `Enabled` should be set as a String value (`true` or `false`); the interval and
address values can be a String or a DWORD, whichever your deployment tool makes easier. How the
registry values get set is up to you: an Intune Settings Catalog custom OMA-URI profile, a Win32
app deployment script, or plain Group Policy all work identically from this app's point of view —
it only reads the registry, it doesn't care how the values arrived.
```

- [ ] **Step 2: Update the "Code signing" paragraph**

In `README.md`, find the existing paragraph starting "**Code signing:** the published exe is
unsigned...". Immediately after that paragraph, add:

```markdown
If this app is installed from the Microsoft Store as an MSIX package instead, this doesn't apply —
the Store signs the package automatically as part of certification, at no cost.
```

- [ ] **Step 3: Review the diff, then commit**

Run: `git diff README.md`
Expected: Only the "Configure" section's replacement, the new "Managing settings via Intune or Group Policy" section, and the added Store-signing note appear in the diff — no unrelated changes.

```bash
git add README.md
git commit -m "Document the layered config model and Intune/GPO-managed policy overrides"
```

---

### Task 5: Add the Windows Application Packaging Project (manual, Visual Studio required)

**This task cannot be driven from a CLI/bash automation loop.** The Windows Application Packaging
Project (`.wapproj`) is a Visual Studio project type backed by MSBuild targets that ship with
Visual Studio's "Universal Windows Platform development" workload (or the "MSIX Packaging Tools"
optional component under .NET desktop development) — there is no `dotnet new`/`dotnet build`
equivalent, and hand-authoring the `.wapproj` XML from scratch is fragile enough (version-sensitive
imports tied to whichever workload is installed) that it should be generated by Visual Studio's own
wizard rather than reproduced here. If executing this plan via subagent-driven-development or
executing-plans, stop before this task and hand it to whoever has Visual Studio installed locally;
everything in Tasks 1-4 is independent of it.

**Files:**
- Create: `OncallAgent.Package/OncallAgent.Package.wapproj` (generated by Visual Studio)
- Create: `OncallAgent.Package/Package.appxmanifest` (generated by Visual Studio, then hand-edited per Step 5)
- Modify: `OncallAgent.sln` (Visual Studio adds this automatically)

**Interfaces:**
- Consumes: `OncallAgent.Packaging.PackagedStartupTaskManager.TaskId` (Task 1) — the manifest's `TaskId` attribute (Step 5 below) must be the literal string `OncallBusyBarAgentStartup`, matching that constant exactly.

- [ ] **Step 1: Add the packaging project**

In Visual Studio 2022 (with the "Universal Windows Platform development" workload, or the "MSIX
Packaging Tools" optional component under ".NET desktop development", installed), open
`OncallAgent.sln`. Right-click the solution → **Add** → **New Project** → search "Windows
Application Packaging Project" → name it `OncallAgent.Package`, location at the repo root
(sibling to `src`).

- [ ] **Step 2: Set target/minimum version**

In the version dialog, set **Target Version** to the latest installed Windows SDK, and **Minimum
Version** to at least **Windows 10, version 1809 (10.0; Build 17763)** or later (comfortably covers
the `StartupTaskState.EnabledByPolicy` value used in Task 1, added in SDK 17134).

- [ ] **Step 3: Reference the app project as the entry point**

In Solution Explorer, under `OncallAgent.Package` → **Dependencies**, right-click → **Add Project
Reference** → check `OncallAgent` → OK. If more than one application ends up listed under the
packaging project's **Applications** node, right-click `OncallAgent` → **Set as Entry Point** (with
only one referenced project this is usually already the case).

- [ ] **Step 4: Fix x64 configuration**

Open **Build** → **Configuration Manager**. Confirm both `OncallAgent.Package` and `OncallAgent`
have an `x64` platform entry (add one via the **Platform** dropdown → **New...** → `x64`, copying
settings from `Any CPU`, if it's missing) and that `OncallAgent.Package`'s Release|x64 configuration
builds `OncallAgent`'s Release|x64 (not Release|Any CPU).

- [ ] **Step 5: Add the startup task extension to the manifest**

Right-click `OncallAgent.Package\Package.appxmanifest` → **View Code** (not the designer). Confirm
the root `<Package>` element declares the `desktop` namespace and lists it as ignorable — Visual
Studio's default template usually already includes both:

```xml
<Package
  xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
  IgnorableNamespaces="uap rescap desktop">
```

(Add `desktop` to the existing `IgnorableNamespaces` list if it's not already there, rather than
overwriting whatever else is listed.)

Then, inside the `<Application>` element (under `<Applications>`), add an `<Extensions>` element
containing the startup task declaration, if `<Extensions>` doesn't already exist:

```xml
<Applications>
  <Application Id="App" Executable="$targetnametoken$.exe" EntryPoint="$targetentrypoint$">
    <!-- ... existing uap:VisualElements etc. unchanged ... -->
    <Extensions>
      <desktop:Extension Category="windows.startupTask" Executable="OncallAgent.exe" EntryPoint="Windows.FullTrustApplication">
        <desktop:StartupTask TaskId="OncallBusyBarAgentStartup" Enabled="true" DisplayName="BUSY Bar on call agent" />
      </desktop:Extension>
    </Extensions>
  </Application>
</Applications>
```

`TaskId="OncallBusyBarAgentStartup"` must match `PackagedStartupTaskManager.TaskId` from Task 1
exactly (case-sensitive) — the WinRT `StartupTask.GetAsync` call in that class looks up the task by
this string.

- [ ] **Step 6: Build the packaging project**

Set `OncallAgent.Package` as the startup project (right-click → **Set as StartUp Project**), select
the `Release`/`x64` configuration, then **Build** → **Build OncallAgent.Package**. Confirm no
errors. If it fails complaining about mismatched platforms, revisit Step 4.

- [ ] **Step 7: Produce and install a local test package**

Right-click `OncallAgent.Package` → **Publish** → **Create App Packages...** → choose
**Sideloading** (not Store, for this first local test) → follow the wizard (a self-signed test
certificate is fine here) → **Create**. Enable **Developer Mode** in Windows Settings if not
already on, then install the produced `.msix` from the output folder (double-click it, or
`Add-AppxPackage -Path <path>.msix` in PowerShell).

- [ ] **Step 8: Verify packaged behavior end-to-end**

With the sideloaded package installed and running:
- Confirm the tray icon appears and behaves as it does unpackaged.
- Right-click the tray icon → check **Start with Windows** → open Task Manager → **Startup apps**
  tab → confirm "BUSY Bar on call agent" appears there (this confirms `PackagedStartupTaskManager`
  and the manifest extension from Step 5 are wired correctly — the packaged app is not writing a
  `.lnk` file the way the unpackaged build does).
- Confirm `%LOCALAPPDATA%\oncall-busybar-agent\appsettings.json` exists and the app reads it
  (change `BusyBar:Address` there, restart the app, confirm the new address takes effect) —
  confirming Task 3's config bootstrap works under real package identity, not just under
  `dotnet run`.

- [ ] **Step 9: Commit**

```bash
git add OncallAgent.sln OncallAgent.Package/
git commit -m "Add the Windows Application Packaging Project for MSIX/Store distribution"
```

Reserving the app name in Partner Center and producing a Store-bound `.msixupload` (via the same
**Create App Packages** wizard, choosing "Microsoft Store" instead of "Sideloading" once a name is
reserved) is a separate, later step outside this plan — it depends on the Partner Center account
and app name reservation, both manual prerequisites noted as out of scope in
`docs/superpowers/specs/2026-08-11-msix-packaging-design.md`.
