// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class RoutingIdBuilder
    {
        const string Delimiter = ".";
        static readonly string[] DelimiterArray = { Delimiter };

        readonly string cachedId;

        public string IotHubName { get; }

        public long RouterNumber { get; }

        public Option<string> EndpointId { get; }

        public RoutingIdBuilder(string iotHubName, long routerNumber)
            : this(iotHubName, routerNumber, Option.None<string>())
        {

        }

        public RoutingIdBuilder(string iotHubName, long routerNumber, Option<string> endpointId)
        {
            this.IotHubName = Preconditions.CheckNotNull(iotHubName);
            Preconditions.CheckArgument(!iotHubName.Contains(Delimiter));
            Preconditions.CheckArgument(!endpointId.GetOrElse(string.Empty).Contains(Delimiter));

            this.RouterNumber = routerNumber;
            this.EndpointId = endpointId;

            this.cachedId = string.Join(Delimiter, iotHubName, routerNumber.ToString(CultureInfo.InvariantCulture));
            if (endpointId.HasValue)
            {
                this.cachedId = string.Join(Delimiter, this.cachedId, endpointId.OrDefault());
            }
        }

        public static Option<RoutingIdBuilder> Parse(string id)
        {
            string[] tokens = id?.Split(DelimiterArray, StringSplitOptions.RemoveEmptyEntries);
            if (tokens == null)
            {
                return Option.None<RoutingIdBuilder>();
            }

            Option<string> endpointId = Option.None<string>();

            switch (tokens.Length)
            {
                case 3:
                    endpointId = Option.Some(tokens[2]);
                    goto case 2;

                case 2:

                    string iotHubName = tokens[0];
                    if (long.TryParse(tokens[1], out long routerNumber))
                    {
                        return Option.Some(new RoutingIdBuilder(iotHubName, routerNumber, endpointId));
                    }
                    break;
            }

            return Option.None<RoutingIdBuilder>();
        }

        public string GetId() => this.cachedId;

        public override string ToString() => this.GetId();

        public override int GetHashCode() => this.cachedId.GetHashCode();

        public override bool Equals(object obj)
        {
            var other = obj as RoutingIdBuilder;
            return other?.cachedId == this.cachedId;
        }
    }
}
