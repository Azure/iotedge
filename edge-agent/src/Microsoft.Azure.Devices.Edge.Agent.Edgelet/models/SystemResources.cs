// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Edgelet.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices.Edge.Util;
    using Newtonsoft.Json;

    public class SystemResources
    {
        public SystemResources(long hostUptime, long iotEdgedUptime, double usedCpu, long usedRam, long totalRam, Disk[] disks, string dockerStats)
        {
            this.HostUptime = Preconditions.CheckNotNull(hostUptime, nameof(hostUptime));
            this.IotEdgedUptime = Preconditions.CheckNotNull(iotEdgedUptime, nameof(iotEdgedUptime));
            this.UsedCpu = Preconditions.CheckNotNull(usedCpu, nameof(usedCpu));
            this.UsedRam = Preconditions.CheckNotNull(usedRam, nameof(usedRam));
            this.TotalRam = Preconditions.CheckNotNull(totalRam, nameof(totalRam));
            this.Disks = Preconditions.CheckNotNull(disks, nameof(disks));
            this.ModuleStats = Preconditions.CheckNotNull(dockerStats, nameof(dockerStats));
        }

        public long HostUptime { get; }

        public long IotEdgedUptime { get; }

        public double UsedCpu { get; }

        public long UsedRam { get; }

        public long TotalRam { get; }

        public Disk[] Disks { get; }

        public string ModuleStats { get; }
    }

    public class Disk
    {
        public Disk(string name, long availableSpace, long totalSpace, string fileSystem, string fileType)
        {
            this.Name = Preconditions.CheckNotNull(name, nameof(name));
            this.AvailableSpace = Preconditions.CheckNotNull(availableSpace, nameof(availableSpace));
            this.TotalSpace = Preconditions.CheckNotNull(totalSpace, nameof(totalSpace));
            this.FileSystem = Preconditions.CheckNotNull(fileSystem, nameof(fileSystem));
            this.FileType = Preconditions.CheckNotNull(fileType, nameof(fileType));
        }

        public string Name { get; }

        public long AvailableSpace { get; }

        public long TotalSpace { get; }

        public string FileSystem { get; }

        public string FileType { get; }
    }
}
