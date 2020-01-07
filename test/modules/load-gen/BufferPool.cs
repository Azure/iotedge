// Copyright (c) Microsoft. All rights reserved.
namespace LoadGen
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;
    using Serilog;

    public class BufferPool
    {
        readonly ConcurrentDictionary<ulong, List<Buffer>> buffers = new ConcurrentDictionary<ulong, List<Buffer>>();

        public Buffer AllocBuffer(ulong size)
        {
            List<Buffer> buffers = this.buffers.GetOrAdd(
                size,
                bufferSize =>
                {
                    var list = new List<Buffer>
                    {
                        new Buffer(bufferSize)
                    };

                    Log.Debug($"Allocated new list & buffer [{list[0].Id}] of size {bufferSize}");
                    return list;
                });

            lock (buffers)
            {
                Buffer buffer = buffers.FirstOrDefault(buf => buf.InUse.Get() == false);

                if (buffer == null)
                {
                    buffer = new Buffer(size);
                    buffers.Add(buffer);

                    Log.Debug($"Allocated buffer [{buffer.Id}] of size {size}");
                }

                buffer.InUse.Set(true);
                return buffer;
            }
        }
    }

    public class Buffer : IDisposable
    {
        static long bufferIdCounter = 0;

        readonly byte[] buffer;

        public Buffer(ulong size)
        {
            this.buffer = new byte[size];
            this.InUse = new AtomicBoolean(false);
            this.Id = Interlocked.Increment(ref bufferIdCounter);
        }

        public AtomicBoolean InUse { get; set; }

        public long Id { get; }

        public byte[] Data
        {
            get { return this.buffer; }
        }

        public void Dispose()
        {
            this.InUse.Set(false);
        }
    }
}
