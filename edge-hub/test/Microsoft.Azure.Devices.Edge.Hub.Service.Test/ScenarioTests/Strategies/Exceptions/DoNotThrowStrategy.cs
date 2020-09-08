// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Service.Test.ScenarioTests
{
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Edge.Hub.Core;

    public class DoNotThrowStrategy : IMessageConverter<bool>
    {
        public DoNotThrowStrategy()
        {
        }

        public static DoNotThrowStrategy Create() => new DoNotThrowStrategy();
        public bool Convert(IMessage message) => false;
        public bool Convert(IEnumerable<IMessage> message) => false;
        public bool Convert(Client.Message message) => false;
        public bool Convert(IEnumerable<Client.Message> messages) => false;
    }
}
