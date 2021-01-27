// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    public interface IConfigDocument
    {
        void ReplaceOrAdd<T>(string dottedKey, T value);
        void RemoveIfExists(string dottedKey);
        string ToString();
    }
}
