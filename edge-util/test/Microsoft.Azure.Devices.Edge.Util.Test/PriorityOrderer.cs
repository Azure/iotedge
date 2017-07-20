// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Util.Test
{
    using System.Collections.Generic;
    using System.Linq;
    using Xunit.Abstractions;
    using Xunit.Sdk;

    public class PriorityOrderer : ITestCaseOrderer
    {
        const string PriorityPropertyName = "Priority";

        public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases) where TTestCase : ITestCase
        {
            (int Priority, TTestCase TestCase) Selector(TTestCase t) =>
                (Priority: t.TestMethod.Method.GetCustomAttributes((typeof(TestPriorityAttribute).AssemblyQualifiedName))
                    .FirstOrDefault()?.GetNamedArgument<int>(PriorityPropertyName) ?? 0,
                TestCase: t);

            return testCases
                .Select(Selector)
                .OrderBy(t => t.Priority)
                .Select(t => t.TestCase);
        }
    }

}
