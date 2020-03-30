// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using System.Linq;
    using System.Net;

    public class DeviceId
    {
        int count;

        static readonly Lazy<DeviceId> Default = new Lazy<DeviceId>(() => new DeviceId());

        public static DeviceId Current => Default.Value;

        public string BaseId { get; }

        protected DeviceId()
        {
            string hostname = string.Concat(Dns.GetHostName().Take(34)).TrimEnd('-');
            string timestamp = $"{DateTime.Now:yyMMdd'-'HHmmss'.'fff}";
            this.BaseId = $"e2e-{hostname}-{timestamp}";
            this.count = 0;
        }

        public string Generate()
        {
            // Max length of returned string is 60 chars. Some tests use the device ID as the basis
            // for the Common Name (CN) of an x509 certificate, and the CN limit is 64 chars, so
            // this gives tests the opportunity to safely append up to 4 additional characters.
            ++this.count;
            if (this.count > 999)
            {
                throw new InvalidOperationException("Device ID counter exceeded 3 digits.");
            }

            return $"{this.BaseId}-{this.count:d3}";
        }
    }
}
