// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Http.Test
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class MethodRequestValidatorTest
    {
        [Theory]
        [MemberData(nameof(GetInvalidData))]
        public void ValidateRequestTest(MethodRequest request, Exception expectedException)
        {

        }

        static IEnumerable<object[]> GetInvalidData()
        {
            yield return new object[0];// { new MethodRequest() }
        }
    }
}
