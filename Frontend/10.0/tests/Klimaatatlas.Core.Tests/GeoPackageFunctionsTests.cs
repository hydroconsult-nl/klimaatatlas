using System.Buffers.Binary;
using Klimaatatlas.Core;

namespace Klimaatatlas.Core.Tests;

public class GeoPackageFunctionsTests
{
    private static byte[] Blob(byte flags, params double[] envelope)
    {
        var blob = new byte[8 + envelope.Length * 8 + 5];
        blob[0] = (byte)'G'; blob[1] = (byte)'P'; blob[3] = flags;
        for (var i = 0; i < envelope.Length; i++)
        {
            var span = blob.AsSpan(8 + i * 8, 8);
            if ((flags & 1) != 0) BinaryPrimitives.WriteDoubleLittleEndian(span, envelope[i]);
            else BinaryPrimitives.WriteDoubleBigEndian(span, envelope[i]);
        }
        return blob;
    }

    [Fact]
    public void IsEmptyReadsHeaderFlag()
    {
        Assert.Equal(0L, GeoPackageFunctions.IsEmpty(Blob(0x03, 1, 2, 3, 4)));
        Assert.Equal(1L, GeoPackageFunctions.IsEmpty(Blob(0x11)));
        Assert.Null(GeoPackageFunctions.IsEmpty(null));
        Assert.Null(GeoPackageFunctions.IsEmpty([1, 2, 3]));
    }

    [Theory]
    [InlineData(0x03)] // little endian, xy envelope
    [InlineData(0x02)] // big endian, xy envelope
    public void EnvelopeReadsMinMax(byte flags)
    {
        var blob = Blob(flags, 100, 200, 300, 400);
        Assert.Equal(100, GeoPackageFunctions.Envelope(blob, 0));
        Assert.Equal(200, GeoPackageFunctions.Envelope(blob, 1));
        Assert.Equal(300, GeoPackageFunctions.Envelope(blob, 2));
        Assert.Equal(400, GeoPackageFunctions.Envelope(blob, 3));
    }

    [Fact]
    public void EnvelopeWithoutHeaderEnvelopeIsNotSupported() =>
        Assert.Throws<NotSupportedException>(() => GeoPackageFunctions.Envelope(Blob(0x01), 0));
}
