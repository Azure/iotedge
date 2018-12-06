// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Collections.Generic;

    public sealed class ApiVersion
    {
        public static readonly ApiVersion Version20180628 = new ApiVersion(1, "2018-06-28");
        public static readonly ApiVersion Version20181230 = new ApiVersion(2, "2018-12-30");

        static readonly Dictionary<string, ApiVersion> Instance = new Dictionary<string, ApiVersion>
        {
            { Version20180628.Name, Version20180628 },
            { Version20181230.Name, Version20181230 }
        };

        ApiVersion(int value, string name)
        {
            this.Name = name;
            this.Value = value;
        }

        public string Name { get; }

        public int Value { get; }

        public static explicit operator ApiVersion(string str)
        {
            if (Instance.TryGetValue(str, out ApiVersion version))
            {
                return version;
            }

            throw new InvalidCastException();
        }
    }
}
