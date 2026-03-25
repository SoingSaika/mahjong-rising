using System;
using System.Collections.Generic;

namespace MahjongRising.code.Player.Actions;

public sealed class PlayerActionHandlerRegistry
{
    private readonly Dictionary<Type, IPlayerActionHandlerAdapter> _handlers = new();

    public void Register<TAction>(IPlayerActionHandler<TAction> handler)
        where TAction : PlayerAction
    {
        _handlers[typeof(TAction)] = new PlayerActionHandlerAdapter<TAction>(handler);
    }

    public bool TryGetHandler(Type actionType, out IPlayerActionHandlerAdapter? handler)
    {
        return _handlers.TryGetValue(actionType, out handler);
    }
}