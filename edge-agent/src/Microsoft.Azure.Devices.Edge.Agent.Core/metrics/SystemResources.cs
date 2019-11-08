// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Azure.Devices.Edge.Agent.Core.Metrics
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class SystemResources
    {
        public SystemResources(double usedRam, double totalRam, Disk[] disks)
        {
            this.UsedRam = usedRam;
            this.TotalRam = totalRam;
            this.Disks = disks;
        }

        public double UsedRam { get; }

        public double TotalRam { get; }

        public Disk[] Disks { get; }
    }

    public class Disk
    {
        public Disk(string name, double availableSpace, double totalSpace, string fileSystem, string fileType)
        {
            this.Name = name;
            this.AvailableSpace = availableSpace;
            this.TotalSpace = totalSpace;
            this.FileSystem = fileSystem;
            this.FileType = fileType;
        }

        public string Name { get; }

        public double AvailableSpace { get; }

        public double TotalSpace { get; }

        public string FileSystem { get; }

        public string FileType { get; }
    }
}
