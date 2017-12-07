// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Util;
    using Xunit;

    public struct TestRecordType
    {
        public readonly TestCommandType TestType;
        public readonly IModule Module;

        public TestRecordType(TestCommandType testType, IModule module)
        {
            this.TestType = testType;
            this.Module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public bool Equals(TestRecordType other) => this.TestType == other.TestType && Equals(this.Module, other.Module);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is TestRecordType && this.Equals((TestRecordType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)this.TestType * 397) ^ (this.Module != null ? this.Module.GetHashCode() : 0);
            }
        }
    }

    public class TestPlanRecorder
    {
        public List<TestRecordType> ExecutionList { get; }
        public List<TestRecordType> UndoList { get; }
        public List<(TestCommandType Type, ICommand Command)> WrappedCommmandList { get; }

        public TestPlanRecorder()
        {
            this.ExecutionList = new List<TestRecordType>();
            this.UndoList = new List<TestRecordType>();
            this.WrappedCommmandList = new List<(TestCommandType Type, ICommand Command)>();
        }

        public void ModuleExecuted(TestCommandType type, IModule module) => this.ExecutionList.Add(new TestRecordType(type, module));

        public void ModuleUndone(TestCommandType type, IModule module) => this.UndoList.Add(new TestRecordType(type, module));

        public void CommandWrapped(ICommand command) => this.WrappedCommmandList.Add((TestCommandType.TestWrap, command));
    }

    public class TestCommandFactory : ICommandFactory
    {
        public Option<TestPlanRecorder> Recorder { get; }

        public TestCommandFactory()
        {
            this.Recorder = Option.Some(new TestPlanRecorder());
        }

        public Task<ICommand> CreateAsync(IModuleWithIdentity module)
        {
            Assert.True(module.Module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestCreate, module.Module, this.Recorder));
        }

        public Task<ICommand> PullAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestPull, module, this.Recorder));
        }

        public Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next)
        {
            Assert.True(current is TestModule);
            Assert.True(next.Module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestUpdate, next.Module, this.Recorder));
        }

        public Task<ICommand> RemoveAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestRemove, module, this.Recorder));
        }

        public Task<ICommand> StartAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestStart, module, this.Recorder));
        }

        public Task<ICommand> StopAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestStop, module, this.Recorder));
        }

        public Task<ICommand> RestartAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestRestart, module, this.Recorder));
        }

        public Task<ICommand> WrapAsync(ICommand command)
        {
            this.Recorder.ForEach(r => r.CommandWrapped(command));
            return Task.FromResult<ICommand>(command);
        }
    }

    public class TestCommandFailureFactory : ICommandFactory
    {
        public Option<TestPlanRecorder> Recorder { get; }

        public TestCommandFailureFactory()
        {
            this.Recorder = Option.Some(new TestPlanRecorder());
        }

        public Task<ICommand> CreateAsync(IModuleWithIdentity module)
        {
            Assert.True(module.Module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestCreate, module.Module, this.Recorder, true));
        }

        public Task<ICommand> PullAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestPull, module, this.Recorder, true));
        }

        public Task<ICommand> UpdateAsync(IModule current, IModuleWithIdentity next)
        {
            Assert.True(current is TestModule);
            Assert.True(next.Module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestUpdate, next.Module, this.Recorder, true));
        }

        public Task<ICommand> RemoveAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestRemove, module, this.Recorder, true));
        }

        public Task<ICommand> StartAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestStart, module, this.Recorder, true));
        }

        public Task<ICommand> StopAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestStop, module, this.Recorder, true));
        }

        public Task<ICommand> RestartAsync(IModule module)
        {
            Assert.True(module is TestModule);
            return Task.FromResult<ICommand>(new TestCommand(TestCommandType.TestRestart, module, this.Recorder, true));
        }

        public Task<ICommand> WrapAsync(ICommand command)
        {
            foreach (TestPlanRecorder r in this.Recorder)
                r.CommandWrapped(command);

            return Task.FromResult<ICommand>(command);
        }
    }
    public class TestCommand : ICommand
    {
        readonly Option<TestPlanRecorder> recorder;
        readonly TestCommandType type;
        readonly IModule module;
        readonly bool throwOnExecute;
        public bool CommandExecuted;
        public bool CommandUndone;

        public TestCommand(TestCommandType type, IModule module) :
            this(type, module, Option.None<TestPlanRecorder>(), false)
        {
        }

        public TestCommand(TestCommandType type, IModule module, Option<TestPlanRecorder> recorder) :
            this(type, module, recorder, false)
        {
        }

        public TestCommand(TestCommandType type, IModule module, Option<TestPlanRecorder> recorder, bool throwOnExecute)
        {
            this.type = type;
            this.module = Preconditions.CheckNotNull(module, nameof(module));
            this.recorder = recorder;
            this.throwOnExecute = throwOnExecute;
            this.CommandExecuted = false;
            this.CommandUndone = false;
        }

        public string Id => this.Show();

        public string Show() => $"TestCommand {this.type.ToString()}:{this.module.Name}";

        public Task ExecuteAsync(CancellationToken token)
        {
            if (this.throwOnExecute)
            {
                throw new ArgumentException(this.module.Name);
            }

            foreach (TestPlanRecorder r in this.recorder)
                r.ModuleExecuted(this.type, this.module);
            this.CommandExecuted = true;
            return TaskEx.Done;
        }

        public Task UndoAsync(CancellationToken token)
        {
            foreach (TestPlanRecorder r in this.recorder)
                r.ModuleUndone(this.type, this.module);
            this.CommandUndone = true;
            return TaskEx.Done;
        }

        public override string ToString() => this.Show();
    }
}
