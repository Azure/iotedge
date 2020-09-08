// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Identity
{
    public interface ISharedKeyCredentials : IClientCredentials
    {
        string ConnectionString { get; }
    }
}
