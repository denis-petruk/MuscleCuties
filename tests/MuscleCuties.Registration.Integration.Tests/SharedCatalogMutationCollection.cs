using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

// Committed catalog edits change every account's preview fingerprint. Keep these
// fixtures separate from concurrent readers; in-test concurrency remains enabled.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SharedCatalogMutationCollection
{
    public const string Name = "Shared workout catalog mutation";
}
