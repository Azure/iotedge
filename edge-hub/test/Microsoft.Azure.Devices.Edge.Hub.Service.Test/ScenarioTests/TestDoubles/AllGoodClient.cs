// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Shared;

    public class AllGoodClient : IClient, IClientHooks
    {
        private Random rnd = new Random(346543);
        private MethodCallback methodHandler;

        private Action closeAction;
        private Action openAction;
        private Action<IReadOnlyCollection<Message>> sendEventAction;

        private DesiredPropertyUpdateCallback desiredPropertyUpdateCallback;
        private object desiredPropertyUpdateUserContext;

        private ConnectionStatusChangesHandler connectionStatusChangesHandler;

        private bool isActive = true;

        public AllGoodClient()
        {
        }

        public virtual bool IsActive => Volatile.Read(ref this.isActive);
        public virtual Task AbandonAsync(string messageId) => Task.CompletedTask;

        public virtual Task CompleteAsync(string messageId) => Task.CompletedTask;
        public virtual void Dispose()
        {
        }

        public Task<Twin> GetTwinAsync() => Task.FromResult(new Twin());

        public Task OpenAsync()
        {
            this.openAction?.Invoke();
            Volatile.Write(ref this.isActive, true);

            this.connectionStatusChangesHandler?.Invoke(ConnectionStatus.Connected, ConnectionStatusChangeReason.Connection_Ok);
            return Task.CompletedTask;
        }

        public virtual Task CloseAsync()
        {
            Volatile.Write(ref this.isActive, false);
            this.closeAction?.Invoke();

            this.connectionStatusChangesHandler?.Invoke(ConnectionStatus.Disconnected, ConnectionStatusChangeReason.Client_Close);
            return Task.CompletedTask;
        }

        // this should be configurable once needed. leaving here just for quick example
        public async Task<Message> ReceiveAsync(TimeSpan receiveMessageTimeout)
        {
            var delay = 0;
            var odds = 0.0;

            lock (this.rnd)
            {
                delay = this.rnd.Next(2000, 10000);
                odds = this.rnd.Next();
            }

            await Task.Delay(delay);

            return odds > 0.2 ? default(Message) : GenerateMessage();

            Message GenerateMessage()
            {
                var result = new Message();
                var resultSetup = result.AsPrivateAccessible();

                resultSetup.LockToken = Guid.NewGuid().ToString();
                return result;
            }
        }

        public Task RejectAsync(string messageId) => Task.CompletedTask;

        public virtual Task SendEventAsync(Message message)
        {
            this.sendEventAction?.Invoke(new[] { message });
            return Task.CompletedTask;
        }

        public virtual Task SendEventBatchAsync(IEnumerable<Message> messages)
        {
            this.sendEventAction?.Invoke(messages.ToArray());
            return Task.CompletedTask;
        }

        public void SetConnectionStatusChangedHandler(ConnectionStatusChangesHandler handler)
        {
            this.connectionStatusChangesHandler = handler;
        }

        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertyUpdates, object userContext)
        {
            this.desiredPropertyUpdateCallback = onDesiredPropertyUpdates;
            this.desiredPropertyUpdateUserContext = userContext;

            return Task.CompletedTask;
        }

        // Hook for direct methods
        public Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext)
        {
            this.methodHandler = methodHandler;
            this.StartPeriodicalDirectMethodCaller();
            return Task.CompletedTask;
        }

        public void SetOperationTimeoutInMilliseconds(uint defaultOperationTimeoutMilliseconds)
        {
        }

        public void SetProductInfo(string productInfo)
        {
        }

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => Task.CompletedTask;

        public bool HasUpdateDesiredPropertySubscription => this.desiredPropertyUpdateCallback != null;
        public async Task<bool> UpdateDesiredProperty(TwinCollection desiredProperties)
        {
            if (!this.HasUpdateDesiredPropertySubscription)
            {
                return false;
            }

            if (!this.IsActive)
            {
               return false;
            }

            await this.desiredPropertyUpdateCallback(desiredProperties, this.desiredPropertyUpdateUserContext);
            return true;
        }

        // TODO: this must be made configurable once it is needed. leaving here just for quick example
        private void StartPeriodicalDirectMethodCaller()
        {
            var random = new Random(84634522);

            Task.Factory.StartNew(
                async () =>
                {
                    for (var i = 0; i < 10; i++)
                    {
                        await Task.Delay(random.Next(3000, 6000));
                        await this.methodHandler(new MethodRequest("test-method-call"), null);
                    }
                });
        }

        void IClientHooks.AddCloseAction(Action action) => this.ChainAction(ref this.closeAction, action);
        void IClientHooks.AddOpenAction(Action action) => this.ChainAction(ref this.openAction, action);
        void IClientHooks.AddSendEventAction(Action<IReadOnlyCollection<Message>> action) => this.ChainAction(ref this.sendEventAction, action);

        private void ChainAction(ref Action originalAction, Action newAction)
        {
            originalAction = Delegate.Combine(originalAction, newAction) as Action;
        }

        private void ChainAction(ref Action<IReadOnlyCollection<Message>> originalAction, Action<IReadOnlyCollection<Message>> newAction)
        {
            originalAction = Delegate.Combine(originalAction, newAction) as Action<IReadOnlyCollection<Message>>;
        }
    }
}
