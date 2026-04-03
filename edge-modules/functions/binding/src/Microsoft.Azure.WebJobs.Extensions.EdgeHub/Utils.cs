// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.WebJobs.Extensions.EdgeHub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Client;

    static class Utils
    {
        public static TelemetryMessage GetMessageCopy(byte[] payload, TelemetryMessage message)
        {
            var copy = new TelemetryMessage(payload);

            foreach (KeyValuePair<string, string> kv in message.Properties)
            {
                copy.Properties.Add(kv.Key, kv.Value);
            }

            return copy;
        }

        public static bool HasTimeoutException(this Exception ex) =>
            ex != null &&
            (ex is TimeoutException || HasTimeoutException(ex.InnerException) ||
             (ex is AggregateException argEx && (argEx.InnerExceptions?.Select(e => HasTimeoutException(e)).Any(e => e) ?? false)));
    }
}
