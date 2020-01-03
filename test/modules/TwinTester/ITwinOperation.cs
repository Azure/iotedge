// Copyright (c) Microsoft. All rights reserved.
namespace TwinTester
{
    using System.Threading.Tasks;

    interface ITwinOperation
    {
        Task UpdateAsync();
    }
}
