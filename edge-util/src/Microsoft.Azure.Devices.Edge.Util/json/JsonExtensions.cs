// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Json
{
    using Newtonsoft.Json;

    public static class JsonExtensions
    {
        public static string ToJson(this object obj) => JsonConvert.SerializeObject(obj, Formatting.None);

        public static string ToPrettyJson(this object obj) => JsonConvert.SerializeObject(obj, Formatting.Indented);
    }
}
