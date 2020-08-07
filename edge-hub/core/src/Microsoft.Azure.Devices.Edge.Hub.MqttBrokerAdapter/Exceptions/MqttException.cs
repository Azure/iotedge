// Copyright (c) Microsoft. All rights reserved.

using System;

namespace Microsoft.Azure.Devices.Edge.Hub.MqttBrokerAdapter.Exceptions
{
    public class MqttException : Exception
    {
        public MqttException(bool isTemporary = false, string message = null, Exception cause = null) : base(message, cause)
        {
            IsTemporary = isTemporary;
        }

        public bool IsTemporary { get; }
    }
}
