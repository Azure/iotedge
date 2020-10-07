// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core.Test.Config
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Config;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using Xunit;
    public class TwinConfigSourceTest
    {
        [Theory]
        [MemberData(nameof(GetTwinForVerifyingSignatures))]
        public void ValidateTwinSignature(string type, string schemaVersion, string algo, string cert1, string cert2, string signature)
        {
            var desiredProperties = new
            {
                routes = new
                {
                    route = "FROM /messages/* INTO $upstream",
                    SimulatedTemperatureSensorToIoTHub = "FROM /messages/modules/SimulatedTemperatureSensor/* INTO $upstream"
                },
                schemaVersion,
                storeAndForwardConfiguration = new
                {
                    timeToLiveSecs = 7200
                },
                integrity = new
                {
                    header = new
                    {
                        version = "1",
                        cert1,
                        cert2,
                    },
                    signature = new
                    {
                        bytes = signature,
                        algorithm = algo
                    }
                },
                version = "10"
            };

            TwinCollection twinDesiredProperties = new TwinCollection(JsonConvert.SerializeObject(desiredProperties));
            if (type == "good")
            {
                Assert.True(TwinConfigSource.ExtractHubTwinAndVerify(twinDesiredProperties));
            }
            else
            {
                Assert.False(TwinConfigSource.ExtractHubTwinAndVerify(twinDesiredProperties));
            }
        }

        public static IEnumerable<object[]> GetTwinForVerifyingSignatures()
        {
            string goodSchemaVersion = "1.0";
            string badSchemaVersion = "10000.0";
            string ecdsaCert1 = "\rMIIBojCCAQMCFC9DNqy01WDwj8osN3vtP1WOvIK0MAoGCCqGSM49BAMCMC8xLTAr\rBgNVBAMMJE1hbmlmZXN0IFNpZ25lciBDbGllbnQgRUNEU0EgUm9vdCBDQTAeFw0y\rMDEwMDQwMTMxMzdaFw0yMDExMDMwMTMxMzdaMDMxMTAvBgNVBAMMKE1hbmlmZXN0\rIFNpZ25lciBDbGllbnQgRUNEU0EgU2lnbmVyIENlcnQwWTATBgcqhkjOPQIBBggq\rhkjOPQMBBwNCAAQkCRltNJxkeu";
            string ecdsaCert2 = "ZpMzR8EyGplPN30VbGA5ZTDMXXMiGNykwDV2jA\ri9Z6vspIIqxpUibBEfd+5cnkl+Mw9T1ze0IlMAoGCCqGSM49BAMCA4GMADCBiAJC\rAQifI6gDU9Bv87aHikmxXtNW6Xvbzqhivd8cq9Xc6nKBt7zYLvsKzXBJhJNIrGCa\rJ3Yb+Q+FQfYOXfqMP/mcyKNdAkIBxRxVA1loN2VwoeG9IUG+rQhLdCzTvOgQrxZq\rhJRvXjMJxD52KClvXp/Bijq17yhV1aNNLTQuaV4Qiwh38fwA+kA=\r";
            string rsaCert1 = "\rMIID5TCCAc0CFA3QH6TFwsq4ZQwX3hrcKfSFF0UfMA0GCSqGSIb3DQEBCwUAMC0x\rKzApBgNVBAMMIk1hbmlmZXN0IFNpZ25lciBDbGllbnQgUlNBIFJvb3QgQ0EwHhcN\rMjAwOTMwMjA1MDM1WhcNMjEwOTMwMjA1MDM1WjAxMS8wLQYDVQQDDCZNYW5pZmVz\rdCBTaWduZXIgQ2xpZW50IFJTQSBTaWduZXIgQ2VydDCCASIwDQYJKoZIhvcNAQEB\rBQADggEPADCCAQoCggEBALl7JhOI95LHlA0dNYIg6jnJJNLTEh3G8v/gzhB+61F6\rTXLV2DCjPxHwnYq09mFpjj4+JAwLw44MJtU9VEszw+XvXwibPCbxQ/GMYy5w+By6\rHVLMDF3l8QpLmNnhc+KNjgDQmwinGP+gmNrEF7oNIp/n2cA1x8s5q5LueisB4szt\rnMSKacpddm15h38KMxgJkfetpfBhQdojNvzaTeH4gmT34/39QLkUX3g6PJ88nHob\rZEwYQDZ4xGwbrypdVUm2xNGcZLmBgDYNYe5zYy/zWj2DkzOTCkypsf/8toL38Gqn\rUCPEdi7lGwN8/Lp8TASFeB3cc6WRDaiJYdeph7VpWaMCAwEAATANBgkqhkiG9w0B\rAQsFAAOCAgEARChMFj1Ktt/Gz58A";
            string rsaCert2 = "4irJL/HocqDOTgjDEaz0sHGD8Cn8QegINW3H\rT96zJ0ra9uwIyHbZk6UWdIhQfmNRlsm9tqB/9XhiW+PAGjYaA28FDY19cyGHhB54\r94gNLS+DqIkhwab5i4QXADtoE6ni3T/Dre/gPf7QrVrLBezjYq+WKZdOxUUfpsDb\rukf0wmfcSjdDBeWyA605Q8KKsk9M77e7nuzezq/s+HIMFlMvM7xZEowRkqkHZqZ2\rZ8ksUBucjDqZUhH7iyIu4ohH3yZjzEGh77s+jPWk4Om9mUyma/hct9jIsO1+MvyR\rFt9uTfzm+2xrPc8YZfzv9FnAc+3APjskmo94W8daHUEMcOqs40RBwp4JHR8xv1vq\rcrX/PWTpG7/2RRcG4x3dc0TmnOFbcLsa2bMIrHAoFI98YW8yhHW7SZdIRdKJRJ1u\rdYSfoVcgBiQf0CHgIg++f+SyYsZsz0UEP1jtq3ieUDSvlxUgxnDfVx6h4TKCYxrV\rhln4hAR4wSA7+UM3UYYG2AMKrBCxV/cxOO/pFJCwveJtIJVGUi1cww2LTglJ9RRO\rQrMMrgXPtl4DBUzO7Kzvexfq4pmi4mCWBou+0PeCfI9aBnvt6nobrXKAxNkXUKIi\ruTOizMejEmrOWVGOTixXH1OMVvg0uHW65vbyxX/DvOErMaHubCHJNIk=\r";
            string goodEcdsaSignature = "AK6XcZ2PfW1RT+1pi0FNvRh5+5gFcprlgqmG2Eo6xdnisgRtvNICk5EEK0Ob8YOCt+NE5gdvO5lSSGTGbjhGag==";
            string goodRsaSignature = "GL3sEkD6mIUYCK6R36RJZIrI8gmNDzmh/6sT1+lHHYY9Dg2AFK4az13hydisPYjcvjj7+WNzHBLhfOU7NtoriLrTyL76E+7Y2n2CDkb+hqR/y4YraXqdgX6X/Pi97O72pa5orgSqKv/G/U9jxYF3ajK08hEXnq/AsEJhOUGuQg//n8K7znUliyav9ZSfAZ38uG65f+p1RftbWr/S9urynhk/rTc8ACgxwkQ9/jVlc+a7+db4mYl1D5Y1I/lQliaJCR5lo8A32s+A8TYL2MJnAEsHa5q3yvytQZpl/CyKndOZAHlKiAPzBn3eTzjH6J7grWtV+2pPyxPZ+BxkkUlJKQ==";
            string ecdsaAlgo = "ES256";
            string rsaAlgo = "RS256";
            yield return new object[] { "good", goodSchemaVersion, ecdsaAlgo, ecdsaCert1, ecdsaCert2, goodEcdsaSignature };
            yield return new object[] { "bad", badSchemaVersion, ecdsaAlgo, ecdsaCert1, ecdsaCert2, goodEcdsaSignature };
            yield return new object[] { "good", goodSchemaVersion, rsaAlgo, rsaCert1, rsaCert2, goodRsaSignature };
            yield return new object[] { "bad", badSchemaVersion, rsaAlgo, rsaCert1, rsaCert2, goodRsaSignature };
        }
    }
}
