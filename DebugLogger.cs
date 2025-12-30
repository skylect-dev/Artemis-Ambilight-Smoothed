using System;
using System.IO;

namespace Artemis.Plugins.LayerBrushes.AmbilightSmoothed;

public static class DebugLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "AmbilightSmoothed-HDR-Debug.log");
    
    private static readonly object _lock = new object();
    
    static DebugLogger()
    {
        // Clear log on startup
        try
        {
            File.WriteAllText(LogPath, $"=== Ambilight Smoothed HDR Debug Log - {DateTime.Now} ===\n");
        }
        catch { }
    }
    
    public static void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
        }
        catch { }
    }
    
    public static string GetLogPath() => LogPath;
}
