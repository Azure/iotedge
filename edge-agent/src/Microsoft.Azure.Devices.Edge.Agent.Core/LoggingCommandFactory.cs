// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
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

        public async Task<ICommand> CreateAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) => new LoggingCommand(await this.underlying.CreateAsync(module, runtimeInfo), "create", this.logger);

        public async Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next, IRuntimeInfo runtimeInfo) => new LoggingCommand(await this.underlying.UpdateAsync(current, next, runtimeInfo), "update", this.logger);

        public async Task<ICommand> UpdateEdgeAgentAsync(IModuleWithIdentity module, IRuntimeInfo runtimeInfo) => new LoggingCommand(await this.underlying.UpdateEdgeAgentAsync(module, runtimeInfo), "update Edge Agent", this.logger);

        public async Task<ICommand> RemoveAsync(IModule module) => new LoggingCommand(await this.underlying.RemoveAsync(module), "remove", this.logger);

        public async Task<ICommand> StartAsync(IModule module) => new LoggingCommand(await this.underlying.StartAsync(module), "start", this.logger);

        public async Task<ICommand> StopAsync(IModule module) => new LoggingCommand(await this.underlying.StopAsync(module), "stop", this.logger);

        public async Task<ICommand> RestartAsync(IModule module) => new LoggingCommand(await this.underlying.RestartAsync(module), "restart", this.logger);

        public async Task<ICommand> WrapAsync(ICommand command) => new LoggingCommand(await this.underlying.WrapAsync(command), command.ToString(), this.logger);

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

            // Since we are simply wrapping another command we have no independent identifier
            // of our own. We delegate to our underlying command.
            public string Id => this.underlying.Id;

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

            public string Show() => this.underlying.Show();

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
                    logger.LogInformation(ExecuteId, "Executing command: {0}", command.Show());
                }

                public static void ExecuteSuccess(ILogger logger, string operation)
                {
                    logger.LogDebug(ExecuteSuccessId, "Executing command for operation [{0}] succeeded.", operation);
                }

                public static void ExecuteFailure(ILogger logger, string operation, Exception exception)
                {
                    logger.LogError(ExecuteFailureId, exception, "Executing command for operation [{0}] failed.", operation);
                }

                public static void Undo(ILogger logger, ICommand command)
                {
                    logger.LogInformation(UndoId, "Undoing command: {0}", command.Show());
                }

                public static void UndoSuccess(ILogger logger, string operation)
                {
                    logger.LogDebug(UndoSuccessId, "Undoing command for operation [{0}] succeeded.", operation);
                }

                public static void UndoFailure(ILogger logger, string operation, Exception exception)
                {
                    logger.LogError(UndoFailureId, exception, "Undoing command for operation [{0}] failed.", operation);
                }
            }
        }
    }
}
