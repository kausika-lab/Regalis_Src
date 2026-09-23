using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Regalis.API.Services;

/// <summary>
/// Development convenience (AUTOSTART_FRONTEND=true, set in launchSettings.json):
/// starts the Vite dev server alongside the API when it is not already running,
/// so F5 on Regalis.API brings up the whole stack without relying on the VS
/// JavaScript debugger. Never active in Test/UAT/Production.
/// </summary>
public static class FrontendDevLauncher
{
    private const int VitePort = 5173;

    public static void EnsureRunning(string contentRootPath, ILogger logger)
    {
        try
        {
            if (IsPortListening(VitePort))
            {
                logger.LogInformation("Frontend dev server already running on port {Port}.", VitePort);
                return;
            }

            // ContentRoot is backend/src/Regalis.API; the frontend lives at the repo root.
            var frontendDir = Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "..", "frontend"));
            if (!File.Exists(Path.Combine(frontendDir, "package.json")))
            {
                logger.LogWarning("Frontend not found at {Dir}; skipping dev-server autostart.", frontendDir);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k title Regalis.Frontend (vite) & npm run dev",
                WorkingDirectory = frontendDir,
                UseShellExecute = true,
            });
            logger.LogInformation("Started frontend dev server (npm run dev) in {Dir}.", frontendDir);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not start the frontend dev server; run 'npm run dev' manually.");
        }
    }

    private static bool IsPortListening(int port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(IPAddress.Loopback, port).Wait(300);
        }
        catch
        {
            return false;
        }
    }
}
