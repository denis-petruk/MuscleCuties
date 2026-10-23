using System.Text.Json.Serialization;

namespace MuscleCuties.Core.Services.ExerciseMedia;

public sealed class WgerApiResponse<T>
{
    [JsonPropertyName("count")] public int Count { get; set; }

    [JsonPropertyName("next")] public string? Next { get; set; }

    [JsonPropertyName("previous")] public string? Previous { get; set; }

    [JsonPropertyName("results")] public List<T> Results { get; set; } = [];
}

public sealed class WgerExerciseInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")] public WgerCategory? Category { get; set; }

    [JsonPropertyName("muscles")] public List<WgerMuscle> Muscles { get; set; } = [];

    [JsonPropertyName("muscles_secondary")]
    public List<WgerMuscle> MusclesSecondary { get; set; } = [];

    [JsonPropertyName("equipment")] public List<WgerEquipment> Equipment { get; set; } = [];

    [JsonPropertyName("images")] public List<WgerImage> Images { get; set; } = [];
}

public sealed class WgerCategory
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public sealed class WgerMuscle
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("name_en")] public string? NameEn { get; set; }

    [JsonPropertyName("is_front")] public bool IsFront { get; set; }

    [JsonPropertyName("image_url_main")] public string? ImageUrlMain { get; set; }

    [JsonPropertyName("image_url_secondary")]
    public string? ImageUrlSecondary { get; set; }
}

public sealed class WgerEquipment
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public sealed class WgerImage
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("image")] public string ImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("is_main")] public bool IsMain { get; set; }
}

public sealed class WgerMuscleSvgResult
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("is_front")] public bool IsFront { get; set; }

    [JsonPropertyName("svg_url")] public string? SvgUrl { get; set; }
}
