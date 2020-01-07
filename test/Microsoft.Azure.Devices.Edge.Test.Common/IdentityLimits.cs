// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;

    // A utility class that enforces length limits on edge device ID, leaf device ID, and certificate
    // common name (CN) strings.
    public class IdentityLimits
    {
        // The max string lengths set here are very specific to the way the tests are generating
        // unique identity strings. Look at the callers of this class's static methods to see how
        // identity strings are generated. It would be great to add generation of the strings to
        // this class as well, but it's a lot trickier because different elements of the identity
        // strings are generated at different times, and they build upon one another in a hierarchy
        // (e.g., CN = "{edge-device-id}-{leaf-device-id}[-{unique-id}]").
        const int EdgeDeviceIdMaxLength = 36;
        const int LeafDeviceIdMaxLength = 62;
        const int CertCnMaxLength = 64;

        const string LengthErrorMessageBase =
            "The {0} is more than {1} characters long and may cause problems in tests that " +
            "generate certificates";
        const string LengthErrorMessageDevice =
            LengthErrorMessageBase + " with a CN based on the device ID";

        public static string CheckEdgeId(string id)
        {
            if (id.Length > EdgeDeviceIdMaxLength)
            {
                throw new ArgumentException(
                    string.Format(LengthErrorMessageDevice, "edge device ID", EdgeDeviceIdMaxLength));
            }

            return id;
        }

        public static string CheckLeafId(string id)
        {
            if (id.Length > LeafDeviceIdMaxLength)
            {
                throw new ArgumentException(
                    string.Format(LengthErrorMessageDevice, "leaf device ID", LeafDeviceIdMaxLength));
            }

            return id;
        }

        public static string CheckCommonName(string id)
        {
            if (id.Length > CertCnMaxLength)
            {
                throw new ArgumentException(
                    string.Format(LengthErrorMessageBase, "certificate common name (CN)", CertCnMaxLength));
            }

            return id;
        }
    }
}