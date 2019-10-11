// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util;

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

        public ICollection<T> Succeeded { get; }

        public ICollection<T> Failed { get; }

        public ICollection<InvalidDetails<T>> InvalidDetailsList { get; }

        public Option<SendFailureDetails> SendFailureDetails { get; protected set; }

        public bool IsSuccessful => !this.Failed.Any();
    }

    public class MergingSinkResult<T> : SinkResult<T>
    {
        public MergingSinkResult()
            : base(new List<T>(), new List<T>(), new List<InvalidDetails<T>>(), null)
        {
        }

        public void Merge(ISinkResult<T> other)
        {
            this.Succeeded.AddRange(other.Succeeded);
            this.Failed.AddRange(other.Failed);
            this.InvalidDetailsList.AddRange(other.InvalidDetailsList);

            if (IsMoreSignificant(this.SendFailureDetails, other.SendFailureDetails))
            {
                this.SendFailureDetails = other.SendFailureDetails;
            }
        }

        public void AddFailed(IEnumerable<T> failed)
        {
            this.Failed.AddRange(failed);
        }

        private new List<T> Succeeded => base.Succeeded as List<T>;
        private new List<T> Failed => base.Failed as List<T>;
        private new List<InvalidDetails<T>> InvalidDetailsList => base.InvalidDetailsList as List<InvalidDetails<T>>;

        private static bool IsMoreSignificant(Option<SendFailureDetails> baseDetails, Option<SendFailureDetails> currentDetails)
        {
            // whatever happend before, if no details now, that cannot be more significant
            if (currentDetails == Option.None<SendFailureDetails>())
                return false;

            // if something wrong happened now, but nothing before, then that is more significant
            if (baseDetails == Option.None<SendFailureDetails>())
                return true;

            // at this point something has happened before, as well as now. Pick the more significant
            var baseUnwrapped = baseDetails.Expect(ThrowBadProgramLogic);
            var currentUnwrapped = currentDetails.Expect(ThrowBadProgramLogic);

            // in theory this case is represened by Option.None, but let's check it just for sure
            if (currentUnwrapped.FailureKind == FailureKind.None)
                return false;

            // Transient beats non-transient
            if (baseUnwrapped.FailureKind != FailureKind.Transient && currentUnwrapped.FailureKind == FailureKind.Transient)
                return true;

            return false;

            InvalidOperationException ThrowBadProgramLogic() => new InvalidOperationException("Error in program logic, uwrapped Option<T> should have had value");
        }
    }
}
