namespace MuscleCuties.UI.Services.Localization
{
    // The linked view model only needs localization when reporting a failure.
    internal static class UiTextCatalog
    {
        public static string Get(string key) => key switch
        {
            // Simulates missing localization copy for a catalog key that the app resolves.
            "Workout_Generation_EmptyCopy" => string.Empty,
            "Workout_Generation_WhitespaceCopy" => "   ",
            _ => key
        };

        public static bool HasKey(string key) => key.StartsWith("Workout_", StringComparison.Ordinal);

        public static string Format(string key, params object[] args) => $"{key}:{string.Join(',', args)}";

        public static string TranslateRuntimeText(string text) => text switch
        {
            "Hack Squat" => "Гакк-присед",
            "Machine Hip Thrust" => "Ягодичный мост в тренажёре",
            _ => text
        };
    }
}

namespace Microsoft.Maui.Accessibility
{
    internal interface ISemanticScreenReader
    {
        void Announce(string text);
    }

    internal sealed class SemanticScreenReaderStub : ISemanticScreenReader
    {
        public void Announce(string text) { }
    }

    internal static class SemanticScreenReader
    {
        public static ISemanticScreenReader Default { get; } = new SemanticScreenReaderStub();
    }
}
