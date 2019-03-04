// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public class NullNotifierFactory : INotifierFactory
    {
        public static NullNotifierFactory Instance { get; } = new NullNotifierFactory();

        public INotifier Create(string hubName) => NullNotifier.Instance;
    }
}
