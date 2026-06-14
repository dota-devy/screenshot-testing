namespace Surfshack.Screenshots.Testing.Tests;

/// <summary>
/// Filters Playwright console "error" messages so screenshot tests only fail on
/// real JavaScript errors, ignoring network/CORS errors caused by the hermetic
/// browser-context routing (which aborts non-loopback requests on purpose).
/// </summary>
public static class ScreenshotConsoleFilter
{
    public static bool IsIgnorableNetworkError(string consoleText)
    {
        if (string.IsNullOrEmpty(consoleText))
            return false;

        return consoleText.Contains("net::ERR_")
            || consoleText.Contains("Failed to load resource")
            || consoleText.Contains("Access to font")
            || consoleText.Contains("Access to script")
            || consoleText.Contains("Access to stylesheet")
            || consoleText.Contains("Access to fetch");
    }
}
