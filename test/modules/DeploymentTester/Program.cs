// Copyright (c) Microsoft. All rights reserved.
namespace DeploymentTester
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Edge.ModuleUtil;
    using Microsoft.Azure.Devices.Edge.ModuleUtil.TestResults;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    class Program
    {
        static readonly ILogger Logger = ModuleUtil.CreateLogger("DeploymentTester");

        static async Task Main(string[] args)
        {
            Logger.LogInformation($"Starting Deployment Tester with the following settings: \r\n{Settings.Current}");

            try
            {
                (CancellationTokenSource cts, ManualResetEventSlim completed, Option<object> handler) = ShutdownHandler.Init(TimeSpan.FromSeconds(5), Logger);

                var testResultReportingClient = new TestResultReportingClient { BaseUrl = Settings.Current.TestResultCoordinatorUrl.AbsoluteUri };

                if (Settings.Current.TestMode == DeploymentTesterMode.Receiver)
                {
                    await ReportDeploymentEnvironmentVariablesAsync(testResultReportingClient);
                }
                else
                {
                    await Task.Delay(Settings.Current.TestStartDelay);
                    await UpdateDeploymentEnvironmentVariablesAsync(testResultReportingClient, cts);
                }

                await cts.Token.WhenCanceled();
                completed.Set();
                handler.ForEach(h => GC.KeepAlive(h));
                Logger.LogInformation("DeploymentTester Main() finished.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected exception found.");
            }
        }

        static async Task UpdateDeploymentEnvironmentVariablesAsync(TestResultReportingClient apiClient, CancellationTokenSource cts)
        {
            RegistryManager registryManager = null;

            try
            {
                registryManager = RegistryManager.CreateFromConnectionString(Settings.Current.IoTHubConnectionString);
                JObject deploymentJson = await GetEdgeAgentDeploymentManifestJsonAsync(registryManager, Settings.Current.DeviceId);

                DateTime testStartAt = DateTime.UtcNow;
                long count = 1;
                var envVars = new Dictionary<string, string>();

                while (!cts.IsCancellationRequested && DateTime.UtcNow - testStartAt < Settings.Current.TestDuration)
                {
                    KeyValuePair<string, string> newEnvVar = AddEnvironmentValue(deploymentJson, Settings.Current.TargetModuleId, count);
                    ConfigurationContent configContent = JsonConvert.DeserializeObject<ConfigurationContent>(deploymentJson.ToString());
                    await registryManager.ApplyConfigurationContentOnDeviceAsync(Settings.Current.DeviceId, configContent);

                    envVars.Add(newEnvVar.Key, newEnvVar.Value);
                    var testResult = new DeploymentTestResult(Settings.Current.ModuleId + ".send", DateTime.UtcNow) { TrackingId = Settings.Current.TrackingId, EnvironmentVariables = envVars };
                    await ModuleUtil.ReportTestResultAsync(apiClient, Logger, testResult);
                    Logger.LogInformation($"Successfully report to TRC for new deployment: tracking id={Settings.Current.TrackingId}, new environment variable={newEnvVar.Key}:{newEnvVar.Value}, EnvVars Count={envVars.Count}.");

                    await Task.Delay(Settings.Current.DeploymentUpdatePeriod, cts.Token);
                    count++;
                }
            }
            finally
            {
                registryManager?.Dispose();
            }
        }

        static KeyValuePair<string, string> AddEnvironmentValue(JObject deploymentJson, string targetModuleId, long count)
        {
            var newEnvVar = new KeyValuePair<string, string>($"{Settings.EnvironmentVariablePrefix}{count}", count.ToString());

            JToken env = deploymentJson["modulesContent"]["$edgeAgent"]["properties.desired"]["modules"][targetModuleId]["env"];
            env[newEnvVar.Key] = JToken.Parse($"{{ \"value\": \"{newEnvVar.Value}\" }}");

            return newEnvVar;
        }

        static async Task<JObject> GetEdgeAgentDeploymentManifestJsonAsync(RegistryManager registryManager, string deviceId)
        {
            Twin edgeAgentTwin = await registryManager.GetTwinAsync(deviceId, "$edgeAgent");

            return new JObject(
                new JProperty(
                    "modulesContent",
                    new JObject(
                        new JProperty("$edgeAgent", GetDesiredProperties(edgeAgentTwin)))));
        }

        static JToken GetDesiredProperties(Twin moduleTwin)
        {
            JToken desiredPropertiesJson = JToken.Parse("{ \"properties.desired\": {} }");

            foreach (KeyValuePair<string, object> desiredProp in moduleTwin.Properties.Desired)
            {
                if (desiredProp.Key.Equals("schemaVersion", StringComparison.Ordinal))
                {
                    // Need to assign schema version explicitly instead of using JToken.Parse, which will automatically convert "1.0" to 1.
                    desiredPropertiesJson["properties.desired"][desiredProp.Key] = desiredProp.Value.ToString();
                }
                else
                {
                    desiredPropertiesJson["properties.desired"][desiredProp.Key] = JToken.Parse(desiredProp.Value.ToString());
                }
            }

            return desiredPropertiesJson;
        }

        static async Task ReportDeploymentEnvironmentVariablesAsync(TestResultReportingClient trcClient)
        {
            // Report all environment variable with predefined prefix to Test Result Coordinator
            var envVars = new Dictionary<string, string>();
            foreach (DictionaryEntry envVariable in Environment.GetEnvironmentVariables())
            {
                if (envVariable.Key.ToString().StartsWith(Settings.EnvironmentVariablePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    envVars.Add(envVariable.Key.ToString(), envVariable.Value.ToString());
                }
            }

            var testResult = new DeploymentTestResult(Settings.Current.ModuleId + ".receive", DateTime.UtcNow)
            {
                TrackingId = Settings.Current.TrackingId,
                EnvironmentVariables = envVars
            };

            await ModuleUtil.ReportTestResultAsync(trcClient, Logger, testResult);
            Logger.LogInformation($"Successfully report to TRC for new deployment: tracking id={Settings.Current.TrackingId}, environment variable count={testResult.EnvironmentVariables.Count}.");
        }
    }
}
