// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MessageAddressConversionConfiguration
    {
        public IList<string> InboundTemplates { get; }
        public IList<string> OutboundTemplates { get; }

        public MessageAddressConversionConfiguration() :
           this(new List<string>(), new List<string>())
        {
        }

        public MessageAddressConversionConfiguration(IList<string> inboundTemplates, IList<string> outboundTemplates)
        {
            Preconditions.CheckNotNull(inboundTemplates, nameof(inboundTemplates));
            Preconditions.CheckNotNull(outboundTemplates, nameof(outboundTemplates));

            this.InboundTemplates = inboundTemplates;
            this.OutboundTemplates = outboundTemplates;
        }
    }
}
