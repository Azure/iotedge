// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common.NUnit
{
    using System;
    using global::NUnit.Framework;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public class EndToEndAttribute : CategoryAttribute
    {
    }
}
