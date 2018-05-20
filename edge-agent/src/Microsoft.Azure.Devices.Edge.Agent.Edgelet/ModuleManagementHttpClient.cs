// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.TransientFaultHandling;
    using Microsoft.Azure.Devices.Edge.Util.Uds;
    using Microsoft.Extensions.Logging;

    public class ModuleManagementHttpClient : IModuleManager, IIdentityManager
    {
        const string ApiVersion = "2018-06-28";
        static readonly ITransientErrorDetectionStrategy TransientErrorDetectionStrategy = new ErrorDetectionStrategy();
        static readonly RetryStrategy TransientRetryStrategy =
            new ExponentialBackoff(retryCount: 3, minBackoff: TimeSpan.FromSeconds(2), maxBackoff: TimeSpan.FromSeconds(30), deltaBackoff: TimeSpan.FromSeconds(3));
        readonly Uri managementUri;

        public ModuleManagementHttpClient(Uri managementUri)
        {
            this.managementUri = Preconditions.CheckNotNull(managementUri, nameof(managementUri));
        }

        public async Task<Identity> CreateIdentityAsync(string name)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                Identity identity = await this.Execute(() => edgeletHttpClient.CreateIdentityAsync(ApiVersion, name, new IdentitySpec { ModuleId = name }), $"Create identity for {name}");
                return identity;
            }
        }

        public async Task DeleteIdentityAsync(string name)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                await this.Execute(() => edgeletHttpClient.DeleteIdentityAsync(ApiVersion, name), $"Delete identity for {name}");
            }
        }

        public async Task<IEnumerable<Identity>> GetIdentities()
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                IdentityList identityList = await this.Execute(() => edgeletHttpClient.ListIdentitiesAsync(ApiVersion), $"List identities");
                return identityList.Identities;
            }
        }

        public async Task CreateModuleAsync(ModuleSpec moduleSpec)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                await this.Execute(() => edgeletHttpClient.CreateModuleAsync(ApiVersion, moduleSpec), $"Create module {moduleSpec.Name}");
            }
        }

        public async Task DeleteModuleAsync(string name)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                await this.Execute(() => edgeletHttpClient.DeleteModuleAsync(ApiVersion, name), $"Delete module {name}");
            }
        }

        public async Task RestartModuleAsync(string name)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                await this.Execute(
                    () => edgeletHttpClient.RestartModuleAsync(ApiVersion, name),
                    $"Restart module {name}");
            }
        }

        public async Task<IEnumerable<ModuleDetails>> GetModules(CancellationToken cancellationToken)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                ModuleList moduleList = await this.Execute(
                    () => edgeletHttpClient.ListModulesAsync(ApiVersion, cancellationToken),
                    $"List modules");
                return moduleList.Modules;
            }
        }

        public async Task StartModuleAsync(string name)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                await this.Execute(() => edgeletHttpClient.StartModuleAsync(ApiVersion, name), $"start module {name}");
            }
        }

        public async Task StopModuleAsync(string name)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                await this.Execute(() => edgeletHttpClient.StopModuleAsync(ApiVersion, name), $"stop module {name}");
            }
        }

        public async Task UpdateModuleAsync(ModuleSpec moduleSpec)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                await this.Execute(() => edgeletHttpClient.UpdateModuleAsync(ApiVersion, moduleSpec.Name, null, moduleSpec), $"update module {moduleSpec.Name}");
            }
        }
        
        public async Task UpdateAndStartModuleAsync(ModuleSpec moduleSpec)
        {
            using (HttpClient httpClient = HttpClientHelper.GetHttpClient(this.managementUri))
            {
                var edgeletHttpClient = new EdgeletHttpClient(httpClient) { BaseUrl = HttpClientHelper.GetBaseUrl(this.managementUri) };
                await this.Execute(() => edgeletHttpClient.UpdateModuleAsync(ApiVersion, moduleSpec.Name, true, moduleSpec), $"update and start module {moduleSpec.Name}");
            }
        }

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
                Events.ExecutingOperation(operation, this.managementUri.ToString());
                T result = await ExecuteWithRetry(func, (r) => Events.RetryingOperation(operation, this.managementUri.ToString(), r));
                Events.SuccessfullyExecutedOperation(operation, this.managementUri.ToString());
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
