// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    public interface IValidator<in T>
    {
        void Validate(T value);
    }
}