// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Mqtt
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util;

    public class MessageAddressConversionConfiguration
    {
        public IList<string> InboundTemplates { get; }
        public IDictionary<string, string> OutboundTemplates { get; }

        public MessageAddressConversionConfiguration() :
           this(new List<string>(), new Dictionary<string, string>())
        {
        }

        public MessageAddressConversionConfiguration(IList<string> inboundTemplates, IDictionary<string, string> outboundTemplates)
        {
            Preconditions.CheckNotNull(inboundTemplates, nameof(inboundTemplates));
            Preconditions.CheckNotNull(outboundTemplates, nameof(outboundTemplates));

            this.InboundTemplates = inboundTemplates;
            this.OutboundTemplates = outboundTemplates;
        }
    }
}
