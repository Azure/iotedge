// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    using System.IO;

    public sealed class FixedPaths
    {
        public sealed class DeviceIdentityCert
        {
            public static string Cert(string deviceId) => $"certs/iot-device-{deviceId}-full-chain.cert.pem";
            public static string Key(string deviceId) => $"private/iot-device-{deviceId}.key.pem";
        }

        public sealed class DeviceCaCert
        {
            public static string Cert(string deviceId) => $"certs/iot-edge-device-{deviceId}-full-chain.cert.pem";
            public static string Key(string deviceId) => $"private/iot-edge-device-{deviceId}.key.pem";
            public static string TrustCert = "certs/azure-iot-test-only.root.ca.cert.pem";
        }

        public sealed class RootCaCert
        {
            public const string Cert = "certs/azure-iot-test-only.root.ca.cert.pem";
            public const string Key = "private/azure-iot-test-only.root.ca.key.pem";
        }
    }
}
