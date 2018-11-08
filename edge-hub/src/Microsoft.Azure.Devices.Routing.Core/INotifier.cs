// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a mechanism to be notified of changes to a particular hub.
    /// </summary>
    public interface INotifier : IDisposable
    {
        /// <summary>
        /// Gets name of the iot hub connected to this notifier. Notifiers are created per hub.
        /// </summary>
        string IotHubName { get; }

        /// <summary>
        /// Cleanup resources associated with any subscriptions on this notifier.
        /// </summary>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task</returns>
        Task CloseAsync(CancellationToken token);

        /// <summary>
        /// Subscribe to changes for this hub.
        /// </summary>
        /// <param name="key">
        /// Secondary key to use for subscription (in addition to hub name).
        /// For instance, this is set as the partition id in the service fabric replica.
        /// </param>
        /// <param name="onChange">OnChange delegate</param>
        /// <param name="onDelete">OnDelete delegate</param>
        /// <param name="token">Cancellation token</param>
        /// <returns>Task</returns>
        Task SubscribeAsync(string key, Func<string, Task> onChange, Func<string, Task> onDelete, CancellationToken token);
    }
}
