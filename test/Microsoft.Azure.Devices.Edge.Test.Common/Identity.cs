// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;

    // A string type that enforces length limits on the individual strings that are ultimately
    // combined in the tests to create a certificate common name (CN).
    public class IdentityString
    {
        enum IdType
        {
            EdgeDevice,
            LeafDevice,
            CertificateCommonName
        }

        // The tests currently create a leaf device ID by appending some unique information to the
        // "parent" device ID. Then they create a CN by optionally appending a short suffix to the
        // leaf device ID.
        //
        // In the worst-case scenario the tests currently need:
        // - 2 chars for the CN suffix
        // - 26 chars for the leaf ID suffix
        // ...and that leaves 36 chars for the device ID if we don't want to exceed 64 chars total.
        const int EdgeDeviceIdMaxLength = 36;
        const int LeafDeviceIdMaxLength = 62;
        const int CertCnMaxLength = 64;

        const string LengthErrorMessageBase = 
            "The {0} is more than {1} characters long and may cause problems in tests that " +
            "generate certificates";
        const string LengthErrorMessageDevice =
            LengthErrorMessageBase + " with a CN based on the device ID";

        string id;
        IdType type;

        IdentityString(string id, IdType type)
        {
            this.id = id;
            this.type = type;
        }

        public static IdentityString EdgeDevice(string id)
        {
            if (id.Length > EdgeDeviceIdMaxLength)
            {
                throw new ArgumentException(
                    string.Format(LengthErrorMessageDevice, "edge device ID", EdgeDeviceIdMaxLength));
            }

            return new IdentityString(id, IdType.EdgeDevice);
        }

        public static IdentityString LeafDevice(IdentityString parent, string suffix)
        {
            if (parent.type != IdType.EdgeDevice)
            {
                string message = "Argument 'parent' must be an edge device";
                throw new ArgumentException(message);
            }

            string id = $"{parent.ToString()}{suffix}";
            if (id.Length > LeafDeviceIdMaxLength)
            {
                throw new ArgumentException(
                    string.Format(LengthErrorMessageDevice, "leaf device ID", LeafDeviceIdMaxLength));
            }

            return new IdentityString(id, IdType.LeafDevice);
        }

        public static IdentityString CertificateCommonName(IdentityString parent, string suffix)
        {
            if (parent.type == IdType.CertificateCommonName)
            {
                string message = "Argument 'parent' cannot be another certificate common name";
                throw new ArgumentException(message);
            }

            string id = $"{parent.ToString()}{suffix}";
            if (id.Length > CertCnMaxLength)
            {
                throw new ArgumentException(
                    string.Format(LengthErrorMessageBase, "certificate common name (CN)", CertCnMaxLength));
            }

            return new IdentityString(id, IdType.CertificateCommonName);
        }

        public override string ToString() => this.id;

        public static implicit operator string(IdentityString s) => s.ToString();
    }
}