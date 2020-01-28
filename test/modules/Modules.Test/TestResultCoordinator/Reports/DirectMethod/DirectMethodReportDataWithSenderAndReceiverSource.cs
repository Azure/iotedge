// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    class DirectMethodReportDataWithSenderAndReceiverSource
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            // See TestCreateReportAsync for parameters names
            new List<object[]>
            {
                new object[]
                {
                    // NetworkOnSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 21, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 7, 0, 0, 0, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOffSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "4", "5", "6", "7" },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 16, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 1, 0, 0, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOnToleratedSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "4", "5", "6", "7" },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 20, 11),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 1, 0, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOffToleratedSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "4", "5", "6", "7" },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 15, 11),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 1, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOnFailure test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "5", "6", "7" },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 21, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 0, 1, 0, 0, 0, false
                },
                new object[]
                {
                    // NetworkOffFailure test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "4", "5", "6", "7" },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 16, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 0, 0, 1, 0, 0, false
                },
                new object[]
                {
                    // MismatchSuccess test
                    Enumerable.Range(1, 7).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "5", "6", "7" },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 21, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 0, 0, 0, 1, 0, true
                },
                new object[]
                {
                    // MismatchFailure test
                    Enumerable.Range(1, 6).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "4", "5", "6", "7" },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 21, 10),
                        new DateTime(2020, 1, 1, 9, 10, 22, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 6, 0, 0, 0, 0, 0, 0, 1, false
                },
                new object[]
                {
                    Enumerable.Range(1, 10).Select(v => v.ToString()),
                    new[] { "1", "2", "3", "8", "10", "11" },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        // Smoke test for mixed results
                        new DateTime(2020, 1, 1, 9, 10, 12, 10), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 13, 10), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 15, 11), // NetworkOffToleratedSuccess
                        new DateTime(2020, 1, 1, 9, 10, 17, 10), // NetworkOffSuccess
                        new DateTime(2020, 1, 1, 9, 10, 18, 10), // NetworkOffSuccess
                        new DateTime(2020, 1, 1, 9, 10, 20, 12), // NetworkOnToleratedSuccess
                        new DateTime(2020, 1, 1, 9, 10, 21, 15), // NetworkOnFailure
                        new DateTime(2020, 1, 1, 9, 10, 24, 15),
                        new DateTime(2020, 1, 1, 9, 10, 24, 17),
                        new DateTime(2020, 1, 1, 9, 10, 25, 20) // NetworkOffFailure
                        // Mismatch Success is the missing 9
                        // MismatchFailure is the presence of 11 in the actualStoreValues
                    },
                    10, 3, 2, 1, 1, 1, 1, 1, 1, false
                }
            };
    }
}
