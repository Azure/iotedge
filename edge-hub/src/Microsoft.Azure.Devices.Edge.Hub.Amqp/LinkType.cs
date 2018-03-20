// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Amqp
{
    public enum LinkType
    {
        Cbs,
        Events,
        C2D,
        ModuleMessages,
        TwinReceiving,
        TwinSending,
        MethodReceiving,
        MethodSending,
    }
}
