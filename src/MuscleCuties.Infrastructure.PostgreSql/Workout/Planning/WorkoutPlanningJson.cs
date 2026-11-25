using System.Text.Json.Serialization;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>
/// Serialization settings for persisted planning snapshots. The request DTO exposes
/// <see cref="IReadOnlySet{T}"/> values, which System.Text.Json cannot construct directly.
/// </summary>
internal static class WorkoutPlanningJson
{
    /// <summary>The serializer settings the planning documents are written with, so a stored plan reads back the same way everywhere.</summary>
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ReadOnlySetJsonConverterFactory());
        return options;
    }
}

/// <summary>Serialises a read-only set as an array, which is the only shape the stored JSON accepts.</summary>
internal sealed class ReadOnlySetJsonConverterFactory : JsonConverterFactory
{
    /// <summary>True when the type is a read-only set the converter handles.</summary>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType &&
        typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlySet<>);

    /// <summary>The converter for the closed generic type.</summary>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var elementType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(ReadOnlySetJsonConverter<>).MakeGenericType(elementType);
        return (JsonConverter)Activator.CreateInstance(converterType, nonPublic: true)!;
    }

    /// <summary>The per-element set converter the factory builds.</summary>
    private sealed class ReadOnlySetJsonConverter<T> : JsonConverter<IReadOnlySet<T>>
    {
        /// <summary>Reads a JSON array into a read-only set.</summary>
        public override IReadOnlySet<T> Read(ref Utf8JsonReader reader, Type typeToConvert,
            JsonSerializerOptions options)
        {
            var values = JsonSerializer.Deserialize<T[]>(ref reader, options);
            return values is null ? new HashSet<T>() : new HashSet<T>(values);
        }

        /// <summary>Writes a read-only set as a JSON array.</summary>
        public override void Write(Utf8JsonWriter writer, IReadOnlySet<T> value,
            JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToArray(), options);
    }
}
