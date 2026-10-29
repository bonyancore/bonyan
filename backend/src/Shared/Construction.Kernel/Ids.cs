namespace Construction.Kernel;

/// <summary>Id generation. All ids are UUID v7 (time-ordered), matching the spec.</summary>
public static class Ids
{
    public static Guid New() => Guid.CreateVersion7();
}
