namespace MuscleCuties.Core.Models.Entities.Users;

// Stores only a signed receipt's opaque unique ID, never the code, token or email.
public sealed class EmailReceiptRedemption
{
    public string Id { get; set; } = string.Empty;
    public int Purpose { get; set; }
    public DateTime RedeemedAtUtc { get; set; }
}
