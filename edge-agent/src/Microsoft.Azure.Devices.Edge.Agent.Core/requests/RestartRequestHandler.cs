// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Requests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Extensions.Logging;

    public class RestartRequestHandler : RequestHandlerBase<RestartRequest, object>
    {
        static readonly Version ExpectedSchemaVersion = new Version("1.0");

        readonly IEnvironmentProvider environmentProvider;
        readonly IConfigSource configSource;
        readonly ICommandFactory commandFactory;

        public RestartRequestHandler(IEnvironmentProvider environmentProvider, IConfigSource configSource, ICommandFactory commandFactory)
        {
            this.environmentProvider = Preconditions.CheckNotNull(environmentProvider, nameof(environmentProvider));
            this.configSource = Preconditions.CheckNotNull(configSource, nameof(configSource));
            this.commandFactory = Preconditions.CheckNotNull(commandFactory, nameof(commandFactory));
        }

        public override string RequestName => "RestartModule";

        protected override async Task<Option<object>> HandleRequestInternal(Option<RestartRequest> payloadOption, CancellationToken cancellationToken)
        {
            RestartRequest payload = payloadOption.Expect(() => new ArgumentException("Request payload not found"));
            if (ExpectedSchemaVersion.CompareMajorVersion(payload.SchemaVersion, "restart module request schema") != 0)
            {
                Events.MismatchedMinorVersions(payload.SchemaVersion, ExpectedSchemaVersion);
            }

            Events.ProcessingRequest(payload);

            DeploymentConfigInfo deploymentConfigInfo = await this.configSource.GetDeploymentConfigInfoAsync();
            IEnvironment environment = this.environmentProvider.Create(deploymentConfigInfo.DeploymentConfig);
            ModuleSet modules = await environment.GetModulesAsync(cancellationToken);
            if (!modules.TryGetModule(payload.Id, out IModule module))
            {
                throw new InvalidOperationException($"Module {payload.Id} not found in the current environment");
            }

            Events.RestartingModule(payload.Id);
            ICommand restartCommand = await this.commandFactory.RestartAsync(module);
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
