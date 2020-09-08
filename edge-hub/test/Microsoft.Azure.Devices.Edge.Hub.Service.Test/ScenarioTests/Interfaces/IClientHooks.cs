// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Shared;

    public interface IClientHooks
    {
        void AddSendEventAction(Action<IReadOnlyCollection<Client.Message>> action);
        void AddOpenAction(Action action);
        void AddCloseAction(Action action);

        Task<bool> UpdateDesiredProperty(TwinCollection desiredProperties);
    }
}
