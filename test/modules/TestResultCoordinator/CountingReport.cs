// Copyright (c) Microsoft. All rights reserved.
namespace TestResultCoordinator
{
    using System.Collections.Generic;
    
    class CountingReport<T> : TestResultReportBase
    {
        readonly IList<T> missingResults = new List<T>();

        public CountingReport(string expectSource, string actualSource, string resultType)
            : base(expectSource, actualSource, resultType)
        {
        }

        public int TotalExpectCount { get; set; }

        public int TotalMatchCount { get; set; }

        public int TotalDuplicateResultCount { get; set; }

        public IList<T> MissingResults => this.missingResults;

        public void AddMissingResult(T result) => this.missingResults.Add(result);
    }
}
