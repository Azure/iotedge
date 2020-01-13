// Copyright (c) Microsoft. All rights reserved.

namespace MetricsValidator
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;

    public class TestReporter
    {
        [JsonProperty("Category")]
        string category;

        [JsonProperty("Successes")]
        List<string> successes = new List<string>();

        [JsonProperty("Failures")]
        Dictionary<string, string> failures = new Dictionary<string, string>();

        [JsonProperty("Subcategories", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        List<TestReporter> subcategories = null;

        [JsonProperty("Succeeded", Order = -2)]
        public int NumSuccesses
        {
            get
            {
                if (this.subcategories != null)
                {
                    return this.subcategories.Sum(sc => sc.NumSuccesses) + this.successes.Count;
                }

                return this.successes.Count;
            }
        }

        [JsonProperty("Failed", Order = -2)]
        public int NumFailures
        {
            get
            {
                if (this.subcategories != null)
                {
                    return this.subcategories.Sum(sc => sc.NumFailures) + this.failures.Count;
                }

                return this.failures.Count;
            }
        }

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

        public void OrderlessCompare<T>(string name, IEnumerable<T> expected, IEnumerable<T> actual)
            where T : IEquatable<T>
        {
            TestUtilities.OrderlessCompareLogic(expected, actual).ForEach(reason => this.Assert(name, false, reason));
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

        public string ReportResults()
        {
            this.SortSubcategories();
            string result = JsonConvert.SerializeObject(this, Formatting.Indented);
            Console.WriteLine(result);

            return result;
        }

        void SortSubcategories()
        {
            if (this.subcategories != null)
            {
                this.subcategories.ForEach(sc => sc.SortSubcategories());
                this.subcategories = this.subcategories.OrderByDescending(sc => sc.NumFailures).ToList();
            }
        }
    }
}
