// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using Microsoft.Azure.Devices.Edge.Util;
    using System;

	public class ModuleSetChangedArgs : EventArgs
	{
		public Diff Diff { get; }
		public ModuleSet ModuleSet { get; }

		public ModuleSetChangedArgs(Diff diff, ModuleSet moduleSet)
		{
			this.Diff = Preconditions.CheckNotNull(diff, nameof(diff));
			this.ModuleSet = Preconditions.CheckNotNull(moduleSet, nameof(moduleSet));
		}
	}
}
