// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Devices.Edge.Util.Test.Common;
    using Moq;
    using Xunit;

    public class RestartPolicyManagerTest
    {
        const int MaxRestartCount = 5;
        const int CoolOffTimeUnitInSeconds = 10;

        [Fact]
        [Unit]
        public void TestCreateValidation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RestartPolicyManager(-1, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RestartPolicyManager(0, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RestartPolicyManager(5, -5));
            Assert.NotNull(new RestartPolicyManager(5, 0));
            Assert.NotNull(new RestartPolicyManager(5, 10));
        }

        [Fact]
        [Unit]
        public void TestComputeModuleStatusFromRestartPolicyInvalidStatus()
        {
            var manager = new RestartPolicyManager(MaxRestartCount, CoolOffTimeUnitInSeconds);
            Assert.Throws<ArgumentException>(() => manager.ComputeModuleStatusFromRestartPolicy(ModuleStatus.Unknown, RestartPolicy.Always, 0, DateTime.MinValue));
        }

        [Theory]
        [MemberData(nameof(GetTestDataForComputeModuleStatusFromRestartPolicy))]
        [Unit]
        public void TestComputeModuleStatusFromRestartPolicy(ModuleStatus status, RestartPolicy restartPolicy, int restartCount, ModuleStatus expectedStatus)
        {
            var manager = new RestartPolicyManager(MaxRestartCount, CoolOffTimeUnitInSeconds);
            Assert.Equal(expectedStatus, manager.ComputeModuleStatusFromRestartPolicy(status, restartPolicy, restartCount, DateTime.MinValue));
        }

        [Theory]
        [MemberData(nameof(GetTestDataForApplyRestartPolicy))]
        [Unit]
        public void TestApplyRestartPolicy(
            RestartPolicy restartPolicy,
            ModuleStatus runtimeStatus,
            int restartCount,
            Func<DateTime> getLastExitTimeUtc,
            bool apply)
        {
            // Arrange
            var manager = new RestartPolicyManager(MaxRestartCount, CoolOffTimeUnitInSeconds);
            Mock<IRuntimeModule> module = CreateMockRuntimeModule(restartPolicy, runtimeStatus, restartCount, getLastExitTimeUtc());

            // Act
            IEnumerable<IRuntimeModule> result = manager.ApplyRestartPolicy(new[] { module.Object });
            int count = result.Count();

            // Assert
            Assert.True(apply ? count == 1 : count == 0);
        }

        [Unit]
        [Fact]
        public void TestApplyRestartPolicyWithWrongUtcTime()
        {
            // Arrange
            var restartPolicy = RestartPolicy.Always;
            var runtimeStatus = ModuleStatus.Backoff;
            int restartCount = 5;
            DateTime lastExitTime = DateTime.UtcNow.Add(TimeSpan.FromHours(1));
            var manager = new RestartPolicyManager(MaxRestartCount, CoolOffTimeUnitInSeconds);
            Mock<IRuntimeModule> module = CreateMockRuntimeModule(restartPolicy, runtimeStatus, restartCount, lastExitTime);

            // Act
            IEnumerable<IRuntimeModule> result = manager.ApplyRestartPolicy(new[] { module.Object });

            // Assert
            Assert.Single(result);
        }

        [Fact]
        [Unit]
        public void TestApplyRestartPolicyWithUnknownRuntimeStatus()
        {
            var manager = new RestartPolicyManager(MaxRestartCount, CoolOffTimeUnitInSeconds);
            Mock<IRuntimeModule> module = CreateMockRuntimeModule(RestartPolicy.Always, ModuleStatus.Unknown, 0, DateTime.MinValue);
            Assert.Throws<ArgumentException>(() => manager.ApplyRestartPolicy(new[] { module.Object }).ToList());
        }

        [Theory]
        [Unit]
        [InlineData(0, 10)]
        [InlineData(1, 20)]
        [InlineData(2, 40)]
        [InlineData(3, 80)]
        [InlineData(4, 160)]
        [InlineData(5, 300)]
        [InlineData(10, 300)]
        [InlineData(20, 300)]
        public void CoolOffPeriodTest(int restartCount, int expectedCoolOffPeriodSecs)
        {
            // Arrange
            var restartPolicyManager = new RestartPolicyManager(20, 10);

            // Act
            TimeSpan coolOffPeriod = restartPolicyManager.GetCoolOffPeriod(restartCount);

            // Assert
            Assert.Equal(expectedCoolOffPeriodSecs, coolOffPeriod.TotalSeconds);
        }

        public static IEnumerable<object[]> GetTestDataForComputeModuleStatusFromRestartPolicy()
        {
            (
                ModuleStatus status,
                RestartPolicy restartPolicy,
                int restartCount,
                ModuleStatus expectedStatus
                )[] data =
                {
                    // Running
                    (
                        ModuleStatus.Running,
                        RestartPolicy.Never,
                        0,
                        ModuleStatus.Running
                    ),
                    (
                        ModuleStatus.Running,
                        RestartPolicy.OnFailure,
                        0,
                        ModuleStatus.Running
                    ),
                    (
                        ModuleStatus.Running,
                        RestartPolicy.OnUnhealthy,
                        0,
                        ModuleStatus.Running
                    ),
                    (
                        ModuleStatus.Running,
                        RestartPolicy.Always,
                        0,
                        ModuleStatus.Running
                    ),

                    // Unhealthy
                    (
                        ModuleStatus.Unhealthy,
                        RestartPolicy.Never,
                        0,
                        ModuleStatus.Unhealthy
                    ),
                    (
                        ModuleStatus.Unhealthy,
                        RestartPolicy.OnFailure,
                        0,
                        ModuleStatus.Backoff
                    ),
                    (
                        ModuleStatus.Unhealthy,
                        RestartPolicy.OnUnhealthy,
                        0,
                        ModuleStatus.Backoff
                    ),
                    (
                        ModuleStatus.Unhealthy,
                        RestartPolicy.Always,
                        0,
                        ModuleStatus.Backoff
                    ),

                    // Unhealthy / RestartCount > MaxRestartCount
                    (
                        ModuleStatus.Unhealthy,
                        RestartPolicy.OnFailure,
                        MaxRestartCount + 1,
                        ModuleStatus.Failed
                    ),
                    (
                        ModuleStatus.Unhealthy,
                        RestartPolicy.OnUnhealthy,
                        MaxRestartCount + 1,
                        ModuleStatus.Failed
                    ),
                    (
                        ModuleStatus.Unhealthy,
                        RestartPolicy.Always,
                        MaxRestartCount + 1,
                        ModuleStatus.Failed
                    ),

                    // Stopped
                    (
                        ModuleStatus.Stopped,
                        RestartPolicy.Never,
                        0,
                        ModuleStatus.Stopped
                    ),
                    (
                        ModuleStatus.Stopped,
                        RestartPolicy.OnFailure,
                        0,
                        ModuleStatus.Stopped
                    ),
                    (
                        ModuleStatus.Stopped,
                        RestartPolicy.OnUnhealthy,
                        0,
                        ModuleStatus.Stopped
                    ),
                    (
                        ModuleStatus.Stopped,
                        RestartPolicy.Always,
                        0,
                        ModuleStatus.Backoff
                    ),

                    // Stopped / RestartCount > MaxRestartCount
                    (
                        ModuleStatus.Stopped,
                        RestartPolicy.Always,
                        MaxRestartCount + 1,
                        ModuleStatus.Failed
                    ),

                    // Failed
                    (
                        ModuleStatus.Failed,
                        RestartPolicy.Never,
                        0,
                        ModuleStatus.Failed
                    ),
                    (
                        ModuleStatus.Failed,
                        RestartPolicy.OnFailure,
                        0,
                        ModuleStatus.Backoff
                    ),
                    (
                        ModuleStatus.Failed,
                        RestartPolicy.OnUnhealthy,
                        0,
                        ModuleStatus.Backoff
                    ),
                    (
                        ModuleStatus.Failed,
                        RestartPolicy.Always,
                        0,
                        ModuleStatus.Backoff
                    ),

                    // Failed / RestartCount > MaxRestartCount
                    (
                        ModuleStatus.Failed,
                        RestartPolicy.OnFailure,
                        MaxRestartCount + 1,
                        ModuleStatus.Failed
                    ),
                    (
                        ModuleStatus.Failed,
                        RestartPolicy.OnUnhealthy,
                        MaxRestartCount + 1,
                        ModuleStatus.Failed
                    ),
                    (
                        ModuleStatus.Failed,
                        RestartPolicy.Always,
                        MaxRestartCount + 1,
                        ModuleStatus.Failed
                    )
                };

            return data.Select(
                d => new object[]
                {
                    d.status, d.restartPolicy, d.restartCount, d.expectedStatus
                });
        }

        public static IEnumerable<object[]> GetTestDataForApplyRestartPolicy()
        {
            (
                RestartPolicy restartPolicy,
                ModuleStatus runtimeStatus,
                int restartCount,
                Func<DateTime> getLastExitTimeUtc,
                bool apply
                )[] data =
                {
                    //////////////////////////////////
                    // RestartPolicy - Always
                    //////////////////////////////////

                    // Running
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Running,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Running,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),

                    // Failed
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Failed,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Failed,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),

                    // Unhealthy
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Unhealthy,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Unhealthy,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Unhealthy,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    // Backoff
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Backoff,
                        0,
                        () => DateTime.MinValue,
                        true
                    ),
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Backoff,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        true
                    ),
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Backoff,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    // Stopped
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Stopped,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Stopped,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),
                    (
                        RestartPolicy.Always,
                        ModuleStatus.Stopped,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    //////////////////////////////////
                    // RestartPolicy - OnUnhealthy
                    //////////////////////////////////

                    // Running
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Running,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Running,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),

                    // Failed
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Failed,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Failed,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),

                    // Unhealthy
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Unhealthy,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Unhealthy,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Unhealthy,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    // Backoff
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Backoff,
                        0,
                        () => DateTime.MinValue,
                        true
                    ),
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Backoff,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        true
                    ),
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Backoff,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    // Stopped
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Stopped,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Stopped,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),
                    (
                        RestartPolicy.OnUnhealthy,
                        ModuleStatus.Stopped,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    //////////////////////////////////
                    // RestartPolicy - OnFailure
                    //////////////////////////////////

                    // Running
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Running,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Running,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),

                    // Failed
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Failed,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Failed,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),

                    // Unhealthy
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Unhealthy,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Unhealthy,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Unhealthy,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    // Backoff
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Backoff,
                        0,
                        () => DateTime.MinValue,
                        true
                    ),
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Backoff,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        true
                    ),
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Backoff,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    // Stopped
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Stopped,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Stopped,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),
                    (
                        RestartPolicy.OnFailure,
                        ModuleStatus.Stopped,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    //////////////////////////////////
                    // RestartPolicy - Never
                    //////////////////////////////////

                    // Running
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Running,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Running,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),

                    // Failed
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Failed,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Failed,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),

                    // Unhealthy
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Unhealthy,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Unhealthy,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Unhealthy,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    ),

                    // Stopped
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Stopped,
                        0,
                        () => DateTime.MinValue,
                        false
                    ),
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Stopped,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromHours(1),
                        false
                    ),
                    (
                        RestartPolicy.Never,
                        ModuleStatus.Stopped,
                        3,
                        () => DateTime.UtcNow - TimeSpan.FromSeconds(40),
                        false
                    )
                };

            return data.Select(
                d => new object[]
                {
                    d.restartPolicy, d.runtimeStatus, d.restartCount, d.getLastExitTimeUtc, d.apply
                });
        }

        static Mock<IRuntimeModule> CreateMockRuntimeModule(RestartPolicy restartPolicy, ModuleStatus runtimeStatus, int restartCount, DateTime lastExitTimeUtc)
        {
            var module = new Mock<IRuntimeModule>();
            module.Setup(m => m.RestartPolicy).Returns(restartPolicy);
            module.Setup(m => m.RuntimeStatus).Returns(runtimeStatus);
            module.Setup(m => m.RestartCount).Returns(restartCount);
            module.Setup(m => m.LastExitTimeUtc).Returns(lastExitTimeUtc);

            return module;
        }
    }
}
