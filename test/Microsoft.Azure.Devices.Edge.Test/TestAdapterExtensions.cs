// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using NUnit.Framework;

    public static class TestAdapterExtensions
    {
        public static string NormalizedName(this TestContext.TestAdapter test)
        {
            // e.g. -
            // 'ModuleToModuleDirectMethod("Mqtt","Amqp")' ==>
            //     'moduletomoduledirectmethod-mqtt-amqp'
            IEnumerable<string> parts = Regex.Split(test.Name, @"[^\w]")
                .Where(s => !string.IsNullOrEmpty(s));
            return string.Join("-", parts).ToLowerInvariant();
        }
    }
}
