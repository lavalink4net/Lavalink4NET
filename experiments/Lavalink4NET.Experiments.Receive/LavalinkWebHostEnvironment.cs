namespace Lavalink4NET.Experiments.Receive;

using System;
using Microsoft.Extensions.FileProviders;

internal sealed class LavalinkWebHostEnvironment : IHostEnvironment
{
    public string EnvironmentName
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public string ApplicationName
    {
        get => "Lavalink";
        set => throw new NotImplementedException();
    }

    public string ContentRootPath
    {
        get => Directory.GetCurrentDirectory();
        set => throw new NotImplementedException();
    }

    public IFileProvider ContentRootFileProvider
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
}
