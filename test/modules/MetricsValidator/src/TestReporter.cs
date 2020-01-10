// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Newtonsoft.Json;

    public class TestReporter
    {
        string category;
        List<string> successes = new List<string>();
        Dictionary<string, string> failures = new Dictionary<string, string>();

        [JsonProperty("subcategories", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        List<TestReporter> subcategories = null;

        public TestReporter(string category)
        {
            this.category = category;
        }

        public void Assert(string name, bool success, string message = "Expected true, got false")
        {
            if (success)
            {
                this.successes.Add(name);
            }
            else
            {
                this.failures.Add(name, message);
            }
        }

        public void Assert<T>(string name, T expected, T actual, string message = null)
            where T : IEquatable<T>
        {
            this.Assert(name, expected.Equals(actual), message ?? $"Expected {JsonConvert.SerializeObject(actual)} to equal {JsonConvert.SerializeObject(expected)}");
        }

        public TestReporter MakeSubcategory(string name)
        {
            if (this.subcategories == null)
            {
                this.subcategories = new List<TestReporter>();
            }

            TestReporter newCategory = new TestReporter(name);
            this.subcategories.Add(newCategory);

            return newCategory;
        }

        public Task ReportResults(ModuleClient moduleClient, CancellationToken cancellationToken)
        {
            string result = JsonConvert.SerializeObject(this, Formatting.Indented);
            Message message = new Message(Encoding.UTF8.GetBytes(result));

            return moduleClient.SendEventAsync(message, cancellationToken);
        }
    }
}
