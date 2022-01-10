// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;

    class DirectMethodLongHaulReportData
    {
        public static IEnumerable<object[]> GetCreateReportData =>
        new List<object[]>
        {
            new object[]
                {
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
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
                    7,
                    true,
                    7, 0, 0, 0, 0, 0, 0
                },
            new object[]
                {
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    7,
                    false,
                    6, 0, 0, 0, 0, 0, 1
                },
            new object[]
                {
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, (HttpStatusCode)Enum.Parse(typeof(HttpStatusCode), "0"), HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    7,
                    false,
                    6, 1, 0, 0, 0, 0, 0
                },
            new object[]
                {
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    7,
                    false,
                    6, 0, 0, 1, 0, 0, 0
                },
            new object[]
                {
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.FailedDependency, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    7,
                    false,
                    6, 0, 0, 0, 1, 0, 0
                },
            new object[]
                {
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    7,
                    false,
                    6, 0, 0, 0, 0, 1, 0
                },
            new object[]
                {
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    7,
                    false,
                    6, 0, 1, 0, 0, 0, 0
                },
        };

        public static object[] GetStatusCodeTestData =>
            new object[]
                {
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, (HttpStatusCode)Enum.Parse(typeof(HttpStatusCode), "0"), HttpStatusCode.Unauthorized, HttpStatusCode.NotFound, HttpStatusCode.FailedDependency, HttpStatusCode.InternalServerError, HttpStatusCode.ServiceUnavailable },
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
                    7,
                    false,
                    1L, 1L, 1L, 1L, 1L, 1L,
                    new Dictionary<HttpStatusCode, long> { { HttpStatusCode.InternalServerError, 1 } }
                };
    }
}
