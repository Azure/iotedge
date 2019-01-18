// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class AddToStoreCommand<T> : ICommand
    {
        readonly IEntityStore<string, T> store;
        readonly string key;
        readonly T value;

        public AddToStoreCommand(IEntityStore<string, T> store, string key, T value)
        {
            this.store = Preconditions.CheckNotNull(store, nameof(store));
            this.key = Preconditions.CheckNonWhiteSpace(key, nameof(key));
            this.value = Preconditions.CheckNotNull(value, nameof(value));
            this.Id = $"AddToStore:{this.key}:{JsonConvert.SerializeObject(this.value)}";
        }

        public string Id { get; }

        public Task ExecuteAsync(CancellationToken token) => this.store.Put(this.key, this.value);

        public string Show() => $"Saving {this.key} to store";

        public Task UndoAsync(CancellationToken token) => this.store.Remove(this.key);
    }
}
