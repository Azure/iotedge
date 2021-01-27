// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    public class DirectMethodConnectivityReportDataWithSenderSourceOnly
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            // See TestCreateReportAsync for parameters names
            new List<object[]>
            {
                new object[]
                {
                    // NetworkOnSuccess test
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
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
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
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
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    10, 5, 0, 0, 0, 2, 0, 0, 0, false
                },
                new object[]
                {
                    // NetworkOffFailure test
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        new DateTime(2020, 1, 1, 9, 10, 12, 10),
                        new DateTime(2020, 1, 1, 9, 10, 13, 10),
                        new DateTime(2020, 1, 1, 9, 10, 16, 10),
                        new DateTime(2020, 1, 1, 9, 10, 17, 10),
                        new DateTime(2020, 1, 1, 9, 10, 23, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 10),
                        new DateTime(2020, 1, 1, 9, 10, 24, 15)
                    },
                    10, 5, 0, 0, 0, 0, 2, 0, 0, false
                },
                new object[]
                {
                    Enumerable.Range(1, 10).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
                    new DateTime[]
                    {
                        // Smoke test for mixed results for edgeAgent scenario
                        new DateTime(2020, 1, 1, 9, 10, 12, 10), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 13, 10), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 15, 11), // NetworkOffToleratedSuccess
                        new DateTime(2020, 1, 1, 9, 10, 17, 10), // NetworkOffSuccess
                        new DateTime(2020, 1, 1, 9, 10, 18, 10), // NetworkOffSuccess
                        new DateTime(2020, 1, 1, 9, 10, 20, 12), // NetworkOnToleratedSuccess
                        new DateTime(2020, 1, 1, 9, 10, 21, 15), // NetworkOnFailure
                        new DateTime(2020, 1, 1, 9, 10, 24, 15), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 24, 17), // NetworkOnSuccess
                        new DateTime(2020, 1, 1, 9, 10, 25, 20) // NetworkOffFailure
                    },
                    10, 4, 2, 1, 1, 1, 1, 0, 0, false
                },
            };
    }
}
