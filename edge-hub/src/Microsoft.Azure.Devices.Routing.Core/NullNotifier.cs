// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public sealed class NullNotifier : INotifier
    {
        public static NullNotifier Instance { get; } = new NullNotifier();

        public string IotHubName { get; } = "<null>";

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;

        public void Dispose()
        {
        }

        public Task SubscribeAsync(string key, Func<string, Task> onChange, Func<string, Task> onDelete, CancellationToken token) => TaskEx.Done;
    }
}
