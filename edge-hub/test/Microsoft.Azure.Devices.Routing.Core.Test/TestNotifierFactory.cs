// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    public class TestNotifierFactory : INotifierFactory
    {
        readonly TestNotifier notifier;

        public TestNotifierFactory(TestNotifier notifier)
        {
            this.notifier = notifier;
        }

        public INotifier Create(string hubName) => this.notifier;
    }
}
