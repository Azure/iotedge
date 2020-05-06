// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common.Certs
{
    public class IdCertificates
    {
        string[] GetFileLocation(string deviceId)
        {
            return new[]
            {
                FixedPaths.DeviceIdentityCert.Cert(deviceId),
                FixedPaths.DeviceIdentityCert.Key(deviceId)
            };
        }

        public string CertificatePath { get; protected set; }

        public string KeyPath { get; protected set; }

        protected IdCertificates()
        {
        }

        public IdCertificates(string deviceId, string scriptPath)
        {
            var location = this.GetFileLocation(deviceId);
            var files = OsPlatform.NormalizeFiles(location, scriptPath);
            this.CertificatePath = files[0];
            this.KeyPath = files[1];
        }
    }
}