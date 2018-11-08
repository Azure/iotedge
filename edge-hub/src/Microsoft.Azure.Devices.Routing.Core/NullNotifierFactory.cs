// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    public class NullNotifierFactory : INotifierFactory
    {
        public static NullNotifierFactory Instance { get; } = new NullNotifierFactory();

        public INotifier Create(string hubName) => NullNotifier.Instance;
    }
}
