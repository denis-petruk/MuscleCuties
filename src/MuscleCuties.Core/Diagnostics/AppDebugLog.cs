using System.Diagnostics;

namespace MuscleCuties.Core.Diagnostics;

public static class AppDebugLog
{
    [Conditional("DEBUG")]
    public static void Write(string area, string message)
    {
        Debug.WriteLine($"[MuscleCuties][{DateTimeOffset.Now:HH:mm:ss.fff}][{area}] {message}");
    }

    [Conditional("DEBUG")]
    public static void Error(string area, Exception exception, string message)
    {
        Debug.WriteLine(
            $"[MuscleCuties][{DateTimeOffset.Now:HH:mm:ss.fff}][{area}] ERROR {message}: " +
            $"{exception.GetType().Name}: {exception.Message}");
        Debug.WriteLine(exception);
    }
}
