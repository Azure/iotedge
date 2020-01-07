// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System;
    using System.Threading.Tasks;

    interface ITwinTestInitializer : IDisposable
    {
        Task Start();
    }
}
