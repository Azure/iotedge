// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using Microsoft.Azure.Devices.Edge.Test.Common.Certs;

    public interface IDaemonConfiguration
    {
        void AddHttpsProxy(Uri proxy);
        void RemoveCertificates();
        void SetCertificates(CaCertificates certs);
        void SetDeviceConnectionString(string connectionString);
        void SetDeviceHostname(string value);
        void SetDeviceManualX509(string hubhostname, string deviceId, string identityCertPath, string identity_pk_path);
        void SetDpsSymmetricKey(string idScope, string registrationId, string deviceKey);
        void SetDpsX509(string idScope, string registrationId, IdCertificates cert);
        void Update();
    }
}
