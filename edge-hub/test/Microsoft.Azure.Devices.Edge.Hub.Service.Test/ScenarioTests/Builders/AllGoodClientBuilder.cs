// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Edge.Hub.CloudProxy;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity;

    public class AllGoodClientBuilder : IClientBuilder
    {
        private Action closeAction;
        private Action openAction;
        private Action<IReadOnlyCollection<Client.Message>> sendEventAction;

        public AllGoodClientBuilder WithCloseAction(Action action)
        {
            this.closeAction = Delegate.Combine(this.closeAction, action) as Action;
            return this;
        }

        public AllGoodClientBuilder WithOpenAction(Action action)
        {
            this.openAction = Delegate.Combine(this.openAction, action) as Action;
            return this;
        }

        public AllGoodClientBuilder WithSendEventAction(Action<IReadOnlyCollection<Client.Message>> action)
        {
            this.sendEventAction = Delegate.Combine(this.sendEventAction, action) as Action<IReadOnlyCollection<Client.Message>>;
            return this;
        }

        public virtual IClient Build(IIdentity identity)
        {
            var result = new AllGoodClient();
            this.AddHooks(result);

            return result;
        }

        protected void AddHooks(IClientHooks clientHooks)
        {
            if (this.closeAction != null)
            {
                clientHooks.AddCloseAction(this.closeAction);
            }

            if (this.openAction != null)
            {
                clientHooks.AddOpenAction(this.openAction);
            }

            if (this.sendEventAction != null)
            {
                clientHooks.AddSendEventAction(this.sendEventAction);
            }
        }
    }
}
