using InsanityGaming.ModSharp.PlaytimeTracker.Shared;
using InsanityGaming.PlaytimeTracker.Commands;
using InsanityGaming.PlaytimeTracker.Data;
using InsanityGaming.PlaytimeTracker.Interfaces;
using InsanityGaming.PlaytimeTracker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Sharp.Extensions.CommandManager;
using Sharp.Extensions.GameEventManager;
using Sharp.Shared;
using Sharp.Shared.Abstractions;
using System;
using System.IO;
using System.Linq;

namespace InsanityGaming.PlaytimeTracker;

public sealed class PlaytimeTracker : IModSharpModule
{
    public string DisplayName   => "PlaytimeTracker";
    public string DisplayAuthor => "InsanityGaming Dev Team";

    private readonly ILogger<PlaytimeTracker> _logger;
    private readonly InterfaceBridge _bridge;
    private readonly ServiceProvider _serviceProvider;

    public PlaytimeTracker(
        ISharedSystem sharedSystem,
        string dllPath,
        string sharpPath,
        Version version,
        IConfiguration coreConfiguration,
        bool hotReload)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(dllPath, "config.json"), false, false)
            .Build();

        var services = new ServiceCollection();

        _bridge  = new InterfaceBridge(dllPath, sharpPath, version, sharedSystem);
        _logger  = sharedSystem.GetLoggerFactory().CreateLogger<PlaytimeTracker>();

        services.AddSingleton(_bridge);
        services.AddSingleton(sharedSystem);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(sharedSystem.GetLoggerFactory());
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

        services.AddCommandManager(sharedSystem);
        services.AddGameEventManager();

        services.AddSingleton<Database>();
        services.AddSingleton<PlaytimeRepository>();

        services.AddSingleton<ServerRegistry>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<PlaytimeTrackerService>();
        services.AddSingleton<PlaytimeCommands>();
        services.AddSingleton<IPlaytimeTracker>(sp => sp.GetRequiredService<PlaytimeTrackerService>());
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<SessionManager>());
        services.AddSingleton<IModule>(sp => sp.GetRequiredService<PlaytimeCommands>());

        _serviceProvider = services.BuildServiceProvider();
    }

    public bool Init()
    {
        _serviceProvider.LoadAllSharpExtensions();
        _serviceProvider.GetRequiredService<IGameEventManager>();

        var db       = _serviceProvider.GetRequiredService<Database>();
        var registry = _serviceProvider.GetRequiredService<ServerRegistry>();
        var tracker  = _serviceProvider.GetRequiredService<PlaytimeTrackerService>();

        db.EnsureSchemaAsync().GetAwaiter().GetResult();
        registry.RegisterAsync().GetAwaiter().GetResult();

        _bridge.CommandManager = _serviceProvider.GetRequiredService<ICommandManager>();

        _bridge.SharpModuleManager.RegisterSharpModuleInterface<IPlaytimeTracker>(
            this, IPlaytimeTracker.Identity, tracker);

        foreach (IModule service in _serviceProvider.GetServices<IModule>())
        {
            if (!service.Init())
                _logger.LogError("PlaytimeTracker module {Name} failed to initialize.", service.GetType().Name);
        }

        return true;
    }

    public void Shutdown()
    {
        _serviceProvider.ShutdownAllSharpExtensions();
        foreach (IModule service in _serviceProvider.GetServices<IModule>())
            service.Shutdown();
    }

    public void PostInit() { }
    public void OnAllModulesLoaded() { }
    public void OnLibraryConnected(string name) { }
    public void OnLibraryDisconnect(string name) { }
}
