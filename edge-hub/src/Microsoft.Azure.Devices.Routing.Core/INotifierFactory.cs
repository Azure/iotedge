// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public interface INotifierFactory
    {
        INotifier Create(string hubName);
    }
}
