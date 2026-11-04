namespace MuscleCuties.DbMaintenance;

/// <summary>
/// Advisory lock identity for serialized deployments. The release wrapper acquires this lock before
/// starting the release engine and fails closed if ownership cannot be established or verified.
/// </summary>
public static class DeploymentAdvisoryLock
{
    public const int ClassId = 0x4D435343;
    public const int KeyId = 0x53434845;
}
