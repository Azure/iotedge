// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util
{
    /// <summary>
    /// Represents a type capable of "showing" itself.
    /// This allows types that require a string representation to
    /// be declared with IShowable, forcing implementors to implement
    /// a string representation.
    /// </summary>
    public interface IShowable
    {
        string Show();
    }
}