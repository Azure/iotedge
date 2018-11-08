// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.Azure.Devices.Routing.Core.Util;

    public enum CommandType
    {
        SendMessage,
        UpdateEndpoint,
        Checkpoint,
        Succeed,
        Fail,
        Throw,
        Retry,
        Die,
        DeadSucceed,
        Revive,
        Close
    }

    public interface ICommand
    {
        CommandType Type { get; }
    }

    public class SendMessage : ICommand
    {
        readonly TaskCompletionSource<bool> tcs;

        public SendMessage(ICollection<IMessage> messages)
            : this(messages, new TaskCompletionSource<bool>())
        {
        }

        SendMessage(ICollection<IMessage> messages, TaskCompletionSource<bool> tcs)
        {
            this.Messages = Preconditions.CheckNotNull(messages);
            this.tcs = Preconditions.CheckNotNull(tcs);
        }

        public Task Completion => this.tcs.Task;

        public ICollection<IMessage> Messages { get; }

        public CommandType Type => CommandType.SendMessage;

        public void Complete() => this.tcs.SetResult(true);

        public void Complete(Exception exception) => this.tcs.SetException(exception);

        /// <summary>
        /// Copies send message command and task completion source.
        /// This is to allow resending of messages with the same task completion source.
        /// </summary>
        /// <param name="messages">Messages</param>
        /// <returns>SendMessage object</returns>
        public SendMessage Copy(ICollection<IMessage> messages)
        {
            return new SendMessage(messages, this.tcs);
        }
    }

    public class UpdateEndpoint : ICommand
    {
        public UpdateEndpoint(Endpoint endpoint)
        {
            this.Endpoint = Preconditions.CheckNotNull(endpoint);
        }

        public Endpoint Endpoint { get; }

        public CommandType Type => CommandType.UpdateEndpoint;
    }

    public class Checkpoint : ICommand
    {
        public Checkpoint(ISinkResult<IMessage> result)
        {
            this.Result = Preconditions.CheckNotNull(result);
        }

        public ISinkResult<IMessage> Result { get; }

        public CommandType Type => CommandType.Checkpoint;
    }

    public class Succeed : ICommand
    {
        public CommandType Type => CommandType.Succeed;
    }

    public class DeadSucceed : ICommand
    {
        public CommandType Type => CommandType.DeadSucceed;
    }

    public class Fail : ICommand
    {
        public Fail(TimeSpan retryAfter)
        {
            this.RetryAfter = retryAfter;
        }

        public TimeSpan RetryAfter { get; }

        public CommandType Type => CommandType.Fail;
    }

    public class Throw : ICommand
    {
        public Throw(Exception exception)
        {
            this.Exception = exception;
        }

        public Exception Exception { get; }

        public CommandType Type => CommandType.Throw;
    }

    public class Retry : ICommand
    {
        public CommandType Type => CommandType.Retry;
    }

    public class Revive : ICommand
    {
        public CommandType Type => CommandType.Revive;
    }

    public class Die : ICommand
    {
        public CommandType Type => CommandType.Die;
    }

    public class Close : ICommand
    {
        public CommandType Type => CommandType.Close;
    }

    public static class Commands
    {
        public static Close Close { get; } = new Close();

        public static DeadSucceed DeadSucceed { get; } = new DeadSucceed();

        public static Die Die { get; } = new Die();

        public static Retry Retry { get; } = new Retry();

        public static Revive Revive { get; } = new Revive();

        public static Succeed Succeed { get; } = new Succeed();

        public static Checkpoint Checkpoint(ISinkResult<IMessage> result) => new Checkpoint(result);

        public static Fail Fail(TimeSpan retryAfter) => new Fail(retryAfter);

        public static SendMessage SendMessage(params IMessage[] messages) => new SendMessage(messages);

        public static Throw Throw(Exception exception) => new Throw(exception);

        public static UpdateEndpoint UpdateEndpoint(Endpoint endpoint) => new UpdateEndpoint(endpoint);
    }
}
