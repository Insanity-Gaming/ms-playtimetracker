using Microsoft.Extensions.Logging;
using Sharp.Extensions.CommandManager;
using Sharp.Shared;
using Sharp.Shared.Abstractions;
using Sharp.Shared.Managers;
using System;
using System.IO;

namespace InsanityGaming.PlaytimeTracker;

public sealed class InterfaceBridge
{
    public string SharpPath  { get; }
    public string RootPath   { get; }
    public string DllPath    { get; }
    public string DataPath   { get; }
    public string ConfigPath { get; }

    public Version Version { get; }

    public IEventManager        EventManager       { get; }
    public IEntityManager       EntityManager      { get; }
    public IClientManager       ClientManager      { get; }
    public IConVarManager       ConVarManager      { get; }
    public IHookManager         HookManager        { get; }
    public IFileManager         FileManager        { get; }
    public IModSharp             ModSharp           { get; }
    public IGameData            GameData           { get; }
    public ILoggerFactory       LoggerFactory      { get; }
    public ISharedSystem        SharedSystem       { get; }
    public ISharpModuleManager  SharpModuleManager { get; }
    public ICommandManager?     CommandManager     { get; set; }

    public InterfaceBridge(string dllPath, string sharpPath, Version version, ISharedSystem sharedSystem)
    {
        DllPath    = dllPath;
        SharpPath  = sharpPath;
        Version    = version;
        SharedSystem = sharedSystem;

        RootPath   = Path.Combine(sharpPath, "..");
        DataPath   = Path.Combine(sharpPath, "data");
        ConfigPath = Path.Combine(sharpPath, "configs");

        Directory.CreateDirectory(DataPath);
        Directory.CreateDirectory(ConfigPath);

        LoggerFactory      = sharedSystem.GetLoggerFactory();
        ModSharp           = sharedSystem.GetModSharp();
        EventManager       = sharedSystem.GetEventManager();
        EntityManager      = sharedSystem.GetEntityManager();
        ClientManager      = sharedSystem.GetClientManager();
        ConVarManager      = sharedSystem.GetConVarManager();
        HookManager        = sharedSystem.GetHookManager();
        FileManager        = sharedSystem.GetFileManager();
        GameData           = sharedSystem.GetModSharp().GetGameData();
        SharpModuleManager = sharedSystem.GetSharpModuleManager();
    }
}
