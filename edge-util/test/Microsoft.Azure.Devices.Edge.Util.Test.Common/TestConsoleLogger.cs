// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Test.Common
{
    using System;
    using System.IO;
    using Xunit.Abstractions;

    /// <summary>
    /// Use this as the base class for a test to output all product logs to console.
    /// </summary>
    public class TestConsoleLogger : IDisposable
    {
        readonly ITestOutputHelper testOutputHelper;
        readonly TextWriter originalConsoleWriter;
        readonly TextWriter consoleWriter;

        public TestConsoleLogger(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
            this.originalConsoleWriter = Console.Out;
            this.consoleWriter = new StringWriter();
            Console.SetOut(this.consoleWriter);
        }

        public virtual void Dispose()
        {
            this.testOutputHelper.WriteLine(this.consoleWriter.ToString());
            Console.SetOut(this.originalConsoleWriter);
        }
    }
}
