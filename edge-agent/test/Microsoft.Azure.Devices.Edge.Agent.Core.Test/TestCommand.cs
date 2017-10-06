// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test
{
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

        public ICommand Create(IModule module)
        {
            Assert.True(module is TestModule);
            return new TestCommand(TestCommandType.TestCreate, module, this.Recorder);
        }

        public ICommand Pull(IModule module)
        {
            Assert.True(module is TestModule);
            return new TestCommand(TestCommandType.TestPull, module, this.Recorder);
        }

        public ICommand Update(IModule current, IModule next)
        {
            Assert.True(current is TestModule);
            Assert.True(next is TestModule);
            return new TestCommand(TestCommandType.TestUpdate, next, this.Recorder);
        }

        public ICommand Remove(IModule module)
        {
            Assert.True(module is TestModule);
            return new TestCommand(TestCommandType.TestRemove, module, this.Recorder);
        }

        public ICommand Start(IModule module)
        {
            Assert.True(module is TestModule);
            return new TestCommand(TestCommandType.TestStart, module, this.Recorder);
        }

        public ICommand Stop(IModule module)
        {
            Assert.True(module is TestModule);
            return new TestCommand(TestCommandType.TestStop, module, this.Recorder);
        }

        public ICommand Restart(IModule module)
        {
            Assert.True(module is TestModule);
            return new TestCommand(TestCommandType.TestRestart, module, this.Recorder);
        }

        public ICommand Wrap(ICommand command)
        {
            foreach (TestPlanRecorder r in this.Recorder)
                r.CommandWrapped(command);

            return command;
        }
    }

    public class TestCommand : ICommand
    {
        readonly Option<TestPlanRecorder> recorder;
        readonly TestCommandType type;
        readonly IModule module;
        public bool CommandExecuted;
        public bool CommandUndone;

        public TestCommand(TestCommandType type, IModule module) :
            this(type, module, Option.None<TestPlanRecorder>())
        {
        }

        public TestCommand(TestCommandType type, IModule module, Option<TestPlanRecorder> recorder)
        {
            this.type = type;
            this.module = Preconditions.CheckNotNull(module, nameof(module));
            this.recorder = recorder;
            this.CommandExecuted = false;
            this.CommandUndone = false;
        }

        public string Show() => $"TestCommand {this.type.ToString()}:{this.module.Name}";

        public Task ExecuteAsync(CancellationToken token)
        {
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