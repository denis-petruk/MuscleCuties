namespace MuscleCuties.DbMaintenance;

/// <summary>
/// A connection setting the maintenance CLI refuses to open a session with, such as a non-loopback host
/// without <c>SSL Mode=VerifyFull</c>. It is reported as an unreachable database, never as schema drift.
/// </summary>
public sealed class DatabaseConfigurationException(string message) : InvalidOperationException(message);
