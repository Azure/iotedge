// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Extensions.Logging;

    public class ModuleManagementHttpClient : IModuleManager, IIdentityManager
    {
        const string ApiVersion = "2018-06-28";
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(3));

        readonly EdgeletHttpClient edgeletHttpClient;
        readonly string EdgeletHttpUrl;

        public ModuleManagementHttpClient(string edgeletHttpUrl)
        {
            this.EdgeletHttpUrl = Preconditions.CheckNonWhiteSpace(edgeletHttpUrl, nameof(edgeletHttpUrl));
            this.edgeletHttpClient = new EdgeletHttpClient { BaseUrl = this.EdgeletHttpUrl };
        }

        public Task<Identity> CreateIdentityAsync(string name) => this.Execute(
                () => this.edgeletHttpClient.CreateIdentityAsync(ApiVersion, name, new IdentitySpec { ModuleId = name }),
                $"Create identity for {name}");

        public Task DeleteIdentityAsync(string name) => this.Execute(
                () => this.edgeletHttpClient.DeleteIdentityAsync(ApiVersion, name),
                $"Delete identity for {name}");

        public async Task<IEnumerable<Identity>> GetIdentities()
        {
            IdentityList identityList = await this.Execute(
                () => this.edgeletHttpClient.ListIdentitiesAsync(ApiVersion),
                $"List identities");
            return identityList.Identities;
        }

        public Task CreateModuleAsync(ModuleSpec moduleSpec) => this.Execute(
                () => this.edgeletHttpClient.CreateModuleAsync(ApiVersion, moduleSpec),
                $"Create module {moduleSpec.Name}");

        public Task DeleteModuleAsync(string name) => this.Execute(
                () => this.edgeletHttpClient.DeleteModuleAsync(ApiVersion, name),
                $"Create module {name}");

        public async Task<IEnumerable<ModuleDetails>> GetModules()
        {
            ModuleList moduleList = await this.Execute(
                () => this.edgeletHttpClient.ListModulesAsync(ApiVersion),
                $"List modules");
            return moduleList.Modules;
        }

        public Task StartModuleAsync(string name) => this.Execute(
                () => this.edgeletHttpClient.StartModuleAsync(ApiVersion, name),
                $"start module {name}");

        public Task StopModuleAsync(string name) => this.Execute(
                () => this.edgeletHttpClient.StopModuleAsync(ApiVersion, name),
                $"stop module {name}");

        public Task UpdateModuleAsync(ModuleSpec moduleSpec) => this.Execute(
                () => this.edgeletHttpClient.UpdateModuleAsync(ApiVersion, moduleSpec.Name, moduleSpec),
                $"update module {moduleSpec.Name}");

        Task Execute(Func<Task> func, string operation) =>
            this.Execute<int>(async () =>
            {
                await func();
                return 1;
            }, operation);

        async Task<T> Execute<T>(Func<Task<T>> func, string operation)
        {
            try
            {
                Events.ExecutingOperation(operation, this.EdgeletHttpUrl);
                T result = await ExecuteWithRetry(func, (r) => Events.RetryingOperation(operation, this.EdgeletHttpUrl, r));
                Events.SuccessfullyExecutedOperation(operation, this.EdgeletHttpUrl);
                return result;
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case SwaggerException<ErrorResponse> errorResponseException:
                        throw new EdgeletCommunicationException($"Error calling {operation}: {errorResponseException.Result?.Message ?? string.Empty}", errorResponseException.StatusCode);
                    case SwaggerException swaggerException:
                        throw new EdgeletCommunicationException($"Error calling {operation}: {swaggerException.Response ?? string.Empty}", swaggerException.StatusCode);
                    default:
                        throw;
                }
            }
        }

        static Task<T> ExecuteWithRetry<T>(Func<Task<T>> func, Action<RetryingEventArgs> onRetry)
        {
            var transientRetryPolicy = new RetryPolicy(TransientErrorDetectionStrategy, TransientRetryStrategy);
            transientRetryPolicy.Retrying += (_, args) => onRetry(args);
            return transientRetryPolicy.ExecuteAsync(func);
        }

        class ErrorDetectionStrategy : ITransientErrorDetectionStrategy
        {
            public bool IsTransient(Exception ex) => ex is SwaggerException se
                && se.StatusCode >= 500;
        }

        static class Events
        {
            static readonly ILogger Log = Logger.Factory.CreateLogger<ModuleManagementHttpClient>();
            const int IdStart = AgentEventIds.ModuleManagementHttpClient;

            enum EventIds
            {
                ExecutingOperation = IdStart,
                SuccessfullyExecutedOperation,
                RetryingOperation
            }

            internal static void RetryingOperation(string operation, string url, RetryingEventArgs r)
            {
                Log.LogDebug((int)EventIds.RetryingOperation, $"Retrying Http call to {url} to {operation} because of error {r.LastException.Message}, retry count = {r.CurrentRetryCount}");
            }

            internal static void ExecutingOperation(string operation, string url)
            {
                Log.LogDebug((int)EventIds.ExecutingOperation, $"Making a Http call to {url} to {operation}");
            }

            internal static void SuccessfullyExecutedOperation(string operation, string url)
            {
                Log.LogDebug((int)EventIds.SuccessfullyExecutedOperation, $"Received a valid Http response from {url} for {operation}");
            }
        }
    }
}
