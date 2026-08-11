# MSIX Packaging and Intune-Managed Config Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `oncall-busybar-agent` packageable as MSIX for free-signed Microsoft Store distribution, without breaking the existing unpackaged self-contained exe path, and add an IT-manageable (Intune/GPO) configuration override layer so a Store install doesn't require every organization to hand-edit config files.

**Architecture:** A runtime "packaged vs. unpackaged" detection selects between two `IStartupShortcutManager` implementations (existing Startup-folder shortcut vs. a new WinRT `StartupTask`-backed one). Configuration becomes a four-layer `IConfiguration` build (bundled defaults → env vars → per-user LocalAppData file → HKLM registry policy), with a small custom `ConfigurationProvider` for the registry layer. The actual MSIX packaging project is added via Visual Studio's wizard (not automatable from the CLI) as the final task.

**Tech Stack:** .NET 10, WinForms, `Microsoft.Extensions.Hosting`/`Configuration`, WinRT (`Windows.ApplicationModel.StartupTask`), xunit.

## Global Constraints

- The existing unpackaged, self-contained single-file publish path (`dotnet publish ... -p:PublishSingleFile=true`, documented in README.md's "Run (production)" section) must keep working unmodified.
- No new NuGet package dependencies. The registry provider is hand-written against `Microsoft.Win32.Registry`, already available on `net*-windows` TFMs.
- `src/OncallAgent/OncallAgent.csproj`'s `TargetFramework` moves from `net10.0-windows` to `net10.0-windows10.0.19041.0` (required for the `Windows.ApplicationModel.StartupTask` WinRT projection). **Amended during final review:** `test/OncallAgent.Tests/OncallAgent.Tests.csproj` must track the same TargetFramework, not stay on plain `net10.0-windows` as originally stated here — a `ProjectReference` from a lower target-platform-version project to a higher one fails NuGet asset selection (`NU1201`), so this constraint was simply wrong. The human approved the bump after Task 1 surfaced it (see ledger).
- Registry policy root: `HKLM\SOFTWARE\Policies\OncallBusyBarAgent`, one subkey per `appsettings.json` section (`Graph`, `Mic`, `BusyBar`), one registry value per config key within it.
- Configuration precedence, lowest to highest: bundled `appsettings.json` → `%LOCALAPPDATA%\oncall-busybar-agent\appsettings.json` → environment variables → command line → `HKLM\SOFTWARE\Policies\OncallBusyBarAgent`. **Amended during final review:** the LocalAppData file was originally specified to outrank environment variables, but since the bootstrap copies every key from the bundled defaults, that left no key for an env var to actually override — silently breaking the pre-existing, documented `Graph__ClientId`-style override path. The LocalAppData file now sits just above the bundled defaults (env vars/command line/registry policy still override it), matching the precedence convention `Host.CreateApplicationBuilder`'s own default chain already uses (file config < env vars < command line).
- **Amended per human request after final review:** MSIX packaging targets x86, x64, **and** ARM64 (not x64 only as originally scoped) — see Task 5 Steps 2, 4, and 7 below, all updated for three architectures. CI/CD submission automation remains out of scope for this plan.
- No changes to `OnCallStateEvaluator`, `GraphPresenceClient`, `MicrophoneActivityDetector`, or `BusyBarTimerController` — none of this touches on-call detection logic.
- Existing `appsettings.json`'s `Graph:ClientId` value is already populated and does not need to change — it's already the shared default app referenced by the README's setup instructions.
- **Added during final review:** once actually packaged (Task 5), Windows redirects new writes under `%LOCALAPPDATA%` to a private per-package folder unless the manifest opts out — see Task 5 Step 5a below. Without that, `UserConfigBootstrapper`'s writes would land somewhere other than the path README.md tells users to edit.
- **Added while actually running Task 5's packaging step:** `src/OncallAgent/OncallAgent.csproj` needs `<RuntimeIdentifiers>win-x86;win-x64;win-arm64</RuntimeIdentifiers>` in its `PropertyGroup`. This is Microsoft's own documented requirement for packaging a .NET project via the classic two-project Windows Application Packaging Project (see "What's new in .NET Core 3.0" — Windows Desktop & COM section): without it, plain `dotnet build`/`msbuild` of `OncallAgent` itself succeeds (it just isn't RID-specific), but the packaging project has no architecture-specific publish output to stage into the MSIX payload, and package creation fails with a manifest validation error claiming the entry-point exe "doesn't exist in the package" — regardless of what the manifest's `Executable` attribute says, since the real problem is upstream of the manifest entirely. Confirmed this addition doesn't change `dotnet build`/`dotnet test` behavior for the unpackaged path (still 39/39 tests passing).

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

- [ ] **Step 3: Reference the app project as the entry point (must be done via the Visual Studio UI, not a file edit)**

`OncallAgent.Package.wapproj` didn't end up with a project reference after Step 1's wizard. A first
attempt at fixing this by hand-adding a plain `<ProjectReference Include="..\src\OncallAgent\
OncallAgent.csproj" />` ItemGroup directly to the file did **not** work — Visual Studio still showed
"Project must have a reference to an application. Please add a reference under the 'Applications'
node in the Solution Explorer," even with the reference correctly positioned before the
`Microsoft.DesktopBridge.targets` import. Whatever VS's own **Add Project Reference** dialog does to
populate the packaging project's **Applications** node evidently isn't fully captured by a plain
`<ProjectReference>` element — this plan doesn't know the exact extra metadata involved, so use the
dialog itself rather than another hand-edit attempt: in Solution Explorer, right-click
`OncallAgent.Package` → **Dependencies** → **Add Project Reference...** → check `OncallAgent` → OK.
Then expand **Dependencies** → **Applications** and confirm `OncallAgent` is listed there (bold =
entry point); if more than one project ends up listed, right-click `OncallAgent` → **Set as Entry
Point**.

- [ ] **Step 4: Wire up x86, x64, and ARM64 in the solution (amended per human request after final review — rewritten against the actual file state, not assumed)**

Reading the real files after Step 1 revealed the gap: `OncallAgent.sln` already has `x86` and `x64`
solution platforms (from before this plan existed), but every single one of `OncallAgent`'s
`ProjectConfigurationPlatforms` entries — for **all** of Any CPU, x64, *and* x86 — maps `ActiveCfg`
and `Build.0` to `Debug|Any CPU` / `Release|Any CPU`. In other words, x64 and x86 already "exist" as
selectable options but have never actually built anything except Any CPU underneath, for any
platform, ever. `ARM64` doesn't exist in the solution at all yet. And `OncallAgent.Package` itself
isn't listed as a project in `OncallAgent.sln` on disk yet either (Visual Studio's in-memory
solution likely already shows it from Step 1's wizard — this needs **File → Save All** to persist,
if it hasn't already).

MSIX packages are tied to one concrete architecture each — a plain "Any CPU" managed build can't be
what actually ships inside an x86/x64/ARM64-specific package — so this has to be fixed for real, not
just have the option exist. Do this in **Build → Configuration Manager**:

1. **Active solution platform** dropdown → **New...** → select `ARM64`, "Copy settings from:" `x64`
   → OK. This adds the missing third platform at the solution level.
2. In the grid, find the `OncallAgent` row. For **each** of the `x64`, `x86`, and `ARM64` solution
   platform columns (leave `Any CPU` alone — that one still exists for plain `dotnet run`/`dotnet
   build` iteration and doesn't need to change): open that row's **Platform** dropdown. If it
   already lists `x64`/`x86`/`ARM64` as options, select the matching one (not `Any CPU`). If it
   doesn't, choose **New...** and create it. Repeat for all three — this is the exact fix for the
   "quietly still building Any CPU" gap found above, and it has to be done per architecture, not
   once.
3. Do the same for the `OncallAgent.Package` row: its `x64`, `x86`, and `ARM64` columns should each
   point at the matching real platform (this is usually already correct for a fresh packaging
   project, since `.wapproj` files declare all of x86/x64/ARM/ARM64/AnyCPU as real
   `ProjectConfiguration`s by default — confirm rather than assume, the same way as for `OncallAgent`
   above).
4. Confirm the **Build** and **Deploy** checkboxes are ticked for `OncallAgent.Package` under the
   Release configuration for all three platforms — an unticked box there is a common reason "Build
   Solution" silently skips packaging one architecture with no error.
5. **File → Save All**, then re-open `OncallAgent.sln` in a text editor (or re-run `git diff
   OncallAgent.sln` from a terminal) to sanity-check that `OncallAgent`'s `ActiveCfg`/`Build.0` lines
   for x64/x86/ARM64 now read e.g. `Release|x64 = Release|x64` rather than `Release|x64 =
   Release|Any CPU` — this is the one-line signature of the bug this step exists to fix, so it's a
   fast way to confirm it actually took.

- [ ] **Step 5: Add the startup task extension to the manifest (already done directly in the file)**

`Package.appxmanifest` has had the `desktop`/`desktop6` namespace declarations, the
`windows.startupTask` extension (`TaskId="OncallBusyBarAgentStartup"`, matching
`PackagedStartupTaskManager.TaskId` from Task 1 exactly, case-sensitive — the WinRT
`StartupTask.GetAsync` call in that class looks up the task by this string), and the
`unvirtualizedResources` capability + `FileSystemWriteVirtualization` property from Step 5a below
all added directly, since appxmanifest XML is a standard, well-documented schema safe to hand-edit
(unlike the `.sln` platform matrix in Step 4). In Visual Studio, right-click
`OncallAgent.Package\Package.appxmanifest` → **View Code** to confirm it now has all of: the four
namespace declarations (`uap`, `rescap`, `desktop`, `desktop6`) all listed in `IgnorableNamespaces`;
an `<Extensions>` element under `<Application>` with the `windows.startupTask` declaration; and
`unvirtualizedResources` under `<Capabilities>`.

**Corrected twice during actual package creation — this is the final, verified state.** The
`windows.startupTask` extension's `Executable` attribute must be the literal real filename,
`OncallAgent.exe` — **not** the `$targetnametoken$.exe` token. That token only gets substituted by
Visual Studio's manifest-generation step for the auto-generated `<Application>` element itself; a
custom `<Extension>` added by hand never gets that substitution, so using the token there just
produces a manifest validation error citing the literal unresolved string `"$targetnametoken$.exe"`
as a missing file (confirmed twice, byte-for-byte identical error both times).

The real blocker turned out to be upstream of the manifest entirely: **before** the
`<RuntimeIdentifiers>` fix (see the Global Constraints entry above) was applied to
`OncallAgent.csproj`, *neither* the literal name *nor* the token worked, because no
architecture-specific publish output existed for the packaging step to stage at all — "doesn't
exist in the package" was literally true regardless of what string the manifest used. Once
`RuntimeIdentifiers` was added and a real `win-x86`/`win-x64`/`win-arm64`-specific publish output
started appearing (confirmed via the build log showing `...\bin\x86\Release\
net10.0-windows10.0.19041.0\win-x86\msixpublish\`), the literal `OncallAgent.exe` name finally had a
real file to match. **Sequencing matters if redoing this from scratch:** add `RuntimeIdentifiers`
first, then use the literal exe name in the Extension — doing the manifest edit alone, in either
form, cannot succeed without the RID fix in place first.

- [ ] **Step 5a: Disable file-system write virtualization (added during final review; already done directly in the file, see Step 5)**

By default, a packaged desktop app's writes to `%LOCALAPPDATA%` are redirected by Windows to a
private per-package folder rather than the real path — which would silently break
`UserConfigBootstrapper`'s LocalAppData file (Task 3) and every path in README.md's "Configure"
section that tells users to edit `%LOCALAPPDATA%\oncall-busybar-agent\appsettings.json`. The
`<desktop6:FileSystemWriteVirtualization>disabled</desktop6:FileSystemWriteVirtualization>` property
and its matching `rescap:Capability Name="unvirtualizedResources"` (both applied in Step 5 above)
opt out of that redirection so the packaged app uses the same real path the unpackaged build already
does. Step 8 below (confirming the LocalAppData file's real path) depends on this being in place; if
Store certification flags `unvirtualizedResources`, that's a signal to fall back to branching on
`OncallAgent.Packaging.IPackagedAppContext.IsPackaged` and using WinRT `ApplicationData.Current.
LocalFolder` instead — not attempted here since the manifest approach is simpler and sufficient for
a full-trust Desktop Bridge app.

- [ ] **Step 6: Build the packaging project for each architecture**

Set `OncallAgent.Package` as the startup project (right-click → **Set as StartUp Project**), then
for each of `x86`, `x64`, and `ARM64` in turn: select the `Release`/(architecture) configuration,
**Build** → **Build OncallAgent.Package**, confirm no errors. If any one fails complaining about
mismatched platforms, revisit Step 4 for that specific architecture.

- [ ] **Step 7: Produce and install a local test package (amended per human request after final review)**

Right-click `OncallAgent.Package` → **Publish** → **Create App Packages...** → choose
**Sideloading** (not Store, for this first local test) → in the **Select and Configure Packages**
step, check all three architecture boxes (**x86**, **x64**, **ARM64**) rather than just one, and set
**Generate app bundle** to **Always** — this produces a single `.msixbundle` containing all three
architecture-specific packages, and Windows installs whichever one matches the target device →
follow the rest of the wizard (a self-signed test certificate is fine here) → **Create**. Enable
**Developer Mode** in Windows Settings if not already on, then install the produced `.msixbundle`
(double-click it, or `Add-AppxPackage -Path <path>.msixbundle` in PowerShell) — on whichever
architecture machine you're testing from; installing a bundle automatically picks the matching
package inside it.

The `.wapproj` already has `<AppxBundle>Always</AppxBundle>` and
`<AppxBundlePlatforms>x86|x64|ARM64</AppxBundlePlatforms>` set, so once Step 4's platform wiring is
correct, a bundle can also be produced from the command line instead of the wizard (useful for
scripting a rebuild without reopening the wizard each time — this is still local/manual, not the
CI/CD automation the Global Constraints put out of scope):

```powershell
msbuild OncallAgent.Package\OncallAgent.Package.wapproj /t:Publish /p:Configuration=Release /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxBundlePlatforms="x86|x64|ARM64" /p:AppxBundle=Always /p:AppxPackageDir=AppPackages\
```

(Requires `msbuild` on PATH — the Developer PowerShell/Command Prompt for VS shortcut sets this up.)

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
- **If an ARM64 device or the ARM64 Windows emulator is available, test interactive sign-in there
  specifically** (added per human request after final review): this app's MSAL WAM broker
  (`Microsoft.Identity.Client.Broker`) has a native component whose current ARM64 support wasn't
  confirmed while planning this. If the broker isn't available on that architecture, MSAL's
  documented behavior is to fall back to a system-browser sign-in automatically rather than fail
  outright — but confirm sign-in actually completes one way or the other before considering ARM64
  support done, since this is the one architecture-specific behavior difference nothing else in
  this plan exercises.

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
