using System.Buffers.Binary;
using System.Security.Cryptography;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using SkiaSharp;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed class SkiaPhotoByteSanitizer : IPhotoByteSanitizer
{
    public const int MaximumInputBytes = 20 * 1024 * 1024;
    public const int MaximumOutputBytes = 15 * 1024 * 1024;
    public const int MaximumPixels = 40_000_000;
    private const int MaximumDimension = 12_000;
    private const int JpegSofStart = 0xc0;
    private const int JpegSofEnd = 0xcf;
    private const int JpegApp2Marker = 0xe2;
    private static ReadOnlySpan<byte> MultiPictureFormatSignature => "MPF\0"u8;

    public SanitizedPhotoBytes Sanitize(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = source.Span;
        if (input.IsEmpty || input.Length > MaximumInputBytes || !IsCanonicalStillImage(input))
            throw new InvalidDataException("Photo input is not a complete bounded still JPEG or PNG.");

        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new SKMemoryStream(source.ToArray());
        using var codec = SKCodec.Create(stream);
        // Skia's Linux codec reports 0 when a still-image frame count is unspecified.
        // The canonical container parser rejects APNG; nonzero counts must still be exactly one.
        if (codec is null || codec.FrameCount > 1 || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png))
            throw new InvalidDataException("Photo bytes could not be decoded as a supported still image.");

        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > MaximumDimension || info.Height > MaximumDimension || (long)info.Width * info.Height > MaximumPixels)
            throw new InvalidDataException("Photo dimensions exceed the supported bounds.");

        cancellationToken.ThrowIfCancellationRequested();
        using var bitmap = new SKBitmap(info);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("Photo decoding failed.");

        cancellationToken.ThrowIfCancellationRequested();
        var origin = codec.EncodedOrigin;
        var swapsDimensions = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var outputWidth = swapsDimensions ? bitmap.Height : bitmap.Width;
        var outputHeight = swapsDimensions ? bitmap.Width : bitmap.Height;
        using var oriented = new SKBitmap(outputWidth, outputHeight, bitmap.ColorType, bitmap.AlphaType);
        using (var canvas = new SKCanvas(oriented))
        {
            ApplyOrigin(canvas, origin, bitmap.Width, bitmap.Height);
            canvas.DrawBitmap(bitmap, 0, 0);
            canvas.Flush();
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var image = SKImage.FromBitmap(oriented);
        using var output = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        if (output is null || output.Size == 0 || output.Size > MaximumOutputBytes)
            throw new InvalidDataException("Sanitized photo output exceeds the supported bounds.");

        cancellationToken.ThrowIfCancellationRequested();
        var bytes = output.ToArray();
        if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8 || bytes[^2] != 0xff || bytes[^1] != 0xd9)
            throw new InvalidDataException("Photo encoder produced invalid JPEG bytes.");
        return new SanitizedPhotoBytes(bytes, "image/jpeg", Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static void ApplyOrigin(SKCanvas canvas, SKEncodedOrigin origin, int width, int height)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopRight: canvas.Translate(width, 0); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.BottomRight: canvas.Translate(width, height); canvas.RotateDegrees(180); break;
            case SKEncodedOrigin.BottomLeft: canvas.Translate(0, height); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
            case SKEncodedOrigin.RightTop: canvas.Translate(height, 0); canvas.RotateDegrees(90); break;
            case SKEncodedOrigin.RightBottom: canvas.Translate(height, width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
            case SKEncodedOrigin.LeftBottom: canvas.Translate(0, width); canvas.RotateDegrees(270); break;
            case SKEncodedOrigin.TopLeft:
                break;
            default: throw new InvalidDataException("Photo orientation is not supported.");
        }
    }

    private static bool IsCanonicalStillImage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 6 && (bytes[..6].SequenceEqual("GIF87a"u8) || bytes[..6].SequenceEqual("GIF89a"u8))) return false;
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return false;
        if (bytes.Length >= 12 && bytes[4..8].SequenceEqual("ftyp"u8)) return false;
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return IsCanonicalPng(bytes);
        return IsCanonicalJpeg(bytes);
    }

    private static bool IsCanonicalPng(ReadOnlySpan<byte> bytes)
    {
        var offset = 8;
        var sawHeader = false;
        var sawData = false;
        while (offset + 12 <= bytes.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset, 4));
            if (length > int.MaxValue || offset + 12L + length > bytes.Length) return false;
            var type = bytes.Slice(offset + 4, 4);
            var data = bytes.Slice(offset + 8, (int)length);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(offset + 8 + (int)length, 4));
            if (PngCrc(bytes.Slice(offset + 4, 4 + (int)length)) != expectedCrc) return false;
            if (!sawHeader && !type.SequenceEqual("IHDR"u8)) return false;
            if (type.SequenceEqual("IHDR"u8))
            {
                if (sawHeader || length != 13) return false;
                sawHeader = true;
            }
            if (type.SequenceEqual("acTL"u8)) return false;
            if (type.SequenceEqual("IDAT"u8)) sawData = true;
            if (type.SequenceEqual("IEND"u8)) return length == 0 && sawData && offset + 12 == bytes.Length;
            offset += checked((int)length + 12);
        }
        return false;
    }

    private static uint PngCrc(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
        }
        return ~crc;
    }

    private static bool IsCanonicalJpeg(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8) return false;
        var offset = 2;
        var sawFrame = false;
        var sawScan = false;
        var pendingMarker = -1;
        while (offset < bytes.Length)
        {
            int marker;
            if (pendingMarker >= 0)
            {
                marker = pendingMarker;
                pendingMarker = -1;
            }
            else
            {
                if (bytes[offset++] != 0xff) return false;
                while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
                if (offset >= bytes.Length) return false;
                marker = bytes[offset++];
            }
            if (marker == 0xd9) return sawFrame && sawScan && offset == bytes.Length;
            if (marker is 0x00 or 0xd8 || marker is >= 0xd0 and <= 0xd7) return false;
            if (offset + 2 > bytes.Length) return false;
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (segmentLength < 2 || offset + segmentLength > bytes.Length) return false;
            // MPF APP2 metadata denotes a multi-picture JPEG container even when a platform
            // codec exposes only the primary image and reports an unspecified frame count.
            if (marker == JpegApp2Marker && segmentLength >= 6 && bytes.Slice(offset + 2, MultiPictureFormatSignature.Length).SequenceEqual(MultiPictureFormatSignature)) return false;
            if (marker is >= JpegSofStart and <= JpegSofEnd && marker is not (0xc4 or 0xc8 or 0xcc)) sawFrame = true;
            offset += segmentLength;
            if (marker != 0xda) continue;
            sawScan = true;
            var foundMarker = false;
            while (offset < bytes.Length)
            {
                if (bytes[offset++] != 0xff) continue;
                while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
                if (offset >= bytes.Length) return false;
                var scanMarker = bytes[offset];
                if (scanMarker == 0x00 || scanMarker is >= 0xd0 and <= 0xd7) { offset++; continue; }
                offset++;
                if (scanMarker == 0xd9) return sawFrame && sawScan && offset == bytes.Length;
                pendingMarker = scanMarker;
                foundMarker = true;
                break;
            }
            if (!foundMarker) return false;
        }
        return false;
    }
}
