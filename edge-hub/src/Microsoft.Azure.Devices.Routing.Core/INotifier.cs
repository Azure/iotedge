// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

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
        /// Name of the iot hub connected to this notifier. Notifiers are created per hub.
        /// </summary>
        string IotHubName { get; }

        /// <summary>
        /// Subscribe to changes for this hub.
        /// </summary>
        /// <param name="key">
        /// Secondary key to use for subscription (in addition to hub name).
        /// For instance, this is set as the partition id in the service fabric replica.
        /// </param>
        /// <param name="onChange"></param>
        /// <param name="onDelete"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task SubscribeAsync(string key, Func<string, Task> onChange, Func<string, Task> onDelete, CancellationToken token);

        /// <summary>
        /// Cleanup resources associated with any subscriptions on this notifier.
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        Task CloseAsync(CancellationToken token);
    }
}