// Copyright (c) Microsoft. All rights reserved.
namespace Modules.Test.TestResultCoordinator.Reports.DirectMethod
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.NetworkController;

    class DirectMethodConnectivityReportDataWithSenderAndReceiverSource
    {
        public static IEnumerable<object[]> GetCreateReportData =>
            // See TestCreateReportAsync for parameters names
            new List<object[]>
            {
                new object[]
                {
                    // NetworkOnSuccess test
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
                    10, 7, 0, 0, 0, 0, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOffSuccess test with NotFound exception
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 4UL, 5UL, 6UL, 7UL },
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
                    // NetworkOffSuccess test with FailedDependency exception
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 4UL, 5UL, 6UL, 7UL },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.FailedDependency, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    new[] { 1UL, 2UL, 4UL, 5UL, 6UL, 7UL },
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
                    new[] { 1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL },
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
                    // NetworkOnFailure test below failure threshold
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 3UL, 5UL, 6UL, 7UL },
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
                    10, 5, 0, 0, 0, 2, 0, 0, 0, true
                },
                new object[]
                {
                    // NetworkOnFailure test above failure threshold
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 3UL, 5UL, 6UL, 7UL },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.NotFound, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError },
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
                    10, 2, 0, 0, 0, 5, 0, 0, 0, false
                },
                new object[]
                {
                    // NetworkOffFailure test
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    // MismatchSuccess test
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 3UL, 5UL, 6UL, 7UL },
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
                    Enumerable.Range(1, 6).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL },
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
                    Enumerable.Range(1, 10).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 3UL, 8UL, 10UL, 11UL },
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError },
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
                },
                new object[]
                {
                    // Non-Offline test below failure threshold
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    10, 6, 0, 0, 0, 1, 0, 0, 0, true,
                    NetworkControllerType.Satellite
                },
                new object[]
                {
                    // Non-Offline test above failure threshold
                    Enumerable.Range(1, 7).Select(v => (ulong)v), Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new List<HttpStatusCode> { HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK },
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
                    10, 3, 0, 0, 0, 4, 0, 0, 0, false,
                    NetworkControllerType.Satellite
                },
                new object[]
                {
                    // MismatchSuccess Non-offline
                    Enumerable.Range(1, 7).Select(v => (ulong)v),
                    new[] { 1UL, 2UL, 3UL, 5UL, 6UL, 7UL },
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
                    10, 6, 0, 0, 0, 0, 0, 1, 0, true,
                    NetworkControllerType.Satellite
                },
            };
    }
}
