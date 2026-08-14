namespace ProductionManagement.UnitTests;

/// <summary>
/// Readable stand-in ids. Ids are Guids in the domain, but these tests still reason about
/// "plan 2" and "user 1", so a seed number maps to a stable, obviously-fake Guid. Using a
/// fixed pattern instead of <see cref="Guid.NewGuid"/> keeps failures reproducible.
/// </summary>
internal static class TestIds
{
    public static Guid Of(int seed) => new($"00000000-0000-0000-0000-{seed:D12}");
}
