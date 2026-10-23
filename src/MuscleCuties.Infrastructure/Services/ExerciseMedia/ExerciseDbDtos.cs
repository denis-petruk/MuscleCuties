using System.Text.Json.Serialization;

namespace MuscleCuties.Core.Services.ExerciseMedia;

public sealed class ExerciseDbItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bodyPart")] public string BodyPart { get; set; } = string.Empty;

    [JsonPropertyName("equipment")] public string Equipment { get; set; } = string.Empty;

    [JsonPropertyName("target")] public string Target { get; set; } = string.Empty;

    [JsonPropertyName("secondaryMuscles")] public List<string> SecondaryMuscles { get; set; } = [];

    [JsonPropertyName("instructions")] public List<string> Instructions { get; set; } = [];

    /// <summary>
    ///     Note: Remote animated GIFs from ExerciseDB must be bypassed or replaced
    ///     with local vector Lottie animations to prevent CollectionView scrolling lag.
    /// </summary>
    [JsonPropertyName("gifUrl")]
    public string? GifUrl { get; set; }
}
