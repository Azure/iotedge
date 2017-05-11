// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.Routing.Core.Util;

    public interface ISinkResult<T>
    {
        /// <summary>
        /// Contains messages that were processed successfully
        /// </summary>
        ICollection<T> Succeeded { get; }

        /// <summary>
        /// Contains messages that failed to be processed but can be retried
        /// </summary>
        ICollection<T> Failed { get; }

        /// <summary>
        /// Contains messages that can never be sent successfully.
        /// </summary>
        ICollection<InvalidDetails<T>> InvalidDetailsList { get; }

        /// <summary>
        /// Optional failure metadata for the issue that caused processing failures
        /// </summary>
        Option<SendFailureDetails> SendFailureDetails { get; }

        /// <summary>
        /// Returns true if result is successful - no failed or invalid messages
        /// </summary>
        bool IsSuccessful { get; }
    }
}