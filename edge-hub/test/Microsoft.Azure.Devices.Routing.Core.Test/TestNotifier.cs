// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Test
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class TestNotifier : INotifier
    {
        Func<string, Task> changeCallback;
        Func<string, Task> deleteCallback;

        public string IotHubName => "test";

        public Task Change(string hubName) => this.changeCallback?.Invoke(hubName) ?? TaskEx.Done;

        public Task CloseAsync(CancellationToken token) => TaskEx.Done;

        public Task Delete(string hubName) => this.deleteCallback?.Invoke(hubName) ?? TaskEx.Done;

        public void Dispose()
        {
        }

        public Task SubscribeAsync(string key, Func<string, Task> onChange, Func<string, Task> onDelete, CancellationToken token)
        {
            this.changeCallback = onChange;
            this.deleteCallback = onDelete;
            return TaskEx.Done;
        }
    }
}
