using System.Text.Json;
using System.Text.Json.Serialization;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>
/// Serialization settings for persisted planning snapshots. The request DTO exposes
/// <see cref="IReadOnlySet{T}"/> values, which System.Text.Json cannot construct directly.
/// </summary>
internal static class WorkoutPlanningJson
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ReadOnlySetJsonConverterFactory());
        return options;
    }
}

internal sealed class ReadOnlySetJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType &&
        typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlySet<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var elementType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ReadOnlySetJsonConverter<>).MakeGenericType(elementType);
        return (JsonConverter)Activator.CreateInstance(converterType, nonPublic: true)!;
    }

    private sealed class ReadOnlySetJsonConverter<T> : JsonConverter<IReadOnlySet<T>>
    {
        public override IReadOnlySet<T> Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            var values = JsonSerializer.Deserialize<T[]>(ref reader, options);
            return values is null ? new HashSet<T>() : new HashSet<T>(values);
        }

        public override void Write(Utf8JsonWriter writer, IReadOnlySet<T> value,
            JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
}
