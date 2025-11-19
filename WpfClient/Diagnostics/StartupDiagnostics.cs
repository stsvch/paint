using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WpfClient.Diagnostics;

internal sealed class StartupDiagnostics
{
    private readonly string _logFile;

    public StartupDiagnostics()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PaintWpf",
            "logs");

        Directory.CreateDirectory(logDir);
        _logFile = Path.Combine(logDir, $"startup-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.log");
    }

    public string LogFilePath => _logFile;

    public string WriteStartupInfo()
    {
        var runtimes = CaptureDotnetRuntimes();

        var sb = new StringBuilder();
        sb.AppendLine($"Timestamp: {DateTimeOffset.Now:O}");
        sb.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($"Process architecture: {(Environment.Is64BitProcess ? "x64" : "x86")}");
        sb.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Runtime identifier: {RuntimeInformation.RuntimeIdentifier}");
        sb.AppendLine("Runtimes reported by `dotnet --list-runtimes`:");
        sb.AppendLine(runtimes);

        File.WriteAllText(_logFile, sb.ToString());

        return runtimes;
    }

    public void LogException(Exception ex, string context)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"[{DateTimeOffset.Now:O}] {context}");
        sb.AppendLine(ex.ToString());
        File.AppendAllText(_logFile, sb.ToString());
    }

    public void LogMessage(string message)
    {
        File.AppendAllText(
            _logFile,
            $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}");
    }

    public bool HasSupportedWindowsDesktopRuntime(string runtimesOutput)
    {
        foreach (var line in runtimesOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains("Microsoft.WindowsDesktop.App", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !Version.TryParse(parts[1], out var version))
            {
                continue;
            }

            if (version.Major >= 8)
            {
                return true;
            }
        }

        return false;
    }

    private static string CaptureDotnetRuntimes()
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return "`dotnet` process failed to start."
                    + Environment.NewLine
                    + "Убедитесь, что установлен .NET Desktop Runtime (Microsoft.WindowsDesktop.App).";
            }

            var output = process.StandardOutput.ReadToEnd();
            var errors = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            var combined = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(output))
            {
                combined.AppendLine(output.TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(errors))
            {
                combined.AppendLine("stderr:");
                combined.AppendLine(errors.TrimEnd());
            }

            return combined.Length == 0 ? "(no output)" : combined.ToString();
        }
        catch (Exception ex)
        {
            return "Не удалось получить список рантаймов: " + ex.Message;
        }
    }
}
