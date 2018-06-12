// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Agent.Core;
    using Microsoft.Azure.Devices.Edge.Agent.Edgelet.GeneratedCode;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Xunit;

    [Unit]
    public class ModuleManagementHttpClientTest : IClassFixture<EdleletFixture>
    {
        readonly Uri serverUrl;

        public ModuleManagementHttpClientTest(EdleletFixture edleletFixture)
        {
            this.serverUrl = new Uri(edleletFixture.ServiceUrl);
        }

        [Fact]
        public async Task IdentityTest()
        {
            // Arrange
            IIdentityManager client = new ModuleManagementHttpClient(this.serverUrl);

            // Act
            Identity identity1 = await client.CreateIdentityAsync("Foo", Constants.ModuleIdentityEdgeManagedByValue);
            Identity identity2 = await client.CreateIdentityAsync("Bar", Constants.ModuleIdentityEdgeManagedByValue);
            Identity identity3 = await client.CreateIdentityAsync("External", "Someone");

            // Assert
            Assert.NotNull(identity1);
            Assert.Equal("Foo", identity1.ModuleId);
            Assert.Equal(Constants.ModuleIdentityEdgeManagedByValue, identity1.ManagedBy);
            Assert.NotNull(identity2);
            Assert.Equal("Bar", identity2.ModuleId);
            Assert.Equal(Constants.ModuleIdentityEdgeManagedByValue, identity2.ManagedBy);
            Assert.NotNull(identity3);
            Assert.Equal("External", identity3.ModuleId);
            Assert.Equal("Someone", identity3.ManagedBy);

            // Act
            Identity identity4 = await client.UpdateIdentityAsync("Foo", identity1.GenerationId, identity1.ManagedBy);
            Identity identity5 = await client.UpdateIdentityAsync("Bar", identity2.GenerationId, identity2.ManagedBy);            
            Identity identity6 = await client.UpdateIdentityAsync("External", identity3.GenerationId, identity3.ManagedBy);

            // Assert
            Assert.NotNull(identity4);
            Assert.Equal("Foo", identity4.ModuleId);
            Assert.Equal(identity1.GenerationId, identity4.GenerationId);
            Assert.Equal(identity1.ManagedBy, identity4.ManagedBy);
            Assert.NotNull(identity5);
            Assert.Equal("Bar", identity5.ModuleId);
            Assert.Equal(identity2.GenerationId, identity5.GenerationId);
            Assert.Equal(identity2.ManagedBy, identity5.ManagedBy);
            Assert.NotNull(identity6);
            Assert.Equal("External", identity6.ModuleId);
            Assert.Equal(identity3.GenerationId, identity6.GenerationId);
            Assert.Equal("Someone", identity6.ManagedBy);

            // Act
            List<Identity> identities = (await client.GetIdentities())
                .OrderBy(o => o.ModuleId)
                .ToList();

            // Assert
            Assert.NotNull(identities);
            Assert.Equal(3, identities.Count);
            Assert.Equal("Bar", identities[0].ModuleId);
            Assert.Equal("External", identities[1].ModuleId);
            Assert.Equal("Foo", identities[2].ModuleId);
            Assert.Equal(Constants.ModuleIdentityEdgeManagedByValue, identities[0].ManagedBy);            
            Assert.Equal("Someone", identities[1].ManagedBy);
            Assert.Equal(Constants.ModuleIdentityEdgeManagedByValue, identities[2].ManagedBy);

            // Act
            await client.DeleteIdentityAsync("Bar");
            identities = (await client.GetIdentities())
                .OrderBy(s => s.ModuleId)
                .ToList();

            // Assert
            Assert.NotNull(identities);
            Assert.Equal(2, identities.Count);
            Assert.Equal("External", identities[0].ModuleId);
            Assert.Equal("Foo", identities[1].ModuleId);
        }

        [Fact]
        public async Task ModulesTest()
        {
            // Arrange
            IModuleManager client = new ModuleManagementHttpClient(this.serverUrl);
            var moduleSpec = new ModuleSpec
            {
                Name = "Module1",
                Type = "Docker",
                Config = new Config
                {
                    Env = new System.Collections.ObjectModel.ObservableCollection<EnvVar> { new EnvVar { Key = "E1", Value = "P1" } },
                    Settings = "{ \"image\": \"testimage\" }"
                }
            };

            // Act
            await client.CreateModuleAsync(moduleSpec);
            ModuleDetails moduleDetails = (await client.GetModules(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal("Module1", moduleDetails.Name);
            Assert.NotNull(moduleDetails.Id);
            Assert.Equal("Docker", moduleDetails.Type);
            Assert.NotNull(moduleDetails.Status);
            Assert.Equal("Created", moduleDetails.Status.RuntimeStatus.Status);

            // Act
            await client.StartModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal("Running", moduleDetails.Status.RuntimeStatus.Status);

            // Act
            await client.StopModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal("Stopped", moduleDetails.Status.RuntimeStatus.Status);

            // Act - Stopping a stopped module should not throw
            await client.StopModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.NotNull(moduleDetails);
            Assert.Equal("Stopped", moduleDetails.Status.RuntimeStatus.Status);

            // Act
            await client.DeleteModuleAsync(moduleSpec.Name);
            moduleDetails = (await client.GetModules(CancellationToken.None)).FirstOrDefault();

            // Assert
            Assert.Null(moduleDetails);
        }
    }
}
