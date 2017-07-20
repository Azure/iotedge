// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.E2E.Test
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;

    public class MqttHeadFixture : ProtocolHeadFixture
    {
        readonly IList<string> routes = new List<string>() {
            "FROM /messages/events INTO $upstream",
            "FROM /messages/modules/senderA INTO BrokeredEndpoint(\"/modules/receiverA/inputs/input1\")",
            "FROM /messages/modules/senderB INTO BrokeredEndpoint(\"/modules/receiverA/inputs/input1\")",
            "FROM /messages/modules/sender1 INTO BrokeredEndpoint(\"/modules/receiver1/inputs/input1\")",
            "FROM /messages/modules/sender2 INTO BrokeredEndpoint(\"/modules/receiver2/inputs/input1\")",
            "FROM /messages/modules/sender3 INTO BrokeredEndpoint(\"/modules/receiver3/inputs/input1\")",
            "FROM /messages/modules/sender4 INTO BrokeredEndpoint(\"/modules/receiver4/inputs/input1\")",
            "FROM /messages/modules/sender5 INTO BrokeredEndpoint(\"/modules/receiver5/inputs/input1\")",
            "FROM /messages/modules/sender6 INTO BrokeredEndpoint(\"/modules/receiver6/inputs/input1\")",
            "FROM /messages/modules/sender7 INTO BrokeredEndpoint(\"/modules/receiver7/inputs/input1\")",
            "FROM /messages/modules/sender8 INTO BrokeredEndpoint(\"/modules/receiver8/inputs/input1\")",
            "FROM /messages/modules/sender9 INTO BrokeredEndpoint(\"/modules/receiver9/inputs/input1\")",
            "FROM /messages/modules/sender10 INTO BrokeredEndpoint(\"/modules/receiver10/inputs/input1\")"
        };

        public MqttHeadFixture()
        {
            bool.TryParse(ConfigHelper.TestConfig["StressTest_StartEdge"], out bool shouldStartEdge);
            if (shouldStartEdge)
            {
                this.StartMqttHead(this.routes, null).Wait();
            }
        }
    }
}
