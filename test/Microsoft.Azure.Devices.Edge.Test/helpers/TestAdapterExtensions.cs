// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Test.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    public static class TestAdapterExtensions
    {
        public static string NormalizedName(this TestContext context)
        {
            // e.g. -
            // 'ModuleToModuleDirectMethod("Mqtt","Amqp")' ==>
            //     'moduletomoduledirectmethod-mqtt-amqp'
            IEnumerable<string> parts = Regex.Split(context.TestName, @"[^\w]")
                .Where(s => !string.IsNullOrEmpty(s));
            return string.Join("-", parts).ToLowerInvariant();
        }
    }
}
