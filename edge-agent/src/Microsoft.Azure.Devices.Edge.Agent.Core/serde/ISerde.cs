// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Serde
{
    public interface ISerde<T>
    {
        string Serialize(T t);

        T Deserialize(string json);

        T1 Deserialize<T1>(string json) where T1 : T;
    }
}