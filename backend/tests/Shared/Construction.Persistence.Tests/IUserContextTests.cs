using Construction.Kernel.Users;
using Construction.Kernel;
using Construction.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Construction.Persistence.Tests;

public class IUserContextTests
{
    [Fact]
    public void NullUserContext_IsAuthenticated_False_UserId_Null()
    {
        var context = new NullUserContext();
        Assert.False(context.IsAuthenticated);
        Assert.Null(context.UserId);
    }

    [Fact]
    public void ServiceRegistration_TryAddScoped_ReplacedByReplace()
    {
        var services = new ServiceCollection();

        // 1. PLT-103 registers default
        services.TryAddScoped<IUserContext, NullUserContext>();

        using (var sp1 = services.BuildServiceProvider())
        {
            var userCtx = sp1.GetRequiredService<IUserContext>();
            Assert.IsType<NullUserContext>(userCtx);
        }

        // 2. PLT-106 replaces with authenticated implementation
        services.Replace(ServiceDescriptor.Scoped<IUserContext, FakeHttpUserContext>());

        using (var sp2 = services.BuildServiceProvider())
        {
            var userCtx = sp2.GetRequiredService<IUserContext>();
            Assert.IsType<FakeHttpUserContext>(userCtx);

            var all = sp2.GetServices<IUserContext>();
            Assert.Single(all);
        }
    }

    [Fact]
    public void AddPersistenceInfrastructure_TryAddSingleton_DoesNotOverrideExistingClock()
    {
        var services = new ServiceCollection();

        // Pre-register custom clock
        var customClock = new CustomTestClock();
        services.AddSingleton<IClock>(customClock);

        // Call persistence registration
        services.AddPersistenceInfrastructure();

        using var sp = services.BuildServiceProvider();
        var resolvedClock = sp.GetRequiredService<IClock>();

        Assert.Same(customClock, resolvedClock);
    }

    private sealed class CustomTestClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch;
    }

    private sealed class FakeHttpUserContext : IUserContext
    {
        public bool IsAuthenticated => true;
        public Guid? UserId { get; } = Guid.NewGuid();
    }
}
