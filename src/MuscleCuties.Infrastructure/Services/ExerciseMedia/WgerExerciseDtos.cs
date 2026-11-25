using System.Text.Json.Serialization;

namespace MuscleCuties.Core.Services.ExerciseMedia;

/// <summary>The paged envelope the wger API answers with.</summary>
public sealed class WgerApiResponse<T>
{
    [JsonPropertyName("count")] public int Count { get; set; }

    [JsonPropertyName("next")] public string? Next { get; set; }

    [JsonPropertyName("previous")] public string? Previous { get; set; }

    [JsonPropertyName("results")] public List<T> Results { get; set; } = [];
}

/// <summary>One wger exercise: its names, muscles, equipment and images.</summary>
public sealed class WgerExerciseInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")] public WgerCategory? Category { get; set; }

    [JsonPropertyName("muscles")] public List<WgerMuscle> Muscles { get; set; } = [];

    [JsonPropertyName("muscles_secondary")]
    /// <summary>The muscles worked secondarily, which the plan uses to avoid overlapping load.</summary>
    public List<WgerMuscle> MusclesSecondary { get; set; } = [];

    [JsonPropertyName("equipment")] public List<WgerEquipment> Equipment { get; set; } = [];

    [JsonPropertyName("images")] public List<WgerImage> Images { get; set; } = [];
}

/// <summary>An exercise category as wger groups them.</summary>
public sealed class WgerCategory
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

/// <summary>One muscle, with an image that may be used as the exercise illustration.</summary>
public sealed class WgerMuscle
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_en")] public string? NameEn { get; set; }

    [JsonPropertyName("is_front")] public bool IsFront { get; set; }

    [JsonPropertyName("image_url_main")] public string? ImageUrlMain { get; set; }

    [JsonPropertyName("image_url_secondary")]
    /// <summary>The muscle's secondary illustration, used when it has no primary one.</summary>
    public string? ImageUrlSecondary { get; set; }
}

/// <summary>One piece of equipment an exercise needs, which the plan matches against the profile's equipment.</summary>
public sealed class WgerEquipment
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

/// <summary>One image of an exercise.</summary>
public sealed class WgerImage
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("image")] public string ImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("is_main")] public bool IsMain { get; set; }
}

/// <summary>A fetched muscle illustration, which the media store saves locally.</summary>
public sealed class WgerMuscleSvgResult
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("is_front")] public bool IsFront { get; set; }

    [JsonPropertyName("svg_url")] public string? SvgUrl { get; set; }
}
