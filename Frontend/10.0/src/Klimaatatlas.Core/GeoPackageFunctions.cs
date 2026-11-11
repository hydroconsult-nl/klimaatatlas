using System.Buffers.Binary;
using Microsoft.Data.Sqlite;

namespace Klimaatatlas.Core;

/// <summary>
/// Minimal implementations of the spatial SQL functions that GeoPackage rtree triggers call
/// (ST_IsEmpty, ST_MinX, ST_MaxX, ST_MinY, ST_MaxY). GDAL creates triggers such as
/// <c>AFTER UPDATE ON table ... ST_IsEmpty(NEW.geom)</c> that SQLite compiles into every UPDATE,
/// even when only attribute columns change; without these functions such an UPDATE fails with
/// "no such function". Registered only when SpatiaLite is not available.
///
/// They read the standard GeoPackage binary header (http://www.geopackage.org/spec/#gpb_format):
/// "GP", version, flags (bit 0 byte order, bits 1-3 envelope type, bit 4 empty geometry), srs_id, envelope.
/// </summary>
public static class GeoPackageFunctions
{
    public static void Register(SqliteConnection connection)
    {
        connection.CreateFunction("ST_IsEmpty", (byte[]? geom) => IsEmpty(geom), isDeterministic: true);
        connection.CreateFunction("ST_MinX", (byte[]? geom) => Envelope(geom, 0), isDeterministic: true);
        connection.CreateFunction("ST_MaxX", (byte[]? geom) => Envelope(geom, 1), isDeterministic: true);
        connection.CreateFunction("ST_MinY", (byte[]? geom) => Envelope(geom, 2), isDeterministic: true);
        connection.CreateFunction("ST_MaxY", (byte[]? geom) => Envelope(geom, 3), isDeterministic: true);
    }

    /// <summary>1 when the geometry is flagged empty, 0 otherwise, NULL for NULL or non-GeoPackage blobs.</summary>
    public static long? IsEmpty(byte[]? geom) =>
        IsGeoPackageBlob(geom) ? ((geom![3] & 0x10) != 0 ? 1 : 0) : null;

    /// <param name="index">0 = minx, 1 = maxx, 2 = miny, 3 = maxy (header envelope order).</param>
    public static double? Envelope(byte[]? geom, int index)
    {
        if (!IsGeoPackageBlob(geom) || (geom![3] & 0x10) != 0) return null;

        var envelopeType = (geom[3] >> 1) & 0x07;
        if (envelopeType is 0 or > 4)
            // Only reached when the geometry itself is inserted or changed, which the rules never do.
            throw new NotSupportedException("Geometry without envelope in its header; load SpatiaLite (--spatialite) to edit geometries.");

        var offset = 8 + index * 8;
        if (geom.Length < offset + 8) return null;
        var span = geom.AsSpan(offset, 8);
        return (geom[3] & 0x01) != 0 ? BinaryPrimitives.ReadDoubleLittleEndian(span) : BinaryPrimitives.ReadDoubleBigEndian(span);
    }

    private static bool IsGeoPackageBlob(byte[]? geom) => geom is { Length: >= 8 } && geom[0] == (byte)'G' && geom[1] == (byte)'P';
}
