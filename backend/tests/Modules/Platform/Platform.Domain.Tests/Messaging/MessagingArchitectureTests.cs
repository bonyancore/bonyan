using Construction.Messaging;
using Xunit;

namespace Construction.Platform.Domain.Tests.Messaging;

public sealed class MessagingArchitectureTests
{
    [Fact]
    public void ConstructionMessaging_ReferencesOnlyKernelAndSystemLibraries()
    {
        var messagingAssembly = typeof(IEventEnvelope).Assembly;
        var referencedAssemblies = messagingAssembly.GetReferencedAssemblies();

        var nonSystemReferences = referencedAssemblies
            .Where(r => r.Name is not null
                        && !r.Name.StartsWith("System", StringComparison.OrdinalIgnoreCase)
                        && !r.Name.StartsWith("Microsoft.NET", StringComparison.OrdinalIgnoreCase)
                        && !r.Name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase)
                        && !r.Name.Equals("netstandard", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Name!)
            .ToList();

        // Must reference only Construction.Kernel
        Assert.Single(nonSystemReferences);
        Assert.Equal("Construction.Kernel", nonSystemReferences[0]);
    }

    [Fact]
    public void ConstructionMessaging_DoesNotReferencePersistenceOrEntityFramework()
    {
        var messagingAssembly = typeof(IEventEnvelope).Assembly;
        var referencedAssemblies = messagingAssembly.GetReferencedAssemblies();

        var forbiddenReferences = referencedAssemblies
            .Where(r => r.Name is not null && (
                r.Name.Contains("Persistence", StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains("EntityFramework", StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains("Platform", StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains("Cash", StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.Name!)
            .ToList();

        Assert.Empty(forbiddenReferences);
    }
}
