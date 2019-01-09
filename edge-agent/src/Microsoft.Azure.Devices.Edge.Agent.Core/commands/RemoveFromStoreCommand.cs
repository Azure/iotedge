// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;

    public class RemoveFromStoreCommand<T> : ICommand
    {
        readonly IEntityStore<string, T> store;
        readonly string key;

        public RemoveFromStoreCommand(IEntityStore<string, T> store, string key)
        {
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.key = Preconditions.CheckNonWhiteSpace(key, nameof(key));
            this.Id = $"RemoveFromStore:{this.key}";
        }

        public string Id { get; }

        public Task ExecuteAsync(CancellationToken token) => this.store.Remove(this.key);

        public string Show() => $"Saving {this.key} to store";

        // TODO: Consider caching previous value, so that undo can add it back.
        public Task UndoAsync(CancellationToken token) => Task.CompletedTask;
    }
}
