using System.Collections.Generic;
using SoClover.Domain;
using SoClover.RealTime;

namespace SoClover.Tests.Helpers;

internal sealed class FakeConnectionTracker : IConnectionTracker
{
    private readonly HashSet<PlayerId> _connected;

    public FakeConnectionTracker(IEnumerable<PlayerId> connected) => _connected = new(connected);

    public bool IsPlayerConnected(PlayerId playerId) => _connected.Contains(playerId);
}
