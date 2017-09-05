// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core.Storage
{
    using System.Text;
    using Newtonsoft.Json;

    public static class SerDeExtensions
    {
        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects
        };

        public static T FromJson<T>(this string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default(T);
            }

            return typeof(T) == typeof(string)
                ? (T)(object)json
                : JsonConvert.DeserializeObject<T>(json, Settings);
        }

        public static string ToJson(this object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value as string ?? JsonConvert.SerializeObject(value, Settings);
        }

        public static string FromBytes(this byte[] bytes) => bytes == null || bytes.Length == 0
            ? string.Empty
            : Encoding.UTF8.GetString(bytes);

        public static byte[] ToBytes(this string value) => string.IsNullOrWhiteSpace(value)
            ? new byte[0]
            : Encoding.UTF8.GetBytes(value);

        public static byte[] ToBytes(this object value)
        {
            if (value == null)
            {
                return null;
            }

            var bytes = value as byte[];
            if (bytes == null)
            {
                string json = value.ToJson();
                bytes = json.ToBytes();
            }
            return bytes;
        }

        public static T FromBytes<T>(this byte[] bytes)
        {
            if (bytes == null)
            {
                return default(T);
            }

            if (typeof(T) == typeof(byte[]))
            {
                return (T)(object)bytes;
            }

            string json = bytes.FromBytes();
            var value = json.FromJson<T>();
            return value;
        }
    }
}
