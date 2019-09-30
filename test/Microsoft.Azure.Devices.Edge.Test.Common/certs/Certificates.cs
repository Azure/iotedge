// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    public class Certificate
    {
        private string[] GetFileLocation(string deviceId)
        {
            return new[]
            {
                $"certs/iot-device-{deviceId}-full-chain.cert.pem",
                $"private/iot-device-{deviceId}.key.pem"
            };
        }

        public string CertificatePath { get; }

        public string KeyPath { get; }

        public Certificate(string deviceId, string scriptPath)
        {
            var location = this.GetFileLocation(deviceId);
            var files = OsPlatform.NormalizeFiles(location, scriptPath);
            this.CertificatePath = files[0];
            this.KeyPath = files[1];
        }
    }
}