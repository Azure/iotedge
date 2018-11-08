// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.MessageSources
{
    public class TelemetryMessageSource : BaseMessageSource
    {
        TelemetryMessageSource()
            : base("/messages")
        {
        }

        public static TelemetryMessageSource Instance { get; } = new TelemetryMessageSource();
    }
}
