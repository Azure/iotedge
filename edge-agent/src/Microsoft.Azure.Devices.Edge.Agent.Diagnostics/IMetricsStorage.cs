// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Stores metrics for later upload.
    /// </summary>
    public interface IMetricsStorage
    {
        /// <summary>
        /// Adds the string to its storage. The current date will be recorded with it.
        /// </summary>
        /// <param name="data">data to store.</param>
        void WriteData(string data);

        /// <summary>
        /// Retrieves all stored data.
        /// </summary>
        /// <returns>Key is date stored and value is a function that retuens the stored data.</returns>
        IDictionary<DateTime, Func<string>> GetData();

        /// <summary>
        /// Retrieves all data stored after start.
        /// </summary>
        /// <param name="start">Only data stored after this value will be returned.</param>
        /// <returns>Key is date stored and value is a function that retuens the stored data.</returns>
        IDictionary<DateTime, Func<string>> GetData(DateTime start);

        /// <summary>
        /// Retrieves all data stored after start and before end.
        /// </summary>
        /// <param name="start">Only data stored after this value will be returned.</param>
        /// <param name="end">Only data stored before this value will be returned.</param>
        /// <returns>Dictionary where key is date stored and value is a function that retuens the stored data.</returns>
        IDictionary<DateTime, Func<string>> GetData(DateTime start, DateTime end);

        /// <summary>
        /// Deletes all entries older than keepAfter. Comparison is less than or equal.
        /// </summary>
        /// <param name="keepAfter">Oldest date to keep data.</param>
        void RemoveOldEntries(DateTime keepAfter);
    }
}
