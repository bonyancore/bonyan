using Construction.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Construction.Modules.Tests;

public class ModuleRegistryTests
{
    private readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

    [Fact]
    public void AddModule_RegistersModuleAndConfiguresServices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddModule<TestModuleA>(_emptyConfig);
        var provider = services.BuildServiceProvider();

        // Assert
        var module = provider.GetService<IModule>();
        Assert.NotNull(module);
        Assert.Equal("module_a", module.Code);
        Assert.Single(module.Permissions);
        Assert.Equal("module_a.read", module.Permissions.First().Code);

        // Verify module type registration
        var typedModule = provider.GetService<TestModuleA>();
        Assert.NotNull(typedModule);
        Assert.Same(module, typedModule);

        // Verify module services were added
        var serviceA = provider.GetService<ServiceA>();
        Assert.NotNull(serviceA);
    }

    [Fact]
    public void AddModule_WhenCalledMultipleTimes_PreventsDuplicateRegistration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddModule<TestModuleA>(_emptyConfig);
        services.AddModule<TestModuleA>(_emptyConfig);
        var provider = services.BuildServiceProvider();

        // Assert
        var modules = provider.GetServices<IModule>().ToList();
        Assert.Single(modules);

        var typedModule = provider.GetRequiredService<TestModuleA>();
        Assert.Equal(1, typedModule.ServicesAddedCallCount);

        // Verify services added by module are not duplicated in DI
        var servicesCount = services.Count(d => d.ServiceType == typeof(ServiceA));
        Assert.Equal(1, servicesCount);
    }

    [Fact]
    public void AddModule_WithInstance_PreventsDuplicateRegistrationOfSameType()
    {
        // Arrange
        var services = new ServiceCollection();
        var instance1 = new TestModuleA();
        var instance2 = new TestModuleA();

        // Act
        services.AddModule(instance1, _emptyConfig);
        services.AddModule(instance2, _emptyConfig);
        var provider = services.BuildServiceProvider();

        // Assert
        var modules = provider.GetServices<IModule>().ToList();
        Assert.Single(modules);
        Assert.Same(instance1, modules[0]);
        Assert.Equal(1, instance1.ServicesAddedCallCount);
        Assert.Equal(0, instance2.ServicesAddedCallCount);
    }

    [Fact]
    public void AddModule_CrossOverloadCalls_PreventsDuplicateRegistration()
    {
        // Arrange
        var servicesGenericFirst = new ServiceCollection();
        var servicesInstanceFirst = new ServiceCollection();

        // Act 1: Generic first, then instance
        servicesGenericFirst.AddModule<TestModuleA>(_emptyConfig);
        servicesGenericFirst.AddModule(new TestModuleA(), _emptyConfig);
        var provider1 = servicesGenericFirst.BuildServiceProvider();

        // Act 2: Instance first, then generic
        var instance = new TestModuleA();
        servicesInstanceFirst.AddModule(instance, _emptyConfig);
        servicesInstanceFirst.AddModule<TestModuleA>(_emptyConfig);
        var provider2 = servicesInstanceFirst.BuildServiceProvider();

        // Assert 1
        var modules1 = provider1.GetServices<IModule>().ToList();
        Assert.Single(modules1);
        Assert.Equal(1, provider1.GetRequiredService<TestModuleA>().ServicesAddedCallCount);

        // Assert 2
        var modules2 = provider2.GetServices<IModule>().ToList();
        Assert.Single(modules2);
        Assert.Same(instance, modules2[0]);
        Assert.Equal(1, instance.ServicesAddedCallCount);
    }

    [Fact]
    public void AddModule_WhenModulePreRegisteredViaFactory_PreventsDuplicateRegistration()
    {
        // Arrange
        var services = new ServiceCollection();
        var preRegisteredModule = new TestModuleA();

        // Pre-register via factory
        services.AddSingleton<TestModuleA>(_ => preRegisteredModule);

        // Act
        services.AddModule<TestModuleA>(_emptyConfig);
        var provider = services.BuildServiceProvider();

        // Assert: module was detected as already registered, AddServices was not called again
        Assert.Equal(0, preRegisteredModule.ServicesAddedCallCount);
        var resolved = provider.GetRequiredService<TestModuleA>();
        Assert.Same(preRegisteredModule, resolved);
    }

    [Fact]
    public void AddModule_WithExistingKeyedServices_DoesNotThrowAndRegistersCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        // Add a keyed service (standard in modern .NET apps)
        services.AddKeyedSingleton<IModule>("custom_key", new TestModuleB());

        // Act
        services.AddModule<TestModuleA>(_emptyConfig);
        var provider = services.BuildServiceProvider();

        // Assert
        var moduleA = provider.GetService<TestModuleA>();
        Assert.NotNull(moduleA);
        Assert.Equal(1, moduleA.ServicesAddedCallCount);
    }

    [Fact]
    public void MapModuleEndpoints_ResolvesModulesFromSpecificServiceProvider_AndInvokesMapEndpoints()
    {
        // Arrange
        var servicesA = new ServiceCollection();
        servicesA.AddModule<TestModuleA>(_emptyConfig);
        var providerA = servicesA.BuildServiceProvider();

        var servicesB = new ServiceCollection();
        servicesB.AddModule<TestModuleB>(_emptyConfig);
        var providerB = servicesB.BuildServiceProvider();

        var routeBuilderA = new FakeEndpointRouteBuilder(providerA);
        var routeBuilderB = new FakeEndpointRouteBuilder(providerB);

        var moduleA = providerA.GetRequiredService<TestModuleA>();
        var moduleB = providerB.GetRequiredService<TestModuleB>();

        // Act 1: Map endpoints for routeBuilderA only
        routeBuilderA.MapModuleEndpoints();

        // Assert 1: Only Module A endpoints mapped
        Assert.True(moduleA.EndpointsMapped);
        Assert.False(moduleB.EndpointsMapped);

        // Act 2: Map endpoints for routeBuilderB
        routeBuilderB.MapModuleEndpoints();

        // Assert 2: Module B endpoints now mapped
        Assert.True(moduleB.EndpointsMapped);
    }

    [Fact]
    public void ModuleRegistration_IsIsolatedBetweenServiceProviders()
    {
        // Arrange
        var services1 = new ServiceCollection();
        var services2 = new ServiceCollection();
        var services3 = new ServiceCollection();

        // Act: register ModuleA in services1, ModuleB in services2, none in services3
        services1.AddModule<TestModuleA>(_emptyConfig);
        services2.AddModule<TestModuleB>(_emptyConfig);

        var provider1 = services1.BuildServiceProvider();
        var provider2 = services2.BuildServiceProvider();
        var provider3 = services3.BuildServiceProvider();

        // Assert: provider 1 has only Module A
        var modules1 = provider1.GetServices<IModule>().ToList();
        Assert.Single(modules1);
        Assert.IsType<TestModuleA>(modules1[0]);
        Assert.Null(provider1.GetService<TestModuleB>());

        // Assert: provider 2 has only Module B
        var modules2 = provider2.GetServices<IModule>().ToList();
        Assert.Single(modules2);
        Assert.IsType<TestModuleB>(modules2[0]);
        Assert.Null(provider2.GetService<TestModuleA>());

        // Assert: provider 3 has no modules
        var modules3 = provider3.GetServices<IModule>().ToList();
        Assert.Empty(modules3);
    }

    private sealed class TestModuleA : IModule
    {
        public string Code => "module_a";
        public int ServicesAddedCallCount { get; private set; }
        public bool EndpointsMapped { get; private set; }

        public IReadOnlyCollection<PermissionDefinition> Permissions =>
            [new("module_a.read", "Read permission for Module A")];

        public IReadOnlyCollection<MigrationInfo> Migrations => [];

        public void AddServices(IServiceCollection services, IConfiguration config)
        {
            ServicesAddedCallCount++;
            services.AddScoped<ServiceA>();
        }

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            EndpointsMapped = true;
        }
    }

    private sealed class TestModuleB : IModule
    {
        public string Code => "module_b";
        public bool EndpointsMapped { get; private set; }

        public IReadOnlyCollection<PermissionDefinition> Permissions => [];
        public IReadOnlyCollection<MigrationInfo> Migrations => [];

        public void AddServices(IServiceCollection services, IConfiguration config)
        {
            services.AddScoped<ServiceB>();
        }

        public void MapEndpoints(IEndpointRouteBuilder app)
        {
            EndpointsMapped = true;
        }
    }

    private sealed class ServiceA;
    private sealed class ServiceB;

    private sealed class FakeEndpointRouteBuilder(IServiceProvider serviceProvider) : IEndpointRouteBuilder
    {
        public IServiceProvider ServiceProvider { get; } = serviceProvider;
        public ICollection<EndpointDataSource> DataSources { get; } = [];

        public IApplicationBuilder CreateApplicationBuilder() =>
            throw new NotSupportedException();
    }
}
