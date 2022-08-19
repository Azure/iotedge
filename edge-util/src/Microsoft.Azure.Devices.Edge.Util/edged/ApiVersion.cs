// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Edged
{
    using System;
    using System.Collections.Generic;

    public sealed class ApiVersion : IComparable<ApiVersion>
    {
        public static readonly ApiVersion Version20180628 = new ApiVersion(1, "2018-06-28");
        public static readonly ApiVersion Version20190130 = new ApiVersion(2, "2019-01-30");
        public static readonly ApiVersion Version20191022 = new ApiVersion(3, "2019-10-22");
        public static readonly ApiVersion Version20191105 = new ApiVersion(4, "2019-11-05");
        public static readonly ApiVersion Version20200707 = new ApiVersion(5, "2020-07-07");
        public static readonly ApiVersion Version20211207 = new ApiVersion(6, "2021-12-07");
        public static readonly ApiVersion Version20220803 = new ApiVersion(7, "2022-08-03");
        public static readonly ApiVersion VersionUnknown = new ApiVersion(100, "Unknown");

        static readonly Dictionary<string, ApiVersion> Instance = new Dictionary<string, ApiVersion>
        {
            { Version20180628.Name, Version20180628 },
            { Version20190130.Name, Version20190130 },
            { Version20191022.Name, Version20191022 },
            { Version20191105.Name, Version20191105 },
            { Version20200707.Name, Version20200707 },
            { Version20211207.Name, Version20211207 },
            { Version20220803.Name, Version20220803 }
        };

        ApiVersion(int value, string name)
        {
            this.Name = name;
            this.Value = value;
        }

        public string Name { get; }

        public int Value { get; }

        public int CompareTo(ApiVersion other)
        {
            return this.Value.CompareTo(other.Value);
        }

        public static ApiVersion ParseVersion(string str)
        {
            if (Instance.TryGetValue(str, out ApiVersion version))
            {
                return version;
            }

            return VersionUnknown;
        }
    }
}
