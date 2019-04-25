// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Common
{
    using System;
    using Microsoft.Azure.Devices.Edge.Util;

    public class EnvironmentVariable
    {
        public static string Expect(string name) => Preconditions.CheckNonWhiteSpace(
                Environment.GetEnvironmentVariable(name),
                name
            );
    }
}
