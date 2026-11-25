using System.Text;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>The barcode symbology a code was read as, which decides the check it is validated by.</summary>
public enum BarcodeFormat
{
    Unknown = 0,
    Ean13 = 1,
    UpcA = 2,
    Ean8 = 3,
    UpcE = 4,
    Code128 = 5
}

/// <summary>
///     Immutable domain value object encapsulating barcode representation,
///     symbology validation, Modulo-10 checksum verification, and GTIN normalization.
///     Zero framework or UI dependencies.
/// </summary>
public sealed class Barcode : IEquatable<Barcode>
{
    public const int Ean13Length = 13;
    public const int UpcALength = 12;
    public const int Ean8Length = 8;
    public const int UpcECompressedLength = 6;
    public const int UpcEFullLength = 8;
    public const int MinCode128Length = 1;
    public const int MaxCode128Length = 128;

    public const int ModuloDivisor = 10;
    public const int WeightOdd = 1;
    public const int WeightEven = 3;

    public const int MinPrintableAscii = 32;
    public const int MaxPrintableAscii = 126;

    private const char ZeroChar = '0';
    private const char OneChar = '1';
    private const char TwoChar = '2';
    private const char ThreeChar = '3';
    private const char FourChar = '4';
    private const char FiveChar = '5';
    private const char NineChar = '9';

    /// <summary>Stores a parsed barcode together with the outcome of its validation.</summary>
    private Barcode(
        string rawValue,
        string normalizedValue,
        BarcodeFormat format,
        bool isValid,
        string? errorMessage = null)
    {
        RawValue = rawValue;
        NormalizedValue = normalizedValue;
        Format = format;
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>The characters exactly as the scanner reported them.</summary>
    public string RawValue { get; }
    /// <summary>The value after the format's normalization rules.</summary>
    public string NormalizedValue { get; }
    /// <summary>Symbology the barcode uses.</summary>
    public BarcodeFormat Format { get; }
    /// <summary>True when the value satisfies its format's length and checksum rules.</summary>
    public bool IsValid { get; }
    /// <summary>Why the value is not a valid barcode, or nothing when it is.</summary>
    public string? ErrorMessage { get; }

    /// <summary>Accept only an exact stored key; substring and case-folded matches are not product identity.</summary>
    public bool MatchesExactLookupValue(string? storedValue) =>
        storedValue is not null && GetExactLookupValues().Contains(storedValue, StringComparer.Ordinal);

    /// <summary>The codes worth querying for this barcode, so a UPC is also looked up as its EAN form.</summary>
    public IReadOnlyList<string> GetExactLookupValues()
    {
        if (!IsValid)
            return [];

        if (Format is not (BarcodeFormat.Ean13 or BarcodeFormat.UpcA or BarcodeFormat.UpcE) ||
            NormalizedValue.Length != Ean13Length || NormalizedValue[0] != ZeroChar)
            return RawValue == NormalizedValue ? [NormalizedValue] : [NormalizedValue, RawValue];

        var upcA = NormalizedValue[1..];
        return RawValue == NormalizedValue || RawValue == upcA
            ? [NormalizedValue, upcA]
            : [NormalizedValue, upcA, RawValue];
    }

    /// <summary>Validates a scanned code against its symbology, falling back to a looser check when the format is unknown.</summary>
    public static Barcode Create(string? input, BarcodeFormat format = BarcodeFormat.Unknown)
    {
        if (string.IsNullOrWhiteSpace(input))
            return new Barcode(string.Empty, string.Empty, format, false, "Barcode cannot be empty.");

        var trimmed = input.Trim();

        var detectedFormat = format == BarcodeFormat.Unknown ? DetectFormat(trimmed) : format;

        return detectedFormat switch
        {
            BarcodeFormat.Ean13 => ValidateEan13(trimmed),
            BarcodeFormat.UpcA => ValidateUpcA(trimmed),
            BarcodeFormat.Ean8 => ValidateEan8(trimmed),
            BarcodeFormat.UpcE => ValidateUpcE(trimmed),
            BarcodeFormat.Code128 => ValidateCode128(trimmed),
            _ => ValidateGenericOrFallback(trimmed)
        };
    }

    /// <summary>Infers the symbology from the digit count, so a scanner that omits the format still works.</summary>
    private static BarcodeFormat DetectFormat(string text)
    {
        if (IsAllDigits(text))
        {
            return text.Length switch
            {
                Ean13Length => BarcodeFormat.Ean13,
                UpcALength => BarcodeFormat.UpcA,
                Ean8Length => BarcodeFormat.Ean8,
                UpcECompressedLength or UpcEFullLength => BarcodeFormat.UpcE,
                _ => BarcodeFormat.Code128
            };
        }

        return BarcodeFormat.Code128;
    }

    /// <summary>Checks the EAN-13 check digit, refusing a misread code before it reaches the catalogue.</summary>
    private static Barcode ValidateEan13(string text)
    {
        if (!IsAllDigits(text) || text.Length != Ean13Length)
            return new Barcode(text, text, BarcodeFormat.Ean13, false,
                $"EAN-13 must be exactly {Ean13Length} numeric digits.");

        var calculatedCheck = CalculateModulo10CheckDigit(text[..^1], WeightOdd, WeightEven);
        var actualCheck = text[^1] - ZeroChar;

        if (calculatedCheck != actualCheck)
            return new Barcode(text, text, BarcodeFormat.Ean13, false,
                $"EAN-13 checksum validation failed (expected {calculatedCheck}, got {actualCheck}).");

        return new Barcode(text, text, BarcodeFormat.Ean13, true);
    }

    /// <summary>Checks the UPC-A check digit.</summary>
    private static Barcode ValidateUpcA(string text)
    {
        if (!IsAllDigits(text) || text.Length != UpcALength)
            return new Barcode(text, text, BarcodeFormat.UpcA, false,
                $"UPC-A must be exactly {UpcALength} numeric digits.");

        var calculatedCheck = CalculateModulo10CheckDigit(text[..^1], WeightEven, WeightOdd);
        var actualCheck = text[^1] - ZeroChar;

        if (calculatedCheck != actualCheck)
            return new Barcode(text, text, BarcodeFormat.UpcA, false,
                $"UPC-A checksum validation failed (expected {calculatedCheck}, got {actualCheck}).");

        // Normalize UPC-A to 13-digit GTIN by prepending '0'
        var normalized = string.Concat(ZeroChar, text);
        return new Barcode(text, normalized, BarcodeFormat.UpcA, true);
    }

    /// <summary>Checks the EAN-8 check digit.</summary>
    private static Barcode ValidateEan8(string text)
    {
        if (!IsAllDigits(text) || text.Length != Ean8Length)
            return new Barcode(text, text, BarcodeFormat.Ean8, false,
                $"EAN-8 must be exactly {Ean8Length} numeric digits.");

        var calculatedCheck = CalculateModulo10CheckDigit(text[..^1], WeightEven, WeightOdd);
        var actualCheck = text[^1] - ZeroChar;

        if (calculatedCheck != actualCheck)
            return new Barcode(text, text, BarcodeFormat.Ean8, false,
                $"EAN-8 checksum validation failed (expected {calculatedCheck}, got {actualCheck}).");

        return new Barcode(text, text, BarcodeFormat.Ean8, true);
    }

    /// <summary>Checks a UPC-E code and expands it to its UPC-A equivalent.</summary>
    private static Barcode ValidateUpcE(string text)
    {
        if (!IsAllDigits(text) || (text.Length != UpcECompressedLength && text.Length != UpcEFullLength))
            return new Barcode(text, text, BarcodeFormat.UpcE, false,
                $"UPC-E must be {UpcECompressedLength} or {UpcEFullLength} numeric digits.");

        var numberSystem = text.Length == UpcEFullLength ? text[0] : ZeroChar;
        var payload = text.Length == UpcEFullLength ? text.Substring(1, UpcECompressedLength) : text;
        var expandedUpcA = ExpandUpcEToUpcA(payload, numberSystem);

        if (expandedUpcA is null)
            return new Barcode(text, text, BarcodeFormat.UpcE, false, "Invalid UPC-E payload structure.");

        // If an explicit 8-digit check digit was given, verify it against expanded UPC-A
        if (text.Length == UpcEFullLength)
        {
            var expectedCheck = text[^1] - ZeroChar;
            var actualCheck = expandedUpcA[^1] - ZeroChar;
            if (expectedCheck != actualCheck)
                return new Barcode(text, text, BarcodeFormat.UpcE, false,
                    $"UPC-E checksum failed (expected {expectedCheck}, got {actualCheck}).");
        }

        var normalizedGtin13 = string.Concat(ZeroChar, expandedUpcA);
        return new Barcode(text, normalizedGtin13, BarcodeFormat.UpcE, true);
    }

    /// <summary>Accepts a Code 128 payload, which carries no check digit to verify here.</summary>
    private static Barcode ValidateCode128(string text)
    {
        if (text.Length < MinCode128Length || text.Length > MaxCode128Length)
            return new Barcode(text, text, BarcodeFormat.Code128, false,
                $"Code-128 length must be between {MinCode128Length} and {MaxCode128Length} characters.");

        foreach (var c in text)
        {
            if (c < MinPrintableAscii || c > MaxPrintableAscii)
                return new Barcode(text, text, BarcodeFormat.Code128, false,
                    "Code-128 contains invalid non-printable characters.");
        }

        return new Barcode(text, text, BarcodeFormat.Code128, true);
    }

    /// <summary>Accepts a code of a symbology this build does not verify, rather than refusing a valid scan.</summary>
    private static Barcode ValidateGenericOrFallback(string text)
    {
        if (IsAllDigits(text) && text.Length >= Ean8Length && text.Length <= Ean13Length)
            return new Barcode(text, text, BarcodeFormat.Unknown, true);

        return ValidateCode128(text);
    }

    /// <summary>Expands the six digits to the twelve-digit UPC-A form, or null when the pattern is invalid.</summary>
    public static string? ExpandUpcEToUpcA(string sixDigits, char numberSystem = ZeroChar)
    {
        if (sixDigits.Length != UpcECompressedLength || !IsAllDigits(sixDigits) ||
            numberSystem is not (ZeroChar or OneChar))
            return null;

        var d1 = sixDigits[0];
        var d2 = sixDigits[1];
        var d3 = sixDigits[2];
        var d4 = sixDigits[3];
        var d5 = sixDigits[4];
        var d6 = sixDigits[5];

        var sb = new StringBuilder(UpcALength);
        sb.Append(numberSystem);

        switch (d6)
        {
            case ZeroChar:
            case OneChar:
            case TwoChar:
                sb.Append(d1).Append(d2).Append(d6).Append(ZeroChar).Append(ZeroChar);
                sb.Append(ZeroChar).Append(ZeroChar).Append(d3).Append(d4).Append(d5);
                break;
            case ThreeChar:
                sb.Append(d1).Append(d2).Append(d3).Append(ZeroChar).Append(ZeroChar);
                sb.Append(ZeroChar).Append(ZeroChar).Append(ZeroChar).Append(d4).Append(d5);
                break;
            case FourChar:
                sb.Append(d1).Append(d2).Append(d3).Append(d4).Append(ZeroChar);
                sb.Append(ZeroChar).Append(ZeroChar).Append(ZeroChar).Append(ZeroChar).Append(d5);
                break;
            default:
                sb.Append(d1).Append(d2).Append(d3).Append(d4).Append(d5);
                sb.Append(ZeroChar).Append(ZeroChar).Append(ZeroChar).Append(ZeroChar).Append(d6);
                break;
        }

        var elevenDigits = sb.ToString();
        var checkDigit = CalculateModulo10CheckDigit(elevenDigits, WeightEven, WeightOdd);
        sb.Append(checkDigit);

        return sb.ToString();
    }

    /// <summary>The GS1 modulo-10 check digit, shared by every EAN and UPC form.</summary>
    public static int CalculateModulo10CheckDigit(string digits, int firstWeight, int secondWeight)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[i] - ZeroChar;
            var weight = i % 2 == 0 ? firstWeight : secondWeight;
            sum += digit * weight;
        }

        var remainder = sum % ModuloDivisor;
        return (ModuloDivisor - remainder) % ModuloDivisor;
    }

    /// <summary>Whether the payload is digits only, which every verified form requires.</summary>
    private static bool IsAllDigits(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var c in text)
        {
            if (c < ZeroChar || c > NineChar)
                return false;
        }

        return true;
    }

    /// <summary>Compares two barcodes by normalized value and format.</summary>
    public bool Equals(Barcode? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(NormalizedValue, other.NormalizedValue, StringComparison.Ordinal);
    }

    /// <summary>Compares against another barcode, treating any other type as unequal.</summary>
    public override bool Equals(object? obj) => Equals(obj as Barcode);

    /// <summary>Hashes the normalized value so equal barcodes hash alike.</summary>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(NormalizedValue);

    /// <summary>Renders the barcode as format:value, or the invalid raw text.</summary>
    public override string ToString() => IsValid ? $"{Format}:{NormalizedValue}" : $"Invalid({RawValue})";
}
