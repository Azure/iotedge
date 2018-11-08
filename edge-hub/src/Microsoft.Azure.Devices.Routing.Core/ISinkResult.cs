// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public interface ISinkResult<T>
    {
        /// <summary>
        /// Gets messages that failed to be processed but can be retried
        /// </summary>
        ICollection<T> Failed { get; }

        /// <summary>
        /// Gets messages that can never be sent successfully.
        /// </summary>
        ICollection<InvalidDetails<T>> InvalidDetailsList { get; }

        /// <summary>
        /// Gets a value indicating whether result is successful; return true if it is successful - no failed or invalid messages
        /// </summary>
        bool IsSuccessful { get; }

        /// <summary>
        /// Gets optional failure metadata for the issue that caused processing failures
        /// </summary>
        Option<SendFailureDetails> SendFailureDetails { get; }

        /// <summary>
        /// Gets messages that were processed successfully
        /// </summary>
        ICollection<T> Succeeded { get; }
    }
}
