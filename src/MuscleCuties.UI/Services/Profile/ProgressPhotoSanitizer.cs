using System.Buffers;

#if IOS
using CoreGraphics;
using Foundation;
using UIKit;
#endif

namespace MuscleCuties.UI.Services.Profile;

/// <summary>Produces bounded, metadata-free JPEG bytes for the progress-photo vault.</summary>
internal static class ProgressPhotoSanitizer
{
    internal const int MaximumUploadBytes = 15 * 1024 * 1024;
    internal const string OutputExtension = ".jpg";
    internal const string OutputMimeType = "image/jpeg";
    private const int MaximumSourceBytes = 60 * 1024 * 1024;
    private const int FourCharacterBrandLength = 4;
    private const int FileTypeBoxSizeFieldOffset = 0;
    private const int FileTypeBoxTypeOffset = 4;
    private const int FileTypeBoxMajorBrandOffset = 8;
    private const int FileTypeBoxHeaderLength = 16;
    private const long MaximumDecodedPixels = 40_000_000;
    private const float InitialJpegQuality = 0.88f;
    private const float MinimumJpegQuality = 0.50f;
    private const float JpegQualityStep = 0.08f;

    public static async Task<byte[]> ToJpegAsync(Stream source, CancellationToken cancellationToken = default)
    {
#if IOS
        ArgumentNullException.ThrowIfNull(source);
        using var buffer = new MemoryStream();
        var rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = await source.ReadAsync(rented.AsMemory(0, rented.Length), cancellationToken);
                if (read == 0) break;
                if (buffer.Length + read > MaximumSourceBytes)
                    throw new InvalidDataException("The selected image exceeds the supported source size.");
                buffer.Write(rented, 0, read);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented, clearArray: true);
        }

        var input = buffer.ToArray();
        try
        {
            if (!IsSupportedStillImage(input))
                throw new InvalidDataException("Only still JPEG, PNG, HEIC, and HEIF images are supported.");

            using var data = NSData.FromArray(input);
            using var image = UIImage.LoadFromData(data);
            if (image?.CGImage is not { } cgImage || cgImage.Width == 0 || cgImage.Height == 0 ||
                (long)cgImage.Width * cgImage.Height > MaximumDecodedPixels)
                throw new InvalidDataException("The selected image could not be decoded.");
            cancellationToken.ThrowIfCancellationRequested();

            // Redrawing normalizes UIImage orientation and emits only pixel data; source
            // EXIF, GPS, and other container metadata are not copied to the JPEG.
            var size = new CGSize(cgImage.Width, cgImage.Height);
            var format = new UIGraphicsImageRendererFormat { Opaque = true, Scale = 1 };
            using var renderer = new UIGraphicsImageRenderer(size, format);
            using var normalized = renderer.CreateImage(context =>
            {
                UIColor.White.SetFill();
                context.FillRect(new CGRect(CGPoint.Empty, size));
                image.Draw(new CGRect(CGPoint.Empty, size));
            });
            cancellationToken.ThrowIfCancellationRequested();

            for (var quality = InitialJpegQuality; quality >= MinimumJpegQuality; quality -= JpegQualityStep)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var output = normalized.AsJPEG(quality);
                if (output is null) throw new InvalidDataException("JPEG conversion failed.");
                var bytes = output.ToArray();
                if (bytes.Length <= MaximumUploadBytes && HasJpegSignature(bytes)) return bytes;
            }

            throw new InvalidDataException("The converted image exceeds the 15 MiB upload limit.");
        }
        finally
        {
            Array.Clear(input);
        }
#else
        await Task.CompletedTask;
        throw new PlatformNotSupportedException("Progress-photo preparation is available on iOS only.");
#endif
    }

#if IOS
    private static bool IsSupportedStillImage(ReadOnlySpan<byte> bytes)
    {
        if (HasJpegSignature(bytes)) return true;
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            // APNG animation control chunk. Reject animated input rather than silently
            // selecting a frame whose meaning may differ from the source.
            return !ContainsAscii(bytes, "acTL");
        }

        if (bytes.Length >= FileTypeBoxHeaderLength &&
            bytes.Slice(FileTypeBoxTypeOffset, FourCharacterBrandLength).SequenceEqual("ftyp"u8))
        {
            // ISO-BMFF FileTypeBox: major brand, minor version, then compatible brands.
            // heix denotes a still image using HEVC Main 10/range extensions; hevc and
            // hevx denote image sequences. Reject a sequence brand anywhere in the box.
            var boxLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(
                bytes.Slice(FileTypeBoxSizeFieldOffset, FourCharacterBrandLength));
            if (boxLength < FileTypeBoxHeaderLength || boxLength > bytes.Length ||
                (boxLength - FileTypeBoxHeaderLength) % FourCharacterBrandLength != 0)
                return false;

            var fileTypeBox = bytes[..(int)boxLength];
            var majorBrand = System.Text.Encoding.ASCII.GetString(
                bytes.Slice(FileTypeBoxMajorBrandOffset, FourCharacterBrandLength));
            var hasStillBrand = IsStillHeifBrand(majorBrand);
            for (var offset = FileTypeBoxHeaderLength; offset < fileTypeBox.Length; offset += FourCharacterBrandLength)
            {
                var brand = System.Text.Encoding.ASCII.GetString(fileTypeBox.Slice(offset, FourCharacterBrandLength));
                if (IsHeifSequenceBrand(brand)) return false;
                hasStillBrand |= IsStillHeifBrand(brand);
            }

            return hasStillBrand && !IsHeifSequenceBrand(majorBrand);
        }

        return false;
    }

    private static bool HasJpegSignature(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool ContainsAscii(ReadOnlySpan<byte> bytes, string value) =>
        bytes.IndexOf(System.Text.Encoding.ASCII.GetBytes(value)) >= 0;

    private static bool IsStillHeifBrand(string brand) => brand is "heic" or "heix" or "mif1" or "heif";

    private static bool IsHeifSequenceBrand(string brand) =>
        brand is "msf1" or "hevc" or "hevx" or "hevm" or "hevs" or "avcs";
#endif
}
