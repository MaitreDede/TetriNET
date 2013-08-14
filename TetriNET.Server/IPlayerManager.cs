﻿using System.Collections.Generic;
using TetriNET.Common;

namespace TetriNET.Server
{
    public interface IPlayerManager
    {
        int Add(IPlayer player);
        bool Remove(IPlayer player);

        int MaxPlayers { get; }
        int PlayerCount { get; }

        IEnumerable<IPlayer> Players { get; }

        int GetId(IPlayer player);

        IPlayer this[string name] { get; }
        IPlayer this[int index] { get; }
        IPlayer this[ITetriNETCallback callback] { get; } // Callback property from IPlayer should only be used here
    }
}
