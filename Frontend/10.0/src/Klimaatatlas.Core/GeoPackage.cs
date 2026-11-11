using Microsoft.Data.Sqlite;

namespace Klimaatatlas.Core;

/// <summary>
/// Opens a GeoPackage (an SQLite database) for reading and writing attribute values.
/// Replaces clsKlimaatatlas.SetGeoPackageConnection and the System.Data.SQLite dependency.
/// </summary>
public static class GeoPackage
{
    /// <summary>Default name of the SpatiaLite extension; SQLite adds .dll / .so / .dylib per platform.</summary>
    public const string DefaultSpatialiteExtension = "mod_spatialite";

    /// <param name="path">Path to the .gpkg file. Must exist.</param>
    /// <param name="spatialiteExtension">
    /// SpatiaLite library to load, or null to skip it. The rules only read and write attribute columns,
    /// so SpatiaLite is optional; it is loaded when available so GeoPackage triggers that call spatial
    /// functions keep working. When loading fails a warning is logged and processing continues.
    /// </param>
    public static SqliteConnection Open(string path, DiagnosticsLog log, string? spatialiteExtension = DefaultSpatialiteExtension)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Path to geopackage does not exist: {path}", path);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false, // release the file handle as soon as the connection closes
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        log.Trace("Geopackage successfully opened.");

        if (spatialiteExtension is not null)
        {
            try
            {
                AddLibraryDirectoryToSearchPath(spatialiteExtension);
                connection.LoadExtension(spatialiteExtension);
                log.Trace($"Geopackage spatialite extension loaded ({spatialiteExtension}).");
            }
            catch (SqliteException ex)
            {
                log.AddWarning(
                    $"SpatiaLite extension '{spatialiteExtension}' could not be loaded ({string.Join(' ', ex.Message.Split((char[])['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Replace(" '.", "'.")}). " +
                    "Continuing without it; this is only a problem if the geopackage has triggers that use spatial functions.");
            }
        }

        return connection;
    }

    /// <summary>
    /// On Windows mod_spatialite.dll needs its sibling DLLs (libgeos, libproj, ...). Windows only finds
    /// those via PATH, so put the extension's own folder in front of PATH for this process.
    /// </summary>
    private static void AddLibraryDirectoryToSearchPath(string extensionPath)
    {
        // A bare name such as "mod_spatialite" is resolved by the OS loader itself.
        if (!OperatingSystem.IsWindows() || !extensionPath.Contains('/') && !extensionPath.Contains('\\')) return;
        var directory = Path.GetDirectoryName(Path.GetFullPath(extensionPath));
        if (string.IsNullOrEmpty(directory)) return;

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (!path.Split(Path.PathSeparator).Contains(directory, StringComparer.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + path);
    }
}
