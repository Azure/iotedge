// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public class SinkResult<T> : ISinkResult<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        static readonly Option<SendFailureDetails> NoSendFailure = Option.None<SendFailureDetails>();
        static readonly ICollection<T> EmptyItems = ImmutableList<T>.Empty;
        static readonly ICollection<InvalidDetails<T>> EmptyInvalidDetailsList = ImmutableList<InvalidDetails<T>>.Empty;

        public SinkResult(ICollection<T> succeeded)
            : this(succeeded, EmptyItems, null)
        {
        }

        public SinkResult(ICollection<T> succeeded, SendFailureDetails sendFailureDetails)
            : this(succeeded, EmptyItems, sendFailureDetails)
        {
        }

        public SinkResult(ICollection<T> succeeded, ICollection<T> failed, SendFailureDetails sendFailureDetails)
            : this(succeeded, failed, EmptyInvalidDetailsList, sendFailureDetails)
        {
        }

        public SinkResult(ICollection<T> succeeded, ICollection<T> failed, ICollection<InvalidDetails<T>> invalid, SendFailureDetails sendFailureDetails)
        {
            this.Succeeded = Preconditions.CheckNotNull(succeeded);
            this.Failed = Preconditions.CheckNotNull(failed);
            this.InvalidDetailsList = Preconditions.CheckNotNull(invalid);
            this.SendFailureDetails = sendFailureDetails == null ? NoSendFailure : Option.Some(sendFailureDetails);
        }

        public static ISinkResult<T> Empty { get; } = new SinkResult<T>(EmptyItems);

        public ICollection<T> Failed { get; }

        public ICollection<InvalidDetails<T>> InvalidDetailsList { get; }

        public bool IsSuccessful => !this.Failed.Any();

        public Option<SendFailureDetails> SendFailureDetails { get; }

        public ICollection<T> Succeeded { get; }
    }
}
