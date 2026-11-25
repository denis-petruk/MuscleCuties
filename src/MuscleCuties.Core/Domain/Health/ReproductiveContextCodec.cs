using System.Text.RegularExpressions;

namespace MuscleCuties.Core.Domain.Health;

/// <summary>Encodes the reproductive context for storage, so its values never appear in clear text.</summary>
public static class ReproductiveContextCodec
{
    private static readonly Regex PregnancyRegex = new(@"""Pregnancy""\s*:\s*(-?\d+)", RegexOptions.Compiled);
    private static readonly Regex ContraceptionRegex = new(@"""Contraception""\s*:\s*(-?\d+)", RegexOptions.Compiled);

    /// <summary>The stored form of the context.</summary>
    public static string Serialize(ReproductiveContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return $"{{\"Pregnancy\":{(int)context.Pregnancy},\"Contraception\":{(int)context.Contraception}}}";
    }

    /// <summary>Reads the stored form, returning an empty context for a value it cannot read rather than throwing.</summary>
    public static ReproductiveContext Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ReproductiveContext.Default;

        // Read the pre-PostgreSQL compact form while writing valid JSON for jsonb-backed profiles.
        var parts = raw.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var legacyPregnancy) &&
            int.TryParse(parts[1], out var legacyContraception))
            return new ReproductiveContext(ParsePregnancy(legacyPregnancy), ParseContraception(legacyContraception));

        try
        {
            var matchPregnancy = PregnancyRegex.Match(raw);
            var matchContraception = ContraceptionRegex.Match(raw);

            if (!matchPregnancy.Success && !matchContraception.Success)
                return ReproductiveContext.Default;

            var pregnancy = matchPregnancy.Success && int.TryParse(matchPregnancy.Groups[1].Value, out var pregnancyNumber)
                ? ParsePregnancy(pregnancyNumber)
                : PregnancyStatus.Unknown;

            var contraception = matchContraception.Success && int.TryParse(matchContraception.Groups[1].Value, out var contraceptionNumber)
                ? ParseContraception(contraceptionNumber)
                : ContraceptionStatus.Unknown;

            return new ReproductiveContext(pregnancy, contraception);
        }
        catch
        {
            return ReproductiveContext.Default;
        }
    }

    /// <summary>Reads the pregnancy value, treating an unknown one as not pregnant so no load is invented.</summary>
    private static PregnancyStatus ParsePregnancy(int value) => Enum.IsDefined(typeof(PregnancyStatus), value)
        ? (PregnancyStatus)value : PregnancyStatus.Unknown;

    /// <summary>Reads the contraception value, treating an unknown one as unset.</summary>
    private static ContraceptionStatus ParseContraception(int value) => Enum.IsDefined(typeof(ContraceptionStatus), value)
        ? (ContraceptionStatus)value : ContraceptionStatus.Unknown;
}
