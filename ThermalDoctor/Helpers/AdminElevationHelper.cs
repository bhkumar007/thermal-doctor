using System.Diagnostics;
using System.Security.Principal;

namespace ThermalDoctor.Helpers;

public static class AdminElevationHelper
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartAsAdmin()
    {
        var exeName = Process.GetCurrentProcess().MainModule?.FileName;
        if (exeName == null) return;

        var startInfo = new ProcessStartInfo
        {
            FileName = exeName,
            UseShellExecute = true,
            Verb = "runas"
        };

        try
        {
            Process.Start(startInfo);
            System.Windows.Application.Current.Shutdown();
        }
        catch
        {
            // User cancelled UAC prompt
        }
    }
}
