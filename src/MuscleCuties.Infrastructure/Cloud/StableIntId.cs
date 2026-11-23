namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>
/// Cloud ids are GUIDs, but the cycle and check-in screens key rows by int. The same GUID must always
/// produce the same positive int, so the projection lives here instead of in each cloud store.
/// </summary>
internal static class StableIntId
{
    public static int From(Guid id)
    {
        Span<byte> bytes = stackalloc byte[16];
        id.TryWriteBytes(bytes);
        var raw = BitConverter.ToInt32(bytes[..4]) & int.MaxValue;
        return raw == 0 ? 1 : raw;
    }
}
