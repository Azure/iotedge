// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.CloudProxy.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Hub.Core;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Newtonsoft.Json;
    using Xunit;

    [Unit]
    public class ServiceIdentityHelpersTest
    {
        public static IEnumerable<object[]> GetDeviceJson()
        {
            yield return new object[]
            {
                @"{
                      ""deviceId"": ""d301"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""status"": ""enabled"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": """ + GetKey() + @""",
                          ""secondaryKey"": """ + GetKey() + @"""
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": null,
                          ""secondaryThumbprint"": null
                        },
                        ""type"": ""sas""
                      },
                      ""capabilities"": {
                        ""iotEdge"": true
                      },
                      ""deviceScope"": ""ms-azure-iot-edge://d301-636704968692034950""
                }",
            };

            yield return new object[]
            {
                @"{
                      ""deviceId"": ""d302"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""status"": ""enabled"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": null,
                          ""secondaryKey"": null
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": """ + GetKey() + @""",
                          ""secondaryThumbprint"": """ + GetKey() + @"""
                        },
                        ""type"": ""selfSigned""
                      },
                      ""capabilities"": {
                        ""iotEdge"": true
                      },
                      ""deviceScope"": ""ms-azure-iot-edge://d301-636704968692034950""
                }"
            };

            yield return new object[]
            {
                @"{
                      ""deviceId"": ""d303"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""status"": ""disabled"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": null,
                          ""secondaryKey"": null
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": null,
                          ""secondaryThumbprint"": null
                        },
                        ""type"": ""none""
                      },
                      ""capabilities"": {
                        ""iotEdge"": false
                      },
                      ""deviceScope"": ""ms-azure-iot-edge://d301-636704968692034950""
                }"
            };

            yield return new object[]
            {
                @"{
                      ""deviceId"": ""d304"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""status"": ""enabled"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": null,
                          ""secondaryKey"": null
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": null,
                          ""secondaryThumbprint"": null
                        },
                        ""type"": ""certificateAuthority""
                      },
                      ""capabilities"": {
                        ""iotEdge"": false
                      },
                      ""deviceScope"": ""ms-azure-iot-edge://d301-636704968692034950""
                }"
            };
        }

        public static IEnumerable<object[]> GetModuleJson()
        {
            yield return new object[]
            {
                @"{                        
                      ""deviceId"": ""d301"",
                      ""moduleId"": ""m1"",
                      ""managedBy"": ""iotEdge"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": """ + GetKey() + @""",
                          ""secondaryKey"": """ + GetKey() + @"""
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": null,
                          ""secondaryThumbprint"": null
                        },
                        ""type"": ""sas""
                      }
                }"
            };

            yield return new object[]
            {
                @"{
                      ""moduleId"": ""$edgeAgent"",
                      ""managedBy"": ""iotEdge"",
                      ""deviceId"": ""d302"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": null,
                          ""secondaryKey"": null
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": """ + GetKey() + @""",
                          ""secondaryThumbprint"": """ + GetKey() + @"""
                        },
                        ""type"": ""selfSigned""
                      }
                }"
            };

            yield return new object[]
            {
                @"{
                      ""moduleId"": ""m3"",
                      ""managedBy"": ""someone"",
                      ""deviceId"": ""d303"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": null,
                          ""secondaryKey"": null
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": null,
                          ""secondaryThumbprint"": null
                        },
                        ""type"": ""none""
                      }
                }"
            };

            yield return new object[]
            {
                @"{
                      ""moduleId"": ""m6"",
                      ""managedBy"": """",
                      ""deviceId"": ""d304"",
                      ""generationId"": ""636704968692034950"",
                      ""etag"": ""NzM0NTkyNTc="",
                      ""connectionState"": ""Disconnected"",
                      ""statusReason"": null,
                      ""connectionStateUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""statusUpdatedTime"": ""0001-01-01T00:00:00"",
                      ""lastActivityTime"": ""0001-01-01T00:00:00"",
                      ""cloudToDeviceMessageCount"": 0,
                      ""authentication"": {
                        ""symmetricKey"": {
                          ""primaryKey"": null,
                          ""secondaryKey"": null
                        },
                        ""x509Thumbprint"": {
                          ""primaryThumbprint"": null,
                          ""secondaryThumbprint"": null
                        },
                        ""type"": ""certificateAuthority""
                      }
                }"
            };
        }

        [Theory]
        [MemberData(nameof(GetDeviceJson))]
        public void DeviceToServiceIdentityTest(string deviceJson)
        {
            // Arrange
            var device = JsonConvert.DeserializeObject<Device>(deviceJson);

            // Act
            ServiceIdentity serviceIdentity = device.ToServiceIdentity();

            // Assert
            Assert.NotNull(serviceIdentity);
            Assert.Equal(device.Id, serviceIdentity.DeviceId);
            Assert.False(serviceIdentity.IsModule);
            Assert.False(serviceIdentity.ModuleId.HasValue);
            Assert.Equal(device.GenerationId, serviceIdentity.GenerationId);
            if (device.Capabilities.IotEdge)
            {
                Assert.Contains(Constants.IotEdgeIdentityCapability, serviceIdentity.Capabilities);
            }
            else
            {
                Assert.False(serviceIdentity.Capabilities.Any());
            }

            Assert.Equal(device.Status == DeviceStatus.Enabled ? ServiceIdentityStatus.Enabled : ServiceIdentityStatus.Disabled, serviceIdentity.Status);

            ValidateAuthentication(device.Authentication, serviceIdentity.Authentication);
        }

        [Theory]
        [MemberData(nameof(GetModuleJson))]
        public void ModuleToServiceIdentityTest(string moduleJson)
        {
            // Arrange
            var module = JsonConvert.DeserializeObject<Module>(moduleJson);

            // Act
            ServiceIdentity serviceIdentity = module.ToServiceIdentity();

            // Assert
            Assert.NotNull(serviceIdentity);
            Assert.Equal(module.DeviceId, serviceIdentity.DeviceId);
            Assert.Equal(module.Id, serviceIdentity.ModuleId.OrDefault());
            Assert.True(serviceIdentity.IsModule);
            Assert.Equal(module.GenerationId, serviceIdentity.GenerationId);
            Assert.False(serviceIdentity.Capabilities.Any());
            Assert.Equal(ServiceIdentityStatus.Enabled, serviceIdentity.Status);
            ValidateAuthentication(module.Authentication, serviceIdentity.Authentication);
        }

        static void ValidateAuthentication(AuthenticationMechanism authenticationMechanism, ServiceAuthentication serviceIdentityAuthentication)
        {
            Assert.NotNull(serviceIdentityAuthentication);
            switch (authenticationMechanism.Type)
            {
                case AuthenticationType.Sas:
                    Assert.Equal(ServiceAuthenticationType.SymmetricKey, serviceIdentityAuthentication.Type);
                    Assert.True(serviceIdentityAuthentication.SymmetricKey.HasValue);
                    Assert.False(serviceIdentityAuthentication.X509Thumbprint.HasValue);
                    Assert.Equal(authenticationMechanism.SymmetricKey.PrimaryKey, serviceIdentityAuthentication.SymmetricKey.OrDefault().PrimaryKey);
                    Assert.Equal(authenticationMechanism.SymmetricKey.SecondaryKey, serviceIdentityAuthentication.SymmetricKey.OrDefault().SecondaryKey);
                    break;

                case AuthenticationType.CertificateAuthority:
                    Assert.Equal(ServiceAuthenticationType.CertificateAuthority, serviceIdentityAuthentication.Type);
                    Assert.False(serviceIdentityAuthentication.X509Thumbprint.HasValue);
                    Assert.False(serviceIdentityAuthentication.SymmetricKey.HasValue);
                    break;

                case AuthenticationType.SelfSigned:
                    Assert.Equal(ServiceAuthenticationType.CertificateThumbprint, serviceIdentityAuthentication.Type);
                    Assert.True(serviceIdentityAuthentication.X509Thumbprint.HasValue);
                    Assert.False(serviceIdentityAuthentication.SymmetricKey.HasValue);
                    Assert.Equal(authenticationMechanism.X509Thumbprint.PrimaryThumbprint, serviceIdentityAuthentication.X509Thumbprint.OrDefault().PrimaryThumbprint);
                    Assert.Equal(authenticationMechanism.X509Thumbprint.SecondaryThumbprint, serviceIdentityAuthentication.X509Thumbprint.OrDefault().SecondaryThumbprint);
                    break;

                case AuthenticationType.None:
                    Assert.Equal(ServiceAuthenticationType.None, serviceIdentityAuthentication.Type);
                    Assert.False(serviceIdentityAuthentication.X509Thumbprint.HasValue);
                    Assert.False(serviceIdentityAuthentication.SymmetricKey.HasValue);
                    break;
            }
        }

        static string GetKey() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()));
    }
}
