using System.Buffers.Binary;

namespace MuscleCuties.Core.Services.Nutrition;

/// <summary>
///     Provides deterministic canonical GUID encoding and decoding for LoggedMeal records,
///     allowing both LoggedMeal (int PK) and FoodItemEntry (Guid PK) to be addressed
///     transparently through a unified Guid-based identity contract.
/// </summary>
public static class CanonicalMealIdentity
{
    // Marker prefix: ASCII bytes for "LOGMEAL\0" + 4 zero bytes (12 bytes total)
    // 0x4C ('L'), 0x4F ('O'), 0x47 ('G'), 0x4D ('M'), 0x45 ('E'), 0x41 ('A'), 0x4C ('L'), 0x00
    private static readonly byte[] Marker = [0x4C, 0x4F, 0x47, 0x4D, 0x45, 0x41, 0x4C, 0x00, 0x00, 0x00, 0x00, 0x00];

    /// <summary>
    ///     Creates a deterministic canonical Guid for a LoggedMeal record.
    /// </summary>
    public static Guid ForLoggedMeal(int loggedMealId)
    {
        if (loggedMealId <= 0)
            throw new ArgumentOutOfRangeException(nameof(loggedMealId), "Meal ID must be greater than zero.");

        Span<byte> bytes = stackalloc byte[16];
        Marker.CopyTo(bytes);
        BinaryPrimitives.WriteInt32BigEndian(bytes[12..], loggedMealId);
        return new Guid(bytes);
    }

    /// <summary>
    ///     Checks whether the specified Guid is a canonical LoggedMeal Guid.
    /// </summary>
    public static bool IsCanonicalLoggedMealId(Guid id)
    {
        return TryExtractLoggedMealId(id, out _);
    }

    /// <summary>
    ///     Attempts to extract the integer LoggedMealId from a canonical Guid.
    ///     Returns true if the Guid was created by ForLoggedMeal, false otherwise.
    /// </summary>
    public static bool TryExtractLoggedMealId(Guid id, out int loggedMealId)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        if (bytes[..12].SequenceEqual(Marker))
        {
            var extracted = BinaryPrimitives.ReadInt32BigEndian(bytes[12..]);
            if (extracted > 0)
            {
                loggedMealId = extracted;
                return true;
            }
        }

        loggedMealId = 0;
        return false;
    }
}
