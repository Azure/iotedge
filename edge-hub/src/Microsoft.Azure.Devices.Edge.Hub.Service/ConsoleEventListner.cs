// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Service
{
    using System.Diagnostics.Tracing;
    using System.Text;

    class ConsoleEventListner : EventListener
    {
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var payload = new StringBuilder();
            foreach (object p in eventData.Payload)
            {
                payload.Append("[" + p + "]");
            }
            System.Console.WriteLine("EventId: {0}, Level: {1}, Message: {2}, Payload: {3} , EventName: {4}", eventData.EventId, eventData.Level, eventData.Message, payload.ToString(), eventData.EventName);
        }
    }
}
