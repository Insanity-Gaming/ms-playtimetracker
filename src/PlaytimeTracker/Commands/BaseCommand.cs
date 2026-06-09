using InsanityGaming.PlaytimeTracker.Interfaces;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace InsanityGaming.PlaytimeTracker.Commands;

internal abstract class BaseCommand : IModule
{
    protected readonly InterfaceBridge Bridge;
    protected readonly ILogger Logger;

    private readonly Dictionary<string, Action<IGameClient?, StringCommand>> _callbacks = new();

    protected BaseCommand(InterfaceBridge bridge, ILogger logger)
    {
        Bridge = bridge;
        Logger = logger;
    }

    protected void RegisterCommand(string command, Action<IGameClient?, StringCommand> callback, bool clientOnly = true)
    {
        if (Bridge.CommandManager is null)
        {
            Task.Run(async () =>
            {
                await Task.Delay(500);
                RegisterCommand(command, callback, clientOnly);
            });
            return;
        }

        Bridge.CommandManager.RegisterClientCommand(command, Execute);
        if (!clientOnly)
            Bridge.CommandManager.RegisterServerCommand(command, string.Empty, cmd => Execute(null, cmd));

        _callbacks[command.ToLower()] = callback;
    }

    private void Execute(IGameClient? client, StringCommand command)
    {
        var name = command.CommandName.ToLower();
        if (name.StartsWith("css_"))
            name = name[4..];

        if (!_callbacks.TryGetValue(name, out var callback))
        {
            Logger.LogError("Command {Name} not registered.", name);
            return;
        }

        callback.Invoke(client, command);
    }

    public abstract bool Init();

    public virtual void Shutdown()
    {
        _callbacks.Clear();
    }
}
