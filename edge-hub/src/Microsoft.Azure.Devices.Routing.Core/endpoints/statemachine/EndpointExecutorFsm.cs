// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Routing.Core.Endpoints.StateMachine
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Extensions.Logging;

    public class EndpointExecutorFsm : IDisposable
    {
        static readonly SendFailureDetails DefaultSendFailureDetails = new SendFailureDetails(FailureKind.None, new Exception());
        static readonly SendFailureDetails DefaultFailureDetails = new SendFailureDetails(FailureKind.InternalError, new Exception());
        static readonly TimeSpan LogUserAnalyticsErrorOnUnhealthySince = TimeSpan.FromMinutes(10);
        static readonly ICollection<IMessage> EmptyMessages = ImmutableList<IMessage>.Empty;

        static readonly IReadOnlyDictionary<State, StateActions> Actions = new Dictionary<State, StateActions>
        {
            { State.Idle, new StateActions(EnterIdleAsync, StateActions.NullAction) },
            { State.Sending, new StateActions(EnterSendingAsync, StateActions.NullAction) },
            { State.Checkpointing, new StateActions(EnterCheckpointingAsync, StateActions.NullAction) },
            { State.Failing, new StateActions(EnterFailingAsync, ExitFailingAsync) },
            { State.DeadCheckpointing, new StateActions(EnterDeadCheckpointingAsync, ExitDeadCheckpointingAsync) },
            { State.DeadIdle, new StateActions(StateActions.NullAction, StateActions.NullAction) },
            { State.DeadProcess, new StateActions(EnterProcessDeadAsync, StateActions.NullAction) },
            { State.Closed, new StateActions(EnterClosedAsync, StateActions.NullAction) }
        };

        static readonly IReadOnlyDictionary<StateCommandPair, StateTransition> Transitions = new Dictionary<StateCommandPair, StateTransition>
        {
            // Idle
            { new StateCommandPair(State.Idle, CommandType.SendMessage), new StateTransition(State.Sending, PrepareForSendAsync) },
            { new StateCommandPair(State.Idle, CommandType.UpdateEndpoint), new StateTransition(State.Idle, UpdateEndpointAsync) },
            { new StateCommandPair(State.Idle, CommandType.Close), new StateTransition(State.Closed) },

            // Sending
            { new StateCommandPair(State.Sending, CommandType.Checkpoint), new StateTransition(State.Checkpointing, PrepareForCheckpointAsync) },
            { new StateCommandPair(State.Sending, CommandType.Throw), new StateTransition(State.Idle, ThrowCompleteAsync) },
            { new StateCommandPair(State.Sending, CommandType.Fail), new StateTransition(State.Failing, FailAsync) },
            { new StateCommandPair(State.Sending, CommandType.Die), new StateTransition(State.DeadProcess, DieAsync) },

            // Checkpointing
            { new StateCommandPair(State.Checkpointing, CommandType.Succeed), new StateTransition(State.Idle, SucceedCompleteAsync) },
            { new StateCommandPair(State.Checkpointing, CommandType.Fail), new StateTransition(State.Failing, FailAsync) },
            { new StateCommandPair(State.Checkpointing, CommandType.Die), new StateTransition(State.DeadProcess, DieAsync) },
            { new StateCommandPair(State.Checkpointing, CommandType.Throw), new StateTransition(State.Idle, ThrowCompleteAsync) },
            { new StateCommandPair(State.Checkpointing, CommandType.SendMessage), new StateTransition(State.Sending, PrepareForSendAsync) },

            // Failing
            { new StateCommandPair(State.Failing, CommandType.Retry), new StateTransition(State.Sending) },
            { new StateCommandPair(State.Failing, CommandType.UpdateEndpoint), new StateTransition(State.Sending, UpdateEndpointAsync) },
            { new StateCommandPair(State.Failing, CommandType.Die), new StateTransition(State.DeadProcess, DieAsync) },
            { new StateCommandPair(State.Failing, CommandType.Close), new StateTransition(State.Closed) },

            // Idle Dead
            { new StateCommandPair(State.DeadIdle, CommandType.SendMessage), new StateTransition(State.DeadProcess, PrepareForSendAsync) },
            { new StateCommandPair(State.DeadIdle, CommandType.UpdateEndpoint), new StateTransition(State.Idle, UpdateEndpointAsync) },
            { new StateCommandPair(State.DeadIdle, CommandType.Close), new StateTransition(State.Closed) },

            // ProcessDead
            { new StateCommandPair(State.DeadProcess, CommandType.Revive), new StateTransition(State.Sending, PrepareForReviveAsync) },
            { new StateCommandPair(State.DeadProcess, CommandType.Checkpoint), new StateTransition(State.DeadCheckpointing, PrepareForCheckpointAsync) },

            // DeadCheckpointing
            { new StateCommandPair(State.DeadCheckpointing, CommandType.DeadSucceed), new StateTransition(State.DeadIdle) },

            // Closed
            { new StateCommandPair(State.Closed, CommandType.SendMessage), new StateTransition(State.Closed, SendMessageClosedAsync) },
        };

        readonly EndpointExecutorConfig config;
        readonly Timer retryTimer;
        readonly AsyncLock sync = new AsyncLock();
        readonly ISystemTime systemTime;

        volatile State state;
        volatile int retryAttempts;
        SendMessage currentSendCommand;
        Checkpoint currentCheckpointCommand;
        Option<DateTime> lastFailedRevivalTime;
        Option<DateTime> unhealthySince;
        volatile IProcessor processor;
        TimeSpan retryPeriod;

        public EndpointExecutorFsm(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig config)
            : this(endpoint, checkpointer, config, SystemTime.Instance)
        {
        }

        public EndpointExecutorFsm(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig config, ISystemTime systemTime)
        {
            this.processor = Preconditions.CheckNotNull(endpoint).CreateProcessor();
            this.Checkpointer = Preconditions.CheckNotNull(checkpointer);
            this.config = Preconditions.CheckNotNull(config);
            this.systemTime = Preconditions.CheckNotNull(systemTime);
            this.retryTimer = new Timer(this.RetryAsync, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            this.retryPeriod = Timeout.InfiniteTimeSpan;
            this.lastFailedRevivalTime = checkpointer.LastFailedRevivalTime;
            this.unhealthySince = checkpointer.UnhealthySince;

            if (checkpointer.LastFailedRevivalTime.HasValue)
            {
                this.state = State.DeadIdle;
                this.retryAttempts = short.MaxValue; // setting to some big value
            }
            else
            {
                this.state = State.Idle;
            }
        }

        public Endpoint Endpoint => this.processor.Endpoint;

        public ICheckpointer Checkpointer { get; }

        public EndpointExecutorStatus Status =>
            new EndpointExecutorStatus(this.Endpoint.Id, this.state, this.retryAttempts, this.retryPeriod, this.lastFailedRevivalTime, this.unhealthySince, new CheckpointerStatus(this.Checkpointer.Id, this.Checkpointer.Offset, this.Checkpointer.Proposed));

        public async Task RunAsync(ICommand command)
        {
            using (await this.sync.LockAsync())
            {
                await RunInternalAsync(this, command);
            }
        }

        public Task CloseAsync() => this.RunAsync(Commands.Close);

        public void Dispose() => this.Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Checkpointer.Dispose();
                this.retryTimer.Dispose();
                this.sync.Dispose();
            }
        }

        static Task PrepareForSendAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is SendMessage);

            var send = (SendMessage)command;
            thisPtr.currentSendCommand = send;
            return TaskEx.Done;
        }

        static Task PrepareForCheckpointAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is Checkpoint);

            var checkpoint = (Checkpoint)command;
            thisPtr.currentCheckpointCommand = checkpoint;
            return TaskEx.Done;
        }

        static Task SucceedCompleteAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is Succeed);

            thisPtr.currentSendCommand.Complete();
            thisPtr.currentSendCommand = null;
            thisPtr.currentCheckpointCommand = null;
            return TaskEx.Done;
        }

        static Task ExitDeadCheckpointingAsync(EndpointExecutorFsm thisPtr)
        {
            Preconditions.CheckNotNull(thisPtr);

            thisPtr.currentSendCommand.Complete();
            thisPtr.currentSendCommand = null;
            thisPtr.currentCheckpointCommand = null;
            return TaskEx.Done;
        }

        static Task ThrowCompleteAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is Throw);

            var @throw = (Throw)command;
            thisPtr.currentSendCommand.Complete(@throw.Exception);
            thisPtr.currentSendCommand = null;
            thisPtr.currentCheckpointCommand = null;
            return TaskEx.Done;
        }

        static Task SendMessageClosedAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is SendMessage);

            var send = (SendMessage)command;
            send.Complete();
            return TaskEx.Done;
        }

        static async Task UpdateEndpointAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is UpdateEndpoint);
            thisPtr.retryAttempts = 0;

            Events.UpdateEndpoint(thisPtr);

            try
            {
                var update = (UpdateEndpoint)command;
                await thisPtr.processor.CloseAsync(CancellationToken.None);
                thisPtr.processor = update.Endpoint.CreateProcessor();
                Events.UpdateEndpointSuccess(thisPtr);
            }
            catch (Exception ex)
            {
                Events.UpdateEndpointFailure(thisPtr, ex);

                // TODO(manusr): If this throws, it will break the state machine
                throw;
            }
        }

        static Task PrepareForReviveAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is Revive);

            Events.PrepareForRevive(thisPtr);
            return TaskEx.Done;
        }

        static Task FailAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is Fail);

            var fail = (Fail)command;
            thisPtr.retryPeriod = fail.RetryAfter;
            return TaskEx.Done;
        }

        static Task DieAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is Die);

            thisPtr.lastFailedRevivalTime = Option.Some(thisPtr.systemTime.UtcNow);
            Events.Die(thisPtr);
            return TaskEx.Done;
        }

        static Task EnterIdleAsync(EndpointExecutorFsm thisPtr)
        {
            // initialize the machine
            Reset(thisPtr);
            return TaskEx.Done;
        }

        static async Task EnterSendingAsync(EndpointExecutorFsm thisPtr)
        {
            ICommand next;
            TimeSpan retryAfter;
            ICollection<IMessage> messages = EmptyMessages;
            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan endpointTimeout = TimeSpan.FromMilliseconds(thisPtr.config.Timeout.TotalMilliseconds * thisPtr.Endpoint.FanOutFactor);
            try
            {
                Preconditions.CheckNotNull(thisPtr.currentSendCommand);

                messages = thisPtr.currentSendCommand.Messages.Where(thisPtr.Checkpointer.Admit).ToArray();
                if (messages.Count > 0)
                {
                    ISinkResult<IMessage> result;
                    Events.Send(thisPtr, thisPtr.currentSendCommand.Messages, messages);

                    using (var cts = new CancellationTokenSource(endpointTimeout))
                    {
                        result = await thisPtr.processor.ProcessAsync(messages, cts.Token);
                    }

                    if (result.IsSuccessful)
                    {
                        if (thisPtr.lastFailedRevivalTime.HasValue)
                        {
                            Events.Revived(thisPtr);
                        }

                        // reset lastFailedRevivalTime and unhealthy since
                        thisPtr.lastFailedRevivalTime = Option.None<DateTime>();
                        thisPtr.unhealthySince = Option.None<DateTime>();
                        thisPtr.retryAttempts = 0;

                        Events.SendSuccess(thisPtr, messages, result, stopwatch);
                    }
                    else
                    {
                        thisPtr.unhealthySince = !thisPtr.unhealthySince.HasValue
                            ? Option.Some(thisPtr.systemTime.UtcNow)
                            : thisPtr.unhealthySince;
                        Events.SendFailure(thisPtr, result, stopwatch);
                    }

                    next = Commands.Checkpoint(result);
                }
                else
                {
                    Events.SendNone(thisPtr);
                    next = Commands.Checkpoint(SinkResult<IMessage>.Empty);
                }
            }
            catch (Exception ex) when (thisPtr.ShouldRetry(ex, out retryAfter))
            {
                Events.SendFailureUnhandledException(thisPtr, messages, stopwatch, ex);
                thisPtr.unhealthySince = !thisPtr.unhealthySince.HasValue
                    ? Option.Some(thisPtr.systemTime.UtcNow)
                    : thisPtr.unhealthySince;
                next = Commands.Fail(retryAfter);
            }
            catch (Exception ex)
            {
                Events.SendFailureUnhandledException(thisPtr, messages, stopwatch, ex);
                thisPtr.unhealthySince = !thisPtr.unhealthySince.HasValue
                    ? Option.Some(thisPtr.systemTime.UtcNow)
                    : thisPtr.unhealthySince;
                next = thisPtr.config.ThrowOnDead ? (ICommand)Commands.Throw(ex) : Commands.Die;
            }

            await RunInternalAsync(thisPtr, next);
        }

        static async Task EnterDeadCheckpointingAsync(EndpointExecutorFsm thisPtr)
        {
            ICommand next;
            try
            {
                Preconditions.CheckNotNull(thisPtr.currentCheckpointCommand);
                using (var cts = new CancellationTokenSource(thisPtr.config.Timeout))
                {
                    ISinkResult<IMessage> result = thisPtr.currentCheckpointCommand.Result;
                    Events.Checkpoint(thisPtr, result);
                    await thisPtr.Checkpointer.CommitAsync(result.Succeeded, EmptyMessages, thisPtr.lastFailedRevivalTime, thisPtr.unhealthySince, cts.Token);
                    Events.CheckpointSuccess(thisPtr, result);
                }

                next = Commands.DeadSucceed;
                Events.DeadSuccess(thisPtr, thisPtr.currentCheckpointCommand.Result.Succeeded);
            }
            catch (Exception ex)
            {
                Events.CheckpointFailure(thisPtr, ex);
                next = thisPtr.config.ThrowOnDead
                    ? (ICommand)Commands.Throw(ex)
                    : Commands.DeadSucceed;
            }

            await RunInternalAsync(thisPtr, next);
        }

        static async Task EnterCheckpointingAsync(EndpointExecutorFsm thisPtr)
        {
            ICommand next;
            try
            {
                Preconditions.CheckNotNull(thisPtr.currentCheckpointCommand);
                using (var cts = new CancellationTokenSource(thisPtr.config.Timeout))
                {
                    ISinkResult<IMessage> result = thisPtr.currentCheckpointCommand.Result;

                    if (result.Succeeded.Any() || result.InvalidDetailsList.Any())
                    {
                        ICollection<IMessage> toCheckpoint = result.InvalidDetailsList.Count > 0
                            ? result.Succeeded.Concat(result.InvalidDetailsList.Select(i => i.Item)).ToList()
                            : result.Succeeded;
                        ICollection<IMessage> remaining = result.Failed;

                        Events.Checkpoint(thisPtr, result);
                        await thisPtr.Checkpointer.CommitAsync(toCheckpoint, remaining, Option.None<DateTime>(), thisPtr.unhealthySince, cts.Token);
                        Events.CheckpointSuccess(thisPtr, result);
                    }
                }

                next = EnterCheckpointingHelper(thisPtr);
            }
            catch (Exception ex)
            {
                Events.CheckpointFailure(thisPtr, ex);
                next = thisPtr.config.ThrowOnDead
                    ? Commands.Throw(ex)
                    : EnterCheckpointingHelper(thisPtr);
            }

            await RunInternalAsync(thisPtr, next);
        }

        static ICommand EnterCheckpointingHelper(EndpointExecutorFsm thisPtr)
        {
            ICommand next;

            // If there was a partial failure, try to resend the failed messages
            // Copy the initial send message command to keep the uncompleted TaskCompletionSource
            if (thisPtr.currentCheckpointCommand.Result.Failed.Count > 0)
            {
                TimeSpan retryAfter;
                // PartialFailures should always have an exception object filled out
                Exception innerException = thisPtr.currentCheckpointCommand.Result.SendFailureDetails.GetOrElse(DefaultSendFailureDetails).RawException;

                // 1. Change Messages to be sent to failedMessages
                // 2. Change the Command type to Fail or Dead depending on retry
                thisPtr.currentSendCommand = thisPtr.currentSendCommand.Copy(thisPtr.currentCheckpointCommand.Result.Failed);

                bool shouldRetry = thisPtr.ShouldRetry(innerException, out retryAfter);
                Events.CheckRetryInnerException(innerException, shouldRetry);
                if (shouldRetry)
                {
                    next = Commands.Fail(retryAfter);
                }
                else
                {
                    next = thisPtr.config.ThrowOnDead ? (ICommand)Commands.Throw(innerException) : Commands.Die;
                }
            }
            else
            {
                next = Commands.Succeed;
            }

            return next;
        }

        static Task EnterFailingAsync(EndpointExecutorFsm thisPtr)
        {
            thisPtr.retryAttempts++;
            thisPtr.retryTimer.Change(thisPtr.retryPeriod, Timeout.InfiniteTimeSpan);
            Events.RetryDelay(thisPtr);
            return TaskEx.Done;
        }

        static Task ExitFailingAsync(EndpointExecutorFsm thisPtr)
        {
            thisPtr.retryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            thisPtr.retryPeriod = Timeout.InfiniteTimeSpan;
            Events.Retry(thisPtr);
            return TaskEx.Done;
        }

        static async Task EnterProcessDeadAsync(EndpointExecutorFsm thisPtr)
        {
            Preconditions.CheckArgument(thisPtr.lastFailedRevivalTime.HasValue);

            TimeSpan deadFor = thisPtr.systemTime.UtcNow - thisPtr.lastFailedRevivalTime.GetOrElse(thisPtr.systemTime.UtcNow);

            if (deadFor >= thisPtr.config.RevivePeriod)
            {
                await RunInternalAsync(thisPtr, Commands.Revive);
            }
            else
            {
                try
                {
                    // In the dead state, checkpoint all "sent" messages
                    // This effectively drops the messages, allowing the other endpoints in
                    // this router to make progress.
                    ICollection<IMessage> tocheckpoint = thisPtr.currentSendCommand.Messages;
                    Events.Dead(thisPtr, tocheckpoint);
                    SendFailureDetails persistingFailureDetails = thisPtr.currentCheckpointCommand?.Result?.SendFailureDetails.GetOrElse(default(SendFailureDetails));
                    await RunInternalAsync(thisPtr, Commands.Checkpoint(new SinkResult<IMessage>(tocheckpoint, persistingFailureDetails)));
                }
                catch (Exception ex)
                {
                    Events.DeadFailure(thisPtr, ex);
                }
            }
        }

        static Task EnterClosedAsync(EndpointExecutorFsm thisPtr)
        {
            thisPtr.currentSendCommand?.Complete();
            Reset(thisPtr);
            return Task.WhenAll(thisPtr.processor.CloseAsync(CancellationToken.None), thisPtr.Checkpointer.CloseAsync(CancellationToken.None));
        }

        static void Reset(EndpointExecutorFsm thisPtr)
        {
            thisPtr.currentSendCommand = null;
            thisPtr.currentCheckpointCommand = null;
            thisPtr.retryAttempts = 0;
        }

        static async Task RunInternalAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            StateTransition transition;
            var pair = new StateCommandPair(thisPtr.state, command.Type);
            if (!Transitions.TryGetValue(pair, out transition))
            {
                throw new InvalidOperationException($"Unknown state transition. In state.{thisPtr.state}, Got command.{command.Type}");
            }

            StateActions currentActions = Actions[thisPtr.state];
            StateActions nextActions = Actions[transition.NextState];

            await currentActions.Exit(thisPtr);
            Events.StateExit(thisPtr);

            await transition.TransitionAction(thisPtr, command);
            Events.StateTransition(thisPtr, thisPtr.state, transition.NextState);

            thisPtr.state = transition.NextState;
            await nextActions.Enter(thisPtr);
            Events.StateEnter(thisPtr);
        }

        bool ShouldRetry(Exception exception, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            ITransientErrorDetectionStrategy detectionStrategy = this.processor.ErrorDetectionStrategy;
            ShouldRetry shouldRetry = this.config.RetryStrategy.GetShouldRetry();
            return detectionStrategy.IsTransient(exception) && shouldRetry(this.retryAttempts, exception, out retryAfter);
        }

        async void RetryAsync(object obj)
        {
            try
            {
                this.retryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                await this.RunAsync(Commands.Retry);
            }
            catch (Exception ex)
            {
                Events.RetryFailed(this, ex);
            }
        }

        static class Events
        {
            const string DateTimeFormat = "o";
            const string TimeSpanFormat = "c";
            const int IdStart = Routing.EventIds.EndpointExecutorFsm;

            static readonly ILogger Log = Routing.LoggerFactory.CreateLogger<EndpointExecutorFsm>();

            enum EventIds
            {
                StateEnter = IdStart,
                StateExit,
                StateTransition,
                Send,
                SendSuccess,
                SendFailureUnhandledException,
                SendFailure,
                SendNone,
                CounterFailure,
                Checkpoint,
                CheckpointSuccess,
                CheckpointFailure,
                Retry,
                RetryDelay,
                RetryFailed,
                Dead,
                DeadSuccess,
                DeadFailure,
                Die,
                PrepareForRevive,
                Revived,
                UpdateEndpoint,
                UpdateEndpointSuccess,
                UpdateEndpointFailure,
                CheckRetryInnerException
            }

            public static void StateEnter(EndpointExecutorFsm fsm)
            {
                Log.LogTrace((int)EventIds.StateEnter, "[StateEnter] Entered state <{0}>. {1}", fsm.state, GetContextString(fsm));
            }

            public static void StateExit(EndpointExecutorFsm fsm)
            {
                Log.LogTrace((int)EventIds.StateExit, "[StateExit] Exited state <{0}>. {1}", fsm.state, GetContextString(fsm));
            }

            public static void StateTransition(EndpointExecutorFsm fsm, State from, State to)
            {
                Log.LogTrace((int)EventIds.StateTransition, "[StateTransition] Transitioned from <{0}> to <{1}>. {2}", from, to, GetContextString(fsm));
            }

            public static void Send(EndpointExecutorFsm fsm, ICollection<IMessage> messages, ICollection<IMessage> admitted)
            {
                Log.LogDebug(
                    (int)EventIds.Send,
                    "[Send Sending began. BatchSize: {0}, AdmittedSize: {1}, MaxAdmittedOffset: {2}, {3}",
                    messages.Count,
                    admitted.Count,
                    admitted.Max(m => m.Offset),
                    GetContextString(fsm));
            }

            public static void SendSuccess(EndpointExecutorFsm fsm, ICollection<IMessage> admitted, ISinkResult<IMessage> result, Stopwatch stopwatch)
            {
                long latencyInMs = stopwatch.ElapsedMilliseconds;

                if (!Routing.PerfCounter.LogExternalWriteLatency(fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, true, latencyInMs, out string error))
                {
                    Log.LogError((int)EventIds.CounterFailure, "[LogExternalWriteLatencyCounterFailed] {0}", error);
                }

                Log.LogDebug(
                    (int)EventIds.SendSuccess,
                    "[SendSuccess] Sending succeeded. AdmittedSize: {0}, SuccessfulSize: {1}, FailedSize: {2}, InvalidSize: {3}, {4}",
                    admitted.Count,
                    result.Succeeded.Count,
                    result.Failed.Count,
                    result.InvalidDetailsList.Count,
                    GetContextString(fsm));
            }

            public static void SendFailureUnhandledException(EndpointExecutorFsm fsm, ICollection<IMessage> messages, Stopwatch stopwatch, Exception exception)
            {
                long latencyInMs = stopwatch.ElapsedMilliseconds;

                if (!Routing.PerfCounter.LogExternalWriteLatency(fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, false, latencyInMs, out string error))
                {
                    Log.LogError((int)EventIds.CounterFailure, "[LogExternalWriteLatencyCounterFailed] {0}", error);
                }

                Log.LogError((int)EventIds.SendFailureUnhandledException, exception, "[SendFailureUnhandledException] Unhandled exception.  FailedSize: {0}, {1}", messages.Count, GetContextString(fsm));
                LogUnhealthyEndpointOpMonError(fsm, FailureKind.InternalError);
            }

            public static void SendFailure(EndpointExecutorFsm fsm, ISinkResult<IMessage> result, Stopwatch stopwatch)
            {
                long latencyInMs = stopwatch.ElapsedMilliseconds;

                if (!Routing.PerfCounter.LogExternalWriteLatency(fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, false, latencyInMs, out string error))
                {
                    Log.LogError((int)EventIds.CounterFailure, "[LogExternalWriteLatencyCounterFailed] {0}", error);
                }

                SendFailureDetails failureDetails = result.SendFailureDetails.GetOrElse(DefaultSendFailureDetails);

                foreach (InvalidDetails<IMessage> invalidDetails in result.InvalidDetailsList)
                {
                    Routing.UserAnalyticsLogger.LogInvalidMessage(fsm.Endpoint.IotHubName, invalidDetails.Item, invalidDetails.FailureKind);
                }

                Log.LogWarning(
                    (int)EventIds.SendFailure,
                    failureDetails.RawException,
                    "[SendFailure] Sending failed. SuccessfulSize: {0}, FailedSize: {1}, InvalidSize: {2}, {3}",
                    result.Succeeded.Count,
                    result.Failed.Count,
                    result.InvalidDetailsList.Count,
                    GetContextString(fsm));

                LogUnhealthyEndpointOpMonError(fsm, failureDetails.FailureKind);
            }

            public static void SendNone(EndpointExecutorFsm fsm)
            {
                Log.LogDebug((int)EventIds.SendNone, "[SendNone] Admitted no messages. {0}", GetContextString(fsm));
            }

            public static void Checkpoint(EndpointExecutorFsm fsm, ISinkResult<IMessage> result)
            {
                Log.LogDebug(
                    (int)EventIds.Checkpoint,
                    "[Checkpoint] Checkpointing began. CheckpointOffset: {0}, SuccessfulSize: {1}, RemainingSize: {2}, {3}",
                    fsm.Status.CheckpointerStatus.Offset,
                    result.Succeeded.Count + result.InvalidDetailsList.Count,
                    result.Failed.Count,
                    GetContextString(fsm));
            }

            public static void CheckpointSuccess(EndpointExecutorFsm fsm, ISinkResult<IMessage> result)
            {
                Log.LogInformation(
                    (int)EventIds.CheckpointSuccess,
                    "[CheckpointSuccess] Checkpointing succeeded. CheckpointOffset: {0}, {1}",
                    fsm.Status.CheckpointerStatus.Offset,
                    GetContextString(fsm));

                IList<IMessage> invalidMessages = result.InvalidDetailsList.Select(d => d.Item).ToList();

                SetProcessingInternalCounters(fsm, "Success", result.Succeeded);
                SetProcessingInternalCounters(fsm, "Failure", result.Failed);
                SetProcessingInternalCounters(fsm, "Invalid", invalidMessages);

                SetSuccessfulEgressUserMetricCounter(fsm, result.Succeeded);
                SetInvalidEgressUserMetricCounter(fsm, invalidMessages);
            }

            public static void CheckpointFailure(EndpointExecutorFsm fsm, Exception ex)
            {
                Log.LogError(
                    (int)EventIds.CheckpointFailure,
                    ex,
                    "[CheckpointFailure] Checkpointing failed. CheckpointOffset: {0}, {1}",
                    fsm.Status.CheckpointerStatus.Offset,
                    GetContextString(fsm));
            }

            public static void CheckRetryInnerException(Exception ex, bool retry)
            {
                Log.LogDebug((int)EventIds.CheckRetryInnerException, ex, $"[CheckRetryInnerException] Decision to retry exception of type {ex.GetType()} is {retry}");
            }

            public static void Retry(EndpointExecutorFsm fsm)
            {
                DateTime next = fsm.systemTime.UtcNow.SafeAdd(fsm.Status.RetryPeriod);

                Log.LogDebug(
                    (int)EventIds.Retry,
                    "[Retry] Retrying. Retry.Attempts: {0}, Retry.Period: {1}, Retry.Next: {2}, {3}",
                    fsm.Status.RetryAttempts,
                    fsm.Status.RetryPeriod.ToString(TimeSpanFormat),
                    next.ToString(DateTimeFormat),
                    GetContextString(fsm));
            }

            public static void RetryDelay(EndpointExecutorFsm fsm)
            {
                DateTime next = fsm.systemTime.UtcNow.SafeAdd(fsm.Status.RetryPeriod);

                Log.LogDebug(
                    (int)EventIds.RetryDelay,
                    "[RetryDelay] Waiting to retry. Retry.Attempts: {0}, Retry.Period: {1}, Retry.Next: {2}, {3}",
                    fsm.Status.RetryAttempts,
                    fsm.Status.RetryPeriod.ToString(TimeSpanFormat),
                    next.ToString(DateTimeFormat),
                    GetContextString(fsm));
            }

            public static void RetryFailed(EndpointExecutorFsm fsm, Exception exception)
            {
                Log.LogError((int)EventIds.RetryFailed, exception, "[RetryFailed] Failed to retry. {0}", GetContextString(fsm));
            }

            public static void Dead(EndpointExecutorFsm fsm, ICollection<IMessage> messages)
            {
                Preconditions.CheckArgument(fsm.Status.LastFailedRevivalTime.HasValue);
                DateTime reviveAt = fsm.Status.LastFailedRevivalTime.GetOrElse(fsm.systemTime.UtcNow).SafeAdd(fsm.config.RevivePeriod);

                Log.LogWarning(
                    (int)EventIds.Dead,
                    "[Dead] Dropping {0} messages. BatchSize: {1}, LastFailedRevivalTime: {2}, UnhealthySince: {3}, ReviveAt: {4}, {5}",
                    messages.Count,
                    messages.Count,
                    fsm.Status.LastFailedRevivalTime.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat),
                    fsm.Status.UnhealthySince.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat),
                    reviveAt.ToString(DateTimeFormat),
                    GetContextString(fsm));
            }

            public static void DeadSuccess(EndpointExecutorFsm fsm, ICollection<IMessage> messages)
            {
                Preconditions.CheckArgument(fsm.Status.LastFailedRevivalTime.HasValue);

                CultureInfo culture = CultureInfo.InvariantCulture;
                DateTime reviveAt = fsm.Status.LastFailedRevivalTime.GetOrElse(fsm.systemTime.UtcNow).SafeAdd(fsm.config.RevivePeriod);

                Log.LogWarning(
                    (int)EventIds.DeadSuccess,
                    "[DeadSuccess] Dropped {0} messages. BatchSize: {1}, LastFailedRevivalTime: {2}, UnhealthySince: {3}, ReviveAt: {4}, {5}",
                    messages.Count,
                    messages.Count,
                    fsm.Status.LastFailedRevivalTime.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture),
                    fsm.Status.UnhealthySince.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture),
                    reviveAt.ToString(DateTimeFormat, culture),
                    GetContextString(fsm));

                SetProcessingInternalCounters(fsm, "Dropped", messages);
                SetDroppedEgressUserMetricCounter(fsm, messages);

                FailureKind failureKind = fsm.currentCheckpointCommand?.Result?.SendFailureDetails.GetOrElse(DefaultFailureDetails).FailureKind ?? FailureKind.InternalError;

                foreach (IMessage message in messages)
                {
                    Routing.UserAnalyticsLogger.LogDroppedMessage(fsm.Endpoint.IotHubName, message, fsm.Endpoint.Name, failureKind);
                }
            }

            public static void DeadFailure(EndpointExecutorFsm fsm, Exception ex)
            {
                Preconditions.CheckArgument(fsm.Status.LastFailedRevivalTime.HasValue);

                CultureInfo culture = CultureInfo.InvariantCulture;
                DateTime reviveAt = fsm.Status.LastFailedRevivalTime.GetOrElse(fsm.systemTime.UtcNow).SafeAdd(fsm.config.RevivePeriod);

                Log.LogError(
                    (int)EventIds.DeadFailure,
                    ex,
                    "[DeadFailure] Dropping messages failed. LastFailedRevivalTime: {0}, UnhealthySince: {1}, DeadTime:{2}, ReviveAt: {3}, {4}",
                    fsm.Status.LastFailedRevivalTime.ToString(),
                    fsm.Status.LastFailedRevivalTime.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture),
                    fsm.Status.UnhealthySince.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture),
                    reviveAt.ToString(DateTimeFormat, culture),
                    GetContextString(fsm));
            }

            public static void Die(EndpointExecutorFsm fsm)
            {
                Log.LogInformation((int)EventIds.Die, "[Die] Endpoint died. {0}", GetContextString(fsm));
                Routing.UserAnalyticsLogger.LogDeadEndpoint(fsm.Endpoint.IotHubName, fsm.Endpoint.Name);
            }

            public static void PrepareForRevive(EndpointExecutorFsm fsm)
            {
                Preconditions.CheckArgument(fsm.Status.LastFailedRevivalTime.HasValue);
                CultureInfo culture = CultureInfo.InvariantCulture;
                DateTime reviveAt = fsm.Status.LastFailedRevivalTime.GetOrElse(fsm.systemTime.UtcNow).SafeAdd(fsm.config.RevivePeriod);

                Log.LogInformation(
                    (int)EventIds.PrepareForRevive,
                    "[PrepareForRevive] Attempting to bring endpoint back. LastFailedRevivalTime: {0}, UnhealthySince: {1},  ReviveAt: {2}, {3}",
                    fsm.Status.LastFailedRevivalTime.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture),
                    fsm.Status.UnhealthySince.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture),
                    reviveAt.ToString(DateTimeFormat, culture),
                    GetContextString(fsm));
            }

            public static void Revived(EndpointExecutorFsm fsm)
            {
                Log.LogInformation((int)EventIds.Revived, "[Revived] Endpoint revived, {0}", GetContextString(fsm));
                Routing.UserAnalyticsLogger.LogHealthyEndpoint(fsm.Endpoint.IotHubName, fsm.Endpoint.Name);
            }

            public static void UpdateEndpoint(EndpointExecutorFsm fsm)
            {
                Log.LogInformation((int)EventIds.UpdateEndpoint, "[UpdateEndpoint] Updating endpoint began. {0}", GetContextString(fsm));
            }

            public static void UpdateEndpointSuccess(EndpointExecutorFsm fsm)
            {
                Log.LogInformation((int)EventIds.UpdateEndpointSuccess, "[UpdateEndpointSuccess] Updating endpoint succeeded. {0}", GetContextString(fsm));
            }

            public static void UpdateEndpointFailure(EndpointExecutorFsm fsm, Exception ex)
            {
                Log.LogError((int)EventIds.UpdateEndpointFailure, ex, "[UpdateEndpointFailure] Updating endpoint failed. {0}", GetContextString(fsm));
            }

            static void LogUnhealthyEndpointOpMonError(EndpointExecutorFsm fsm, FailureKind failureKind)
            {
                if (!fsm.lastFailedRevivalTime.HasValue &&
                    fsm.unhealthySince.GetOrElse(DateTime.MaxValue) < fsm.systemTime.UtcNow.Subtract(LogUserAnalyticsErrorOnUnhealthySince))
                {
                    Routing.UserAnalyticsLogger.LogUnhealthyEndpoint(fsm.Endpoint.IotHubName, fsm.Endpoint.Name, failureKind);
                }
            }

            static string GetContextString(EndpointExecutorFsm fsm)
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "EndpointId: {0}, EndpointName: {1}, CheckpointerId: {2}, State: {3}",
                    fsm.Status.Id,
                    fsm.Endpoint.Name,
                    fsm.Status.CheckpointerStatus.Id,
                    fsm.state);
            }

            static void SetProcessingInternalCounters(EndpointExecutorFsm fsm, string status, ICollection<IMessage> messages)
            {
                if (!messages.Any())
                {
                    return;
                }

                if (!Routing.PerfCounter.LogEventsProcessed(fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, status, messages.Count, out string error))
                {
                    Log.LogError((int)EventIds.CounterFailure, "[LogEventsProcessedCounterFailed] {0}", error);
                }

                double totalTimeMSecs = messages.Select(m => m.DequeuedTime).Aggregate(0D, (span, time) => span + (fsm.systemTime.UtcNow - time).TotalMilliseconds);
                long averageLatencyInMs = totalTimeMSecs < 0 ? 0L : (long)(totalTimeMSecs / messages.Count);

                if (!Routing.PerfCounter.LogEventProcessingLatency(fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, status, averageLatencyInMs, out error))
                {
                    Log.LogError((int)EventIds.CounterFailure, "[LogEventProcessingLatencyCounterFailed] {0}", error);
                }

                double messageE2EProcessingLatencyTotalMSecs = messages.Select(m => m.EnqueuedTime).Aggregate(0D, (span, time) => span + (fsm.systemTime.UtcNow - time).TotalMilliseconds);
                long averageE2ELatencyInMs = messageE2EProcessingLatencyTotalMSecs < 0 ? 0L : (long)(messageE2EProcessingLatencyTotalMSecs / messages.Count);

                if (!Routing.PerfCounter.LogE2EEventProcessingLatency(fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, status, averageE2ELatencyInMs, out error))
                {
                    Log.LogError((int)EventIds.CounterFailure, "[LogE2EEventProcessingLatencyCounterFailed] {0}", error);
                }
            }

            static void SetSuccessfulEgressUserMetricCounter(EndpointExecutorFsm fsm, ICollection<IMessage> messages)
            {
                if (!messages.Any())
                {
                    return;
                }

                foreach (IGrouping<Type, IMessage> group in messages.GroupBy(m => m.MessageSource.GetType()).Where(g => g.Any()))
                {
                    int count = group.Count();
                    Routing.UserMetricLogger.LogEgressMetric(count, fsm.Endpoint.IotHubName, MessageRoutingStatus.Success, count > 0 ? group.First().ToString() : group.Key.Name);
                }

                // calculate average latency
                double totalTimeMSecs = messages.Select(m => m.EnqueuedTime).Aggregate(0D, (span, time) => span + (fsm.systemTime.UtcNow - time).TotalMilliseconds);
                long averageLatencyInMs = totalTimeMSecs < 0 ? 0L : (long)(totalTimeMSecs / messages.Count);

                fsm.Endpoint.LogUserMetrics(messages.Count, averageLatencyInMs);
            }

            static void SetInvalidEgressUserMetricCounter(EndpointExecutorFsm fsm, IEnumerable<IMessage> messages)
            {
                foreach (IGrouping<Type, IMessage> group in messages.GroupBy(m => m.MessageSource.GetType()).Where(g => g.Any()))
                {
                    int count = group.Count();
                    Routing.UserMetricLogger.LogEgressMetric(count, fsm.Endpoint.IotHubName, MessageRoutingStatus.Invalid, count > 0 ? group.First().ToString() : group.Key.Name);
                }
            }

            static void SetDroppedEgressUserMetricCounter(EndpointExecutorFsm fsm, IEnumerable<IMessage> messages)
            {
                foreach (IGrouping<Type, IMessage> group in messages.GroupBy(m => m.MessageSource.GetType()).Where(g => g.Any()))
                {
                    int count = group.Count();
                    Routing.UserMetricLogger.LogEgressMetric(count, fsm.Endpoint.IotHubName, MessageRoutingStatus.Dropped, count > 0 ? group.First().ToString() : group.Key.Name);
                }
            }
        }
    }
}
