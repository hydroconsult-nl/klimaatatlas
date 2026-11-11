namespace Klimaatatlas.Core;

/// <summary>
/// Remembers the last used configuration file, like My.Settings.Configfile in the old application.
/// Stored per user: %APPDATA%\Klimaatatlas on Windows, ~/.config/Klimaatatlas on Linux and macOS.
/// </summary>
public static class UserSettings
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "Klimaatatlas", "lastconfig.txt");

    public static string? LastConfigPath
    {
        get
        {
            try
            {
                var value = File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : "";
                return value.Length > 0 ? value : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
        set
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, value ?? "");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not being able to remember the path is no reason to fail.
            }
        }
    }
}
