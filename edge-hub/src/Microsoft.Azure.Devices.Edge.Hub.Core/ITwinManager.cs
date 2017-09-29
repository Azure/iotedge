// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
	using System.Threading.Tasks;

	public interface ITwinManager
	{
		Task<IMessage> GetTwinAsync(string id);

		Task UpdateDesiredPropertiesAsync(string id, IMessage twinCollection);

		Task UpdateReportedPropertiesAsync(string id, IMessage twinCollection);
	}
}
