// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Storage
{
    public interface ITypeMapper<TK, TU>
    {
        TU From(TK value);

        TK To(TU value);
    }

    public class BytesMapper<T> : ITypeMapper<T, byte[]>
    {
        public byte[] From(T value) => value.ToBytes();

        public T To(byte[] value) => value.FromBytes<T>();
    }

    public class JsonMapper<T> : ITypeMapper<T, string>
    {
        public string From(T value) => value.ToJson();

        public T To(string value) => value.FromJson<T>();
    }
}
