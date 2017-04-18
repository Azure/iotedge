// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Commands
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class LoggingCommandFactory : ICommandFactory
    {
        readonly ICommandFactory underlying;
        readonly ILogger logger;

        public LoggingCommandFactory(ICommandFactory underlying, ILoggerFactory loggerFactory)
        {
            this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
            this.logger = Preconditions.CheckNotNull(loggerFactory, nameof(loggerFactory)).CreateLogger<LoggingCommandFactory>();
        }

        public ICommand Create(IModule module) => new LoggingCommand(this.underlying.Create(module), "create", this.logger);

        public ICommand Update(IModule current, IModule next) => new LoggingCommand(this.underlying.Update(current, next), "update", this.logger);

        public ICommand Remove(IModule module) => new LoggingCommand(this.underlying.Remove(module), "remove", this.logger);

        public ICommand Start(IModule module) => new LoggingCommand(this.underlying.Start(module), "start", this.logger);

        public ICommand Stop(IModule module) => new LoggingCommand(this.underlying.Stop(module), "stop", this.logger);

        class LoggingCommand : ICommand
        {
            readonly string operation;
            readonly ICommand underlying;
            readonly ILogger logger;

            public LoggingCommand(ICommand underlying, string operation, ILogger logger)
            {
                this.underlying = Preconditions.CheckNotNull(underlying, nameof(underlying));
                this.operation = Preconditions.CheckNotNull(operation, nameof(operation));
                this.logger = Preconditions.CheckNotNull(logger, nameof(logger));
            }

            public async Task ExecuteAsync(CancellationToken token)
            {
                try
                {
                    Events.Execute(this.logger, this.underlying);
                    await this.underlying.ExecuteAsync(token);
                    Events.ExecuteSuccess(this.logger, this.operation);
                }
                catch (Exception ex)
                {
                    Events.ExecuteFailure(this.logger, this.operation, ex);
                    throw;
                }
            }

            public async Task UndoAsync(CancellationToken token)
            {
                try
                {
                    Events.Undo(this.logger, this.underlying);
                    await this.underlying.UndoAsync(token);
                    Events.UndoSuccess(this.logger, this.operation);
                }
                catch (Exception ex)
                {
                    Events.UndoFailure(this.logger, this.operation, ex);
                    throw;
                }
            }

            static class Events
            {
                const int ExecuteId = 1;
                const int ExecuteSuccessId = 2;
                const int ExecuteFailureId = 3;

                const int UndoId = 4;
                const int UndoSuccessId = 5;
                const int UndoFailureId = 6;

                public static void Execute(ILogger logger, ICommand command)
                {
                    logger.LogInformation(ExecuteId, "Executing command: {0}", command);
                }

                public static void ExecuteSuccess(ILogger logger, string operation)
                {
                    logger.LogInformation(ExecuteSuccessId, "Executing command for operation [{0}] succeeded.", operation);
                }

                public static void ExecuteFailure(ILogger logger, string operation, Exception exception)
                {
                    logger.LogError(ExecuteFailureId, exception, "Executing command for operation [{0}] succeeded.", operation);
                }

                public static void Undo(ILogger logger, ICommand command)
                {
                    logger.LogInformation(UndoId, "Undoing command: {0}", command);
                }

                public static void UndoSuccess(ILogger logger, string operation)
                {
                    logger.LogInformation(UndoSuccessId, "Undoing command for operation [{0}] succeeded.", operation);
                }

                public static void UndoFailure(ILogger logger, string operation, Exception exception)
                {
                    logger.LogError(UndoFailureId, exception, "Undoing command for operation [{0}] succeeded.", operation);
                }
            }
        }
    }
}