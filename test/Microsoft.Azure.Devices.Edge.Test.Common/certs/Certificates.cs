// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    public class Certificates
    {
        private string[] GetFileLocation(string deviceId)
        {
            return new[]
            {
                $"certs/iot-device-{deviceId}-full-chain.cert.pem",
                $"private/iot-device-{deviceId}.key.pem"
            };
        }

        public string CertificatePath { get; protected set; }

        public string KeyPath { get; protected set; }

        public Certificates(string deviceId, string scriptPath)
        {
            var location = this.GetFileLocation(deviceId);
            var files = OsPlatform.NormalizeFiles(location, scriptPath);
            this.CertificatePath = files[0];
            this.KeyPath = files[1];
        }
    }
}