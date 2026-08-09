using System.Diagnostics;

namespace BootCampCharge;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // WinForms requires high DPI awareness on modern displays.
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 1. Administrator check — SMC port I/O and the InpOut driver require elevation.
        if (!IsRunningAsAdmin())
        {
            // Relaunch as admin.
            string exe = Environment.ProcessPath ?? Application.ExecutablePath;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "BootCampCharge must run as Administrator to access the SMC hardware.\n\n" +
                    "The driver that talks to the Apple SMC requires elevated privileges.\n\n" +
                    ex.Message,
                    "Administrator Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            return;
        }

        // 2. Ensure the InpOut kernel driver is available.
        try
        {
            if (!InpOut.DriverOpen())
            {
                MessageBox.Show(
                    "The InpOut kernel driver is not loaded.\n\n" +
                    "Place inpoutx64.dll next to BootCampCharge.exe and run as Administrator.\n" +
                    "The DLL installs its driver automatically on first elevated launch.",
                    "Driver Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Failed to initialise the InpOut driver:\n\n" + ex.Message,
                "Driver Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // 3. Open the SMC and launch the tray application.
        TrayApp? app = null;
        try
        {
            var battery = BatteryManager.Open();
            var config = AppConfig.Load();
            config.ApplyAutoStart();

            app = new TrayApp(battery, config);
            Application.ApplicationExit += (s, e) => app.OnExit();
            Application.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Failed to start BootCampCharge:\n\n" + ex.Message +
                "\n\nThis tool only works on Intel Macs running Windows via Boot Camp.",
                "Startup Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            app?.Dispose();
        }
    }

    /// <summary>True when the current process has administrator privileges.</summary>
    private static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
