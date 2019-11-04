// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public interface IMessageConverter<T>
    {
        T Convert(IMessage message);
        T Convert(IEnumerable<IMessage> messages);

        T Convert(Client.Message message);
        T Convert(IEnumerable<Client.Message> messages);
    }
}
