namespace Lavalink4NET.Experiments.Receive.Server;

using System;
using System.Diagnostics.Metrics;

internal sealed class LavalinkMeterFactory : IMeterFactory
{
    public Meter Create(MeterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new(options);
    }

    public void Dispose()
    {
    }
}