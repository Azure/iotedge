using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Azure.Devices.Edge.Util.Concurrency;
using Serilog;

namespace LoadGen
{
    public class BufferPool
    {
        private ConcurrentDictionary<ulong, List<Buffer>> buffers = new ConcurrentDictionary<ulong, List<Buffer>>();

        public Buffer AllocBuffer(ulong size)
        {
            List<Buffer> buffers = this.buffers.GetOrAdd(size, (bufSize) =>
            {
                var list = new List<Buffer>
                {
                    new Buffer(bufSize)
                };

                Log.Information($"Allocated new list & buffer [{list[0].Id}] of size {size}");
                return list;
            });

            lock (buffers)
            {
                Buffer buffer = buffers
                    .Where(buf => buf.InUse.Get() == false)
                    .FirstOrDefault();

                if (buffer == null)
                {
                    buffer = new Buffer(size);
                    buffers.Add(buffer);

                    Log.Information($"Allocated buffer [{buffer.Id}] of size {size}");
                }

                buffer.InUse.Set(true);
                return buffer;
            }
        }
    }

    public class Buffer : IDisposable
    {
        static long BufferIdCounter = 0;

        private byte[] buffer;

        public AtomicBoolean InUse { get; set; }

        public long Id { get; }

        public byte[] Data
        {
            get { return this.buffer; }
        }

        public Buffer(ulong size)
        {
            this.buffer = new byte[size];
            this.InUse = new AtomicBoolean(false);
            this.Id = Interlocked.Increment(ref BufferIdCounter);
        }

        public void Dispose()
        {
            this.InUse.Set(false);
        }
    }
}
