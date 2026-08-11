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
