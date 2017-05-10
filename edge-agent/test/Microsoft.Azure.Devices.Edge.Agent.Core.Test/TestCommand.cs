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
        public readonly TestCommandType testType;
        public readonly IModule module;

        public TestRecordType(TestCommandType testType, IModule module)
        {
            this.testType = testType;
            this.module = Preconditions.CheckNotNull(module, nameof(module));
        }

        public bool Equals(TestRecordType other) => this.testType == other.testType && Equals(this.module, other.module);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is TestRecordType && Equals((TestRecordType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)this.testType * 397) ^ (this.module != null ? this.module.GetHashCode() : 0);
            }
        }
    }

    public class TestPlanRecorder 
    {
        public List<TestRecordType> ExecutionList { get; }
        public List<TestRecordType> UndoList { get; }

        public TestPlanRecorder()
        {
            this.ExecutionList = new List<TestRecordType>();
            this.UndoList = new List<TestRecordType>();
        }

        public void ModuleExecuted(TestCommandType type, IModule module) => this.ExecutionList.Add(new TestRecordType(type, module));

        public void ModuleUndone(TestCommandType type, IModule module) => this.UndoList.Add(new TestRecordType(type, module));
    }

    public class TestCommandFactory : ICommandFactory
    {
        public Option<TestPlanRecorder> Recorder { get; }

        public TestCommandFactory()
        {
            this.Recorder = Option.Some<TestPlanRecorder>(new TestPlanRecorder());
        }

        public ICommand Create(IModule module)
        {
            Assert.True(module is TestModule);
            return new TestCommand(TestCommandType.TestCreate, module,  this.Recorder);
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
    }

    public class TestCommand : ICommand
    {
        readonly Option<TestPlanRecorder> Recorder;
        readonly TestCommandType type;
        readonly IModule Module;
        public bool CommandExecuted;
        public bool CommandUndone;

        public TestCommand(TestCommandType type, IModule module)
        : this(type, module, Option.None<TestPlanRecorder>())
        {
        }

        public TestCommand(TestCommandType type, IModule module, Option<TestPlanRecorder> recorder)
        {
            this.type = type;
            this.Module = Preconditions.CheckNotNull(module, nameof(module));
            this.Recorder = recorder;
            this.CommandExecuted = false;
            this.CommandUndone = false;
        }

        public string Show() => $"TestCommand {this.type.ToString()}:{this.Module.Name}";

        public Task ExecuteAsync(CancellationToken token)
        {
            this.Recorder.ForEach(r => r.ModuleExecuted(this.type, this.Module));
            this.CommandExecuted = true;
            return TaskEx.Done;
        }

        public Task UndoAsync(CancellationToken token)
        {
            this.Recorder.ForEach(r=> r.ModuleUndone(this.type, this.Module));
            this.CommandUndone = true;
            return TaskEx.Done;
        }
    }
}