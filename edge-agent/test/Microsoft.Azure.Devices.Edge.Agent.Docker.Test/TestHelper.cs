// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Docker.Test
{
    using System;
    using System.Threading.Tasks;

    public class TestHelper
    {
        public static async Task Safe(Func<Task> action)
        {
            try
            {
                await action();
            }
            // ReSharper disable once EmptyGeneralCatchClause
            catch (Exception)
            {
            }
        }
    }
}