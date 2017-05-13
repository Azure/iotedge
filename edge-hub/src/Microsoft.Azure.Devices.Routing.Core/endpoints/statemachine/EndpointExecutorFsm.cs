// ---------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ---------------------------------------------------------------

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
    using Microsoft.Azure.Devices.Routing.Core.Checkpointers;
    using Microsoft.Azure.Devices.Routing.Core.Util;
    using Microsoft.Azure.Devices.Routing.Core.TransientFaultHandling;
    using AsyncLock = Microsoft.Azure.Devices.Routing.Core.Util.Concurrency.AsyncLock;

    public class EndpointExecutorFsm : IDisposable
    {
        static readonly SendFailureDetails DefaultSendFailureDetails = new SendFailureDetails(FailureKind.None, new Exception());
        static readonly SendFailureDetails DefaultFailureDetails = new SendFailureDetails(FailureKind.InternalError, new Exception());
        static readonly TimeSpan LogUserAnalyticsErrorOnUnhealthySince = TimeSpan.FromMinutes(10);

        volatile State state;
        volatile int retryAttempts;
        readonly EndpointExecutorConfig config;
        SendMessage currentSendCommand;
        Checkpoint currentCheckpointCommand;
        Option<DateTime> lastFailedRevivalTime;
        Option<DateTime> unhealthySince;
        volatile IProcessor processor;
        TimeSpan retryPeriod;
        readonly Timer retryTimer;
        readonly AsyncLock sync = new AsyncLock();
        static readonly ICollection<IMessage> EmptyMessages = ImmutableList<IMessage>.Empty;

        public Endpoint Endpoint => this.processor.Endpoint;

        public ICheckpointer Checkpointer { get; }

        public EndpointExecutorStatus Status =>
            new EndpointExecutorStatus(this.Endpoint.Id, this.state, this.retryAttempts, this.retryPeriod, this.lastFailedRevivalTime, this.unhealthySince, new CheckpointerStatus(this.Checkpointer.Id, this.Checkpointer.Offset, this.Checkpointer.Proposed));

        public EndpointExecutorFsm(Endpoint endpoint, ICheckpointer checkpointer, EndpointExecutorConfig config)
        {
            this.processor = Preconditions.CheckNotNull(endpoint).CreateProcessor();
            this.Checkpointer = Preconditions.CheckNotNull(checkpointer);
            this.config = Preconditions.CheckNotNull(config);
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
            Debug.Assert(this.state == State.Closed);
            if (disposing)
            {
                this.Checkpointer.Dispose();
                this.retryTimer.Dispose();
                this.sync.Dispose();
            }
        }

        bool ShouldRetry(Exception exception, out TimeSpan retryAfter)
        {
            retryAfter = TimeSpan.Zero;
            ITransientErrorDetectionStrategy detectionStrategy = this.processor.ErrorDetectionStrategy;
            ShouldRetry shouldRetry = this.config.RetryStrategy.GetShouldRetry();
            return detectionStrategy.IsTransient(exception) && shouldRetry(this.retryAttempts, exception, out retryAfter);
        }

        static readonly IReadOnlyDictionary<State, StateActions> Actions = new Dictionary<State, StateActions>
        {
            { State.Idle,               new StateActions(EnterIdleAsync,              StateActions.NullAction) },
            { State.Sending,            new StateActions(EnterSendingAsync,           StateActions.NullAction) },
            { State.Checkpointing,      new StateActions(EnterCheckpointingAsync,     StateActions.NullAction) },
            { State.Failing,            new StateActions(EnterFailingAsync,           ExitFailingAsync) },
            { State.DeadCheckpointing,  new StateActions(EnterDeadCheckpointingAsync, ExitDeadCheckpointingAsync) },            
            { State.DeadIdle,           new StateActions(StateActions.NullAction,     StateActions.NullAction) },
            { State.DeadProcess,        new StateActions(EnterProcessDeadAsync,       StateActions.NullAction) },
            { State.Closed,             new StateActions(EnterClosedAsync,            StateActions.NullAction) }
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

            Stopwatch stopwatch = Stopwatch.StartNew();
            Events.UpdateEndpoint(thisPtr);

            try
            {
                var update = (UpdateEndpoint)command;
                await thisPtr.processor.CloseAsync(CancellationToken.None);
                thisPtr.processor = update.Endpoint.CreateProcessor();
                Events.UpdateEndpointSuccess(thisPtr, stopwatch);
            }
            catch (Exception ex)
            {
                Events.UpdateEndpointFailure(thisPtr, ex, stopwatch);

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

        static Task DieAsync(EndpointExecutorFsm thisPtr, ICommand command)
        {
            Preconditions.CheckNotNull(thisPtr);
            Preconditions.CheckNotNull(command);
            Preconditions.CheckArgument(command is Die);

            thisPtr.lastFailedRevivalTime = Option.Some(DateTime.UtcNow);
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

            try
            {
                Preconditions.CheckNotNull(thisPtr.currentSendCommand);

                messages = thisPtr.currentSendCommand.Messages.Where(thisPtr.Checkpointer.Admit).ToArray();
                if (messages.Count > 0)
                {
                    ISinkResult<IMessage> result;
                    Events.Send(thisPtr, thisPtr.currentSendCommand.Messages, messages);
                    using (var cts = new CancellationTokenSource(thisPtr.config.Timeout))
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
                            ? Option.Some(DateTime.UtcNow)
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
                thisPtr.unhealthySince = !thisPtr.unhealthySince.HasValue ? Option.Some(DateTime.UtcNow) : thisPtr.unhealthySince;
                next = Commands.Fail(retryAfter);
            }
            catch (Exception ex)
            {
                Events.SendFailureUnhandledException(thisPtr, messages, stopwatch, ex);
                thisPtr.unhealthySince = !thisPtr.unhealthySince.HasValue ? Option.Some(DateTime.UtcNow) : thisPtr.unhealthySince;
                next = thisPtr.config.ThrowOnDead ? (ICommand)Commands.Throw(ex) : Commands.Die;
            }
            await RunInternalAsync(thisPtr, next);
        }

        static async Task EnterDeadCheckpointingAsync(EndpointExecutorFsm thisPtr)
        {
            ICommand next;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                Preconditions.CheckNotNull(thisPtr.currentCheckpointCommand);
                using (var cts = new CancellationTokenSource(thisPtr.config.Timeout))
                {
                    ISinkResult<IMessage> result = thisPtr.currentCheckpointCommand.Result;
                    Events.Checkpoint(thisPtr, result);
                    await thisPtr.Checkpointer.CommitAsync(result.Succeeded, EmptyMessages, thisPtr.lastFailedRevivalTime, thisPtr.unhealthySince, cts.Token);
                    Events.CheckpointSuccess(thisPtr, result, stopwatch);
                }

                next = Commands.DeadSucceed;
                Events.DeadSuccess(thisPtr, thisPtr.currentCheckpointCommand.Result.Succeeded, stopwatch);
            }
            catch (Exception ex)
            {
                Events.CheckpointFailure(thisPtr, ex, stopwatch);
                if (thisPtr.config.ThrowOnDead)
                {
                    next = Commands.Throw(ex);
                }
                else
                {
                    // We wont retry to checkpoint again and hence succeed in this case too
                    next = Commands.DeadSucceed;
                }
            }

            await RunInternalAsync(thisPtr, next);
        }

        static async Task EnterCheckpointingAsync(EndpointExecutorFsm thisPtr)
        {
            ICommand next;
            Stopwatch stopwatch = Stopwatch.StartNew();
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
                        Events.CheckpointSuccess(thisPtr, result, stopwatch);
                    }
                }

                next = EnterCheckpointingHelper(thisPtr);
            }
            catch (Exception ex)
            {
                Events.CheckpointFailure(thisPtr, ex, stopwatch);
                if (thisPtr.config.ThrowOnDead)
                {
                    next = Commands.Throw(ex);
                }
                else
                {
                    next = EnterCheckpointingHelper(thisPtr);
                }
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
                // PartialFailures should always have an exception object filled out
                Exception innerException = thisPtr.currentCheckpointCommand.Result.SendFailureDetails.GetOrElse(DefaultSendFailureDetails).RawException;
                Debug.Assert(!innerException.Equals(DefaultSendFailureDetails.RawException));

                // 1. Change Messages to be sent to failedMessages
                // 2. Change the Command type to Fail or Dead depending on retry
                thisPtr.currentSendCommand = thisPtr.currentSendCommand.Copy(thisPtr.currentCheckpointCommand.Result.Failed);

                TimeSpan retryAfter;
                if (thisPtr.ShouldRetry(innerException, out retryAfter))
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

            Stopwatch stopwatch = Stopwatch.StartNew();
            TimeSpan deadFor = DateTime.UtcNow - thisPtr.lastFailedRevivalTime.GetOrElse(DateTime.UtcNow);

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
                    SendFailureDetails persistingFailureDetails = thisPtr.currentCheckpointCommand?.Result?.SendFailureDetails.GetOrElse(null);
                    await RunInternalAsync(thisPtr, Commands.Checkpoint(new SinkResult<IMessage>(tocheckpoint, persistingFailureDetails)));
                }
                catch (Exception ex)
                {
                    Events.DeadFailure(thisPtr, ex, stopwatch);
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

        static class Events
        {
            const string Source = nameof(EndpointExecutorFsm);
            const string DeviceId = null;
            const string DateTimeFormat = "o";
            const string TimeSpanFormat = "c";

            //static readonly ILog Log = Routing.Log;

            public static void StateEnter(EndpointExecutorFsm fsm)
            {
                //Log.Verbose(nameof(StateEnter), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Entered state <{0}>. {1}", fsm.state, GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void StateExit(EndpointExecutorFsm fsm)
            {
                //Log.Verbose(nameof(StateExit), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Exited state <{0}>. {1}", fsm.state, GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void StateTransition(EndpointExecutorFsm fsm, State from, State to)
            {
                //Log.Verbose(nameof(StateTransition), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Transitioned from <{0}> to <{1}>. {2}", from, to, GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void Send(EndpointExecutorFsm fsm, ICollection<IMessage> messages, ICollection<IMessage> admitted)
            {
                //Log.Informational(nameof(Send), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Sending began. BatchSize: {0}, AdmittedSize: {1}, MaxAdmittedOffset: {2}, {3}",
                //        messages.Count, admitted.Count, admitted.Max(m => m.Offset), GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void SendSuccess(EndpointExecutorFsm fsm, ICollection<IMessage> admitted, ISinkResult<IMessage> result, Stopwatch stopwatch)
            {
                long latencyInMs = stopwatch.ElapsedMilliseconds;

                string error;
                if (!Routing.PerfCounter.LogExternalWriteLatency(fsm.Endpoint.IotHubName,
                    fsm.Endpoint.Name,
                    fsm.Endpoint.Type,
                    true,
                    latencyInMs,
                    out error))
                {
                    //Log.Error("LogExternalWriteLatencyCounterFailed", Source, error);
                }

                //Log.Informational(nameof(SendSuccess), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Sending succeeded. AdmittedSize: {0}, SuccessfulSize: {1}, FailedSize: {2}, InvalidSize: {3}, {4}",
                //        admitted.Count, result.Succeeded.Count, result.Failed.Count, result.InvalidDetailsList.Count, GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId, latencyInMs.ToString(CultureInfo.InvariantCulture));
            }

            public static void SendFailureUnhandledException(EndpointExecutorFsm fsm, ICollection<IMessage> messages, Stopwatch stopwatch, Exception unhandledException)
            {
                //long latencyInMs = stopwatch.ElapsedMilliseconds;

                //string error;
                //if (!Routing.PerfCounter.LogExternalWriteLatency(fsm.Endpoint.IotHubName,
                //    fsm.Endpoint.Name,
                //    fsm.Endpoint.Type,
                //    false,
                //    latencyInMs,
                //    out error))
                //{
                //    Log.Error("LogExternalWriteLatencyCounterFailed", Source, error);
                //}

                //Log.Error(nameof(SendFailureUnhandledException), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Unhandled exception.  FailedSize: {0}, {1}", messages.Count, GetContextString(fsm)),
                //    unhandledException, fsm.Endpoint.IotHubName, DeviceId, latencyInMs.ToString(CultureInfo.InvariantCulture));

                //LogUnhealthyEndpointOpMonError(fsm, FailureKind.InternalError);
            }

            public static void SendFailure(EndpointExecutorFsm fsm, ISinkResult<IMessage> result, Stopwatch stopwatch)
            {
                //long latencyInMs = stopwatch.ElapsedMilliseconds;

                //string error;
                //if (!Routing.PerfCounter.LogExternalWriteLatency(fsm.Endpoint.IotHubName,
                //    fsm.Endpoint.Name,
                //    fsm.Endpoint.Type,
                //    false,
                //    latencyInMs,
                //    out error))
                //{
                //    Log.Error("LogExternalWriteLatencyCounterFailed", Source, error);
                //}

                //SendFailureDetails failureDetails = result.SendFailureDetails.GetOrElse(DefaultSendFailureDetails);

                //foreach (InvalidDetails<IMessage> invalidDetails in result.InvalidDetailsList)
                //{
                //    Routing.UserAnalyticsLogger.LogInvalidMessage(fsm.Endpoint.IotHubName, invalidDetails.Item, invalidDetails.FailureKind);
                //}

                //Log.Warning(nameof(SendFailure), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Sending failed. SuccessfulSize: {0}, FailedSize: {1}, InvalidSize: {2}, {3}",
                //        result.Succeeded.Count, result.Failed.Count, result.InvalidDetailsList.Count, GetContextString(fsm)),
                //    failureDetails.RawException, fsm.Endpoint.IotHubName, DeviceId, latencyInMs.ToString(CultureInfo.InvariantCulture));

                //LogUnhealthyEndpointOpMonError(fsm, failureDetails.FailureKind);
            }

            public static void SendNone(EndpointExecutorFsm fsm)
            {
                //Log.Informational(nameof(SendNone), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Admitted no messages. {0}", GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void Checkpoint(EndpointExecutorFsm fsm, ISinkResult<IMessage> result)
            {
                //Log.Informational(nameof(Checkpoint), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Checkpointing began. CheckpointOffset: {0}, SuccessfulSize: {1}, RemainingSize: {2}, {3}",
                //        fsm.Status.CheckpointerStatus.Offset, result.Succeeded.Count + result.InvalidDetailsList.Count, result.Failed.Count, GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void CheckpointSuccess(EndpointExecutorFsm fsm, ISinkResult<IMessage> result, Stopwatch stopwatch)
            {
                //Log.Informational(nameof(CheckpointSuccess), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Checkpointing succeeded. CheckpointOffset: {0}, {1}", fsm.Status.CheckpointerStatus.Offset, GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));

                //IList<IMessage> invalidMessages = result.InvalidDetailsList.Select(d => d.Item).ToList();

                //SetProcessingInternalCounters(fsm, "Success", result.Succeeded);
                //SetProcessingInternalCounters(fsm, "Failure", result.Failed);
                //SetProcessingInternalCounters(fsm, "Invalid", invalidMessages);

                //SetSuccessfulEgressUserMetricCounter(fsm, result.Succeeded);
                //SetInvalidEgressUserMetricCounter(fsm, invalidMessages);
            }

            public static void CheckpointFailure(EndpointExecutorFsm fsm, Exception ex, Stopwatch stopwatch)
            {
                //Log.Error(nameof(CheckpointFailure), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Checkpointing failed. CheckpointOffset: {0}, {1}", fsm.Status.CheckpointerStatus.Offset, GetContextString(fsm)),
                //    ex, fsm.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void Retry(EndpointExecutorFsm fsm)
            {
                //CultureInfo culture = CultureInfo.InvariantCulture;
                //DateTime next = DateTime.UtcNow.SafeAdd(fsm.Status.RetryPeriod);

                //Log.Informational(nameof(Retry), Source,
                //    string.Format(culture, "Retrying. Retry.Attempts: {0}, Retry.Period: {1}, Retry.Next: {2}, {3}",
                //        fsm.Status.RetryAttempts, fsm.Status.RetryPeriod.ToString(TimeSpanFormat, culture), next.ToString(DateTimeFormat, culture), GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void RetryDelay(EndpointExecutorFsm fsm)
            {
                //CultureInfo culture = CultureInfo.InvariantCulture;
                //DateTime next = DateTime.UtcNow.SafeAdd(fsm.Status.RetryPeriod);

                //Log.Informational(nameof(RetryDelay), Source,
                //    string.Format(culture, "Waiting to retry. Retry.Attempts: {0}, Retry.Period: {1}, Retry.Next: {2}, {3}",
                //        fsm.Status.RetryAttempts, fsm.Status.RetryPeriod.ToString(TimeSpanFormat, culture), next.ToString(DateTimeFormat, culture), GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void RetryFailed(EndpointExecutorFsm fsm, Exception exception)
            {
                //Log.Error(nameof(RetryFailed), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Failed to retry. {0}", GetContextString(fsm)),
                //    exception, fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void Dead(EndpointExecutorFsm fsm, ICollection<IMessage> messages)
            {
                //Preconditions.CheckArgument(fsm.Status.LastFailedRevivalTime.HasValue);

                //CultureInfo culture = CultureInfo.InvariantCulture;
                //DateTime reviveAt = fsm.Status.LastFailedRevivalTime.GetOrElse(DateTime.UtcNow).SafeAdd(fsm.config.RevivePeriod);

                //Log.Warning(nameof(Dead), Source,
                //    string.Format(culture, "Dropping {0} messages. BatchSize: {1}, LastFailedRevivalTime: {2}, UnhealthySince: {3}, ReviveAt: {4}, {5}",
                //        messages.Count, messages.Count, fsm.Status.LastFailedRevivalTime.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture), fsm.Status.UnhealthySince.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture), reviveAt.ToString(DateTimeFormat, culture), GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void DeadSuccess(EndpointExecutorFsm fsm, ICollection<IMessage> messages, Stopwatch stopwatch)
            {
                //Preconditions.CheckArgument(fsm.Status.LastFailedRevivalTime.HasValue);

                //CultureInfo culture = CultureInfo.InvariantCulture;
                //DateTime reviveAt = fsm.Status.LastFailedRevivalTime.GetOrElse(DateTime.UtcNow).SafeAdd(fsm.config.RevivePeriod);

                //Log.Warning(nameof(DeadSuccess), Source,
                //    string.Format(culture, "Dropped {0} messages. BatchSize: {1}, LastFailedRevivalTime: {2}, UnhealthySince: {3}, ReviveAt: {4}, {5}",
                //        messages.Count, messages.Count, fsm.Status.LastFailedRevivalTime.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture), fsm.Status.UnhealthySince.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture), reviveAt.ToString(DateTimeFormat, culture), GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(culture));

                //SetProcessingInternalCounters(fsm, "Dropped", messages);
                //SetDroppedEgressUserMetricCounter(fsm, messages);

                //var failureKind = FailureKind.InternalError;
                //if (fsm.currentCheckpointCommand?.Result?.SendFailureDetails != null)
                //{
                //    failureKind = fsm.currentCheckpointCommand.Result.SendFailureDetails.GetOrElse(DefaultFailureDetails).FailureKind;
                //}

                //foreach (IMessage message in messages)
                //{
                //    Routing.UserAnalyticsLogger.LogDroppedMessage(fsm.Endpoint.IotHubName, message, fsm.Endpoint.Name, failureKind);
                //}
            }

            public static void DeadFailure(EndpointExecutorFsm fsm, Exception ex, Stopwatch stopwatch)
            {
                //Preconditions.CheckArgument(fsm.Status.LastFailedRevivalTime.HasValue);

                //CultureInfo culture = CultureInfo.InvariantCulture;
                //DateTime reviveAt = fsm.Status.LastFailedRevivalTime.GetOrElse(DateTime.UtcNow).SafeAdd(fsm.config.RevivePeriod);

                //Log.Error(nameof(DeadFailure), Source,
                //    string.Format(culture, "Dropping messages failed. LastFailedRevivalTime: {0}, UnhealthySince: {1}, DeadTime:{2}, ReviveAt: {3}, {4}",
                //        fsm.Status.LastFailedRevivalTime.ToString(), fsm.Status.LastFailedRevivalTime.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture), fsm.Status.UnhealthySince.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture), reviveAt.ToString(DateTimeFormat, culture), GetContextString(fsm)),
                //    ex, fsm.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(culture));
            }

            public static void Die(EndpointExecutorFsm fsm)
            {
                //CultureInfo culture = CultureInfo.InvariantCulture;
                //Log.Informational(nameof(Die), Source,
                //    string.Format(culture, "Endpoint died. {0}", GetContextString(fsm)),
                //    null, fsm.Endpoint.IotHubName, DeviceId, null);

                //Routing.UserAnalyticsLogger.LogDeadEndpoint(fsm.Endpoint.IotHubName, fsm.Endpoint.Name);
            }

            public static void PrepareForRevive(EndpointExecutorFsm fsm)
            {
                //Preconditions.CheckArgument(fsm.Status.LastFailedRevivalTime.HasValue);
                //CultureInfo culture = CultureInfo.InvariantCulture;
                //DateTime reviveAt = fsm.Status.LastFailedRevivalTime.GetOrElse(DateTime.UtcNow).SafeAdd(fsm.config.RevivePeriod);

                //Log.Informational(nameof(PrepareForRevive), Source,
                //    string.Format(culture, "Attempting to bring endpoint back. LastFailedRevivalTime: {0}, UnhealthySince: {1},  ReviveAt: {2}, {3}",
                //        fsm.Status.LastFailedRevivalTime.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture), fsm.Status.UnhealthySince.GetOrElse(Checkpointers.Checkpointer.DateTimeMinValue).ToString(DateTimeFormat, culture), reviveAt.ToString(DateTimeFormat, culture), GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void Revived(EndpointExecutorFsm fsm)
            {
                //CultureInfo culture = CultureInfo.InvariantCulture;

                //Log.Informational(nameof(PrepareForRevive), Source,
                //    string.Format(culture, "Endpoint revived, {0}", GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);

                //Routing.UserAnalyticsLogger.LogHealthyEndpoint(fsm.Endpoint.IotHubName, fsm.Endpoint.Name);
            }

            public static void UpdateEndpoint(EndpointExecutorFsm fsm)
            {
                //Log.Informational(nameof(UpdateEndpoint), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Updating endpoint began. {0}", GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId);
            }

            public static void UpdateEndpointSuccess(EndpointExecutorFsm fsm, Stopwatch stopwatch)
            {
                //Log.Informational(nameof(UpdateEndpointSuccess), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Updating endpoint succeeded. {0}", GetContextString(fsm)),
                //    fsm.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            public static void UpdateEndpointFailure(EndpointExecutorFsm fsm, Exception ex, Stopwatch stopwatch)
            {
                //Log.Error(nameof(UpdateEndpointFailure), Source,
                //    string.Format(CultureInfo.InvariantCulture, "Updating endpoint failed. {0}", GetContextString(fsm)),
                //    ex, fsm.Endpoint.IotHubName, DeviceId, stopwatch.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            }

            static void LogUnhealthyEndpointOpMonError(EndpointExecutorFsm fsm, FailureKind failureKind)
            {
                if (!fsm.lastFailedRevivalTime.HasValue &&
                    fsm.unhealthySince.GetOrElse(DateTime.MaxValue) < DateTime.UtcNow.Subtract(LogUserAnalyticsErrorOnUnhealthySince))
                {
                    Routing.UserAnalyticsLogger.LogUnhealthyEndpoint(fsm.Endpoint.IotHubName, fsm.Endpoint.Name, failureKind);
                }
            }

            static string GetContextString(EndpointExecutorFsm fsm)
            {
                return string.Format(CultureInfo.InvariantCulture, "EndpointId: {0}, EndpointName: {1}, CheckpointerId: {2}, State: {3}",
                    fsm.Status.Id, fsm.Endpoint.Name, fsm.Status.CheckpointerStatus.Id, fsm.state);
            }

            static void SetProcessingInternalCounters(EndpointExecutorFsm fsm, string status, ICollection<IMessage> messages)
            {
                //if (!messages.Any())
                //{
                //    return;
                //}

                //string error;
                //if (!Routing.PerfCounter.LogEventsProcessed(
                //    fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, status,
                //    messages.Count, out error))
                //{
                //    Log.Error("LogEventsProcessedCounterFailed", Source, error);
                //}

                //TimeSpan totalTime = messages.Select(m => m.DequeuedTime).Aggregate(TimeSpan.Zero, (span, time) => span + (DateTime.UtcNow - time));
                //long averageLatencyInMs = totalTime < TimeSpan.Zero ? 0L : (long)(totalTime.TotalMilliseconds / messages.Count);

                //if (!Routing.PerfCounter.LogEventProcessingLatency(
                //    fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, status,
                //    averageLatencyInMs, out error))
                //{
                //    Log.Error("LogEventProcessingLatencyCounterFailed", Source, error);
                //}

                //TimeSpan messageE2EProcessingLatencyTotal = messages.Select(m => m.EnqueuedTime).Aggregate(TimeSpan.Zero, (span, time) => span + (DateTime.UtcNow - time));
                //long averageE2ELatencyInMs = messageE2EProcessingLatencyTotal < TimeSpan.Zero ? 0L : (long)(messageE2EProcessingLatencyTotal.TotalMilliseconds / messages.Count);

                //if (!Routing.PerfCounter.LogE2EEventProcessingLatency(
                //    fsm.Endpoint.IotHubName, fsm.Endpoint.Name, fsm.Endpoint.Type, status,
                //    averageE2ELatencyInMs,
                //    out error))
                //{
                //    Log.Error("LogE2EEventProcessingLatencyCounterFailed", Source, error);
                //}
            }

            static void SetSuccessfulEgressUserMetricCounter(EndpointExecutorFsm fsm, ICollection<IMessage> messages)
            {
                if (!messages.Any())
                {
                    return;
                }

                foreach (var group in messages.GroupBy(m => m.MessageSource).Where(g => g.Any()))
                {
                    Routing.UserMetricLogger.LogEgressMetric(group.Count(), fsm.Endpoint.IotHubName, MessageRoutingStatus.Success, group.Key);
                }

                // calculate average latency
                TimeSpan totalTime = messages.Select(m => m.EnqueuedTime).Aggregate(TimeSpan.Zero, (span, time) => span + (DateTime.UtcNow - time));
                long averageLatencyInMs = totalTime < TimeSpan.Zero ? 0L : (long)(totalTime.TotalMilliseconds / messages.Count);

                fsm.Endpoint.LogUserMetrics(messages.Count, averageLatencyInMs);
            }

            static void SetInvalidEgressUserMetricCounter(EndpointExecutorFsm fsm, IEnumerable<IMessage> messages)
            {
                foreach (var group in messages.GroupBy(m => m.MessageSource).Where(g => g.Any()))
                {
                    Routing.UserMetricLogger.LogEgressMetric(group.Count(), fsm.Endpoint.IotHubName, MessageRoutingStatus.Invalid, group.Key);
                }
            }

            static void SetDroppedEgressUserMetricCounter(EndpointExecutorFsm fsm, IEnumerable<IMessage> messages)
            {
                foreach (var group in messages.GroupBy(m => m.MessageSource).Where(g => g.Any()))
                {
                    Routing.UserMetricLogger.LogEgressMetric(group.Count(), fsm.Endpoint.IotHubName, MessageRoutingStatus.Dropped, group.Key);
                }
            }
        }
    }
}
