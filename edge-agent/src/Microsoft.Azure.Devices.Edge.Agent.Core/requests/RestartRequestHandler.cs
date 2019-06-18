// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class RestartRequestHandler : RequestHandlerBase<RestartRequest, object>
    {
        static readonly Version ExpectedSchemaVersion = new Version("1.0");

        readonly IRuntimeInfoProvider runtimeInfoProvider;
        readonly ICommandFactory commandFactory;

        public RestartRequestHandler(IRuntimeInfoProvider runtimeInfoProvider, ICommandFactory commandFactory)
        {
            this.runtimeInfoProvider = Preconditions.CheckNotNull(runtimeInfoProvider, nameof(runtimeInfoProvider));
            this.commandFactory = Preconditions.CheckNotNull(commandFactory, nameof(commandFactory));
        }

        public override string RequestName => "RestartModule";

        protected override async Task<Option<object>> HandleRequestInternal(Option<RestartRequest> payloadOption, CancellationToken cancellationToken)
        {
            RestartRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));
            if (ExpectedSchemaVersion.CompareMajorVersion(payload.SchemaVersion, "logs upload request schema") != 0)
            {
                Events.MismatchedMinorVersions(payload.SchemaVersion, ExpectedSchemaVersion);
            }

            Events.ProcessingRequest(payload);
            IEnumerable<ModuleRuntimeInfo> modules = await this.runtimeInfoProvider.GetModules(cancellationToken);
            Option<ModuleRuntimeInfo> moduleOption = modules.FirstOption(m => m.Name == payload.Id);
            if (!moduleOption.HasValue)
            {
                throw new InvalidOperationException($"Module {payload.Id} not found in the current environment");
            }

            if (!moduleOption.Filter(m => m.ModuleStatus == ModuleStatus.Running).HasValue)
            {
                throw new InvalidOperationException($"Module {payload.Id} cannot be restarted since it is not running");
            }

            Events.RestartingModule(payload.Id);
            ICommand restartCommand = await this.commandFactory.RestartAsync(payload.Id);
            await restartCommand.ExecuteAsync(cancellationToken);
            Events.RestartedModule(payload.Id);
            return Option.None<object>();
        }

        static class Events
        {
            const int IdStart = AgentEventIds.RestartRequestHandler;
            static readonly ILogger Log = Logger.Factory.CreateLogger<RestartRequestHandler>();

            enum EventIds
            {
                MismatchedMinorVersions = IdStart,
                ProcessingRequest,
                RestartingModule,
                RestartedModule
            }

            public static void MismatchedMinorVersions(string payloadSchemaVersion, Version expectedSchemaVersion)
            {
                Log.LogWarning((int)EventIds.MismatchedMinorVersions, $"Logs upload request schema version {payloadSchemaVersion} does not match expected schema version {expectedSchemaVersion}. Some settings may not be supported.");
            }

            public static void ProcessingRequest(RestartRequest payload)
            {
                Log.LogInformation((int)EventIds.ProcessingRequest, $"Processing request to restart {payload.Id}");
            }

            public static void RestartingModule(string id)
            {
                Log.LogInformation((int)EventIds.RestartingModule, $"Restarting {id}...");
            }

            public static void RestartedModule(string id)
            {
                Log.LogInformation((int)EventIds.RestartedModule, $"Restarted {id}");
            }
        }
    }
}
