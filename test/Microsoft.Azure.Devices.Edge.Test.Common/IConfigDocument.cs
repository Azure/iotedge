// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    public interface IConfigDocument
    {
        void ReplaceOrAdd(string dottedKey, string value);
        void RemoveIfExists(string dottedKey);
        string ToString();
    }
}
