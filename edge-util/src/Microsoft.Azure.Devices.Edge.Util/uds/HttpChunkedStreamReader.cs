// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Uds
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    class HttpChunkedStreamReader : Stream
    {
        readonly HttpBufferedStream stream;
        int chunkBytes;
        bool eos;

        public HttpChunkedStreamReader(HttpBufferedStream stream)
        {
            this.stream = Preconditions.CheckNotNull(stream, nameof(stream));
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (this.eos)
            {
                return 0;
            }

            if (this.chunkBytes == 0)
            {
                string line = await this.stream.ReadLineAsync(cancellationToken);
                if (!int.TryParse(line, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out this.chunkBytes))
                {
                    throw new IOException($"Cannot parse chunk header - {line}");
                }
            }

            int bytesRead = 0;
            if (this.chunkBytes > 0)
            {
                int bytesToRead = Math.Min(count, this.chunkBytes);
                bytesRead = await this.stream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);
                if (bytesToRead == 0)
                {
                    throw new EndOfStreamException();
                }

                this.chunkBytes -= bytesToRead;
            }

            if (this.chunkBytes == 0)
            {
                await this.stream.ReadLineAsync(cancellationToken);
                if (bytesRead == 0)
                {
                    this.eos = true;
                }
            }

            return bytesRead;
        }

        public override void Flush() => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (this.eos)
            {
                return 0;
            }

            if (this.chunkBytes == 0)
            {
                string line = this.stream.ReadLine();
                if (!int.TryParse(line, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out this.chunkBytes))
                {
                    throw new IOException($"Cannot parse chunk header - {line}");
                }
            }

            int bytesRead = 0;
            if (this.chunkBytes > 0)
            {
                int bytesToRead = Math.Min(count, this.chunkBytes);
                bytesRead = this.stream.Read(buffer, offset, bytesToRead);
                if (bytesToRead == 0)
                {
                    throw new EndOfStreamException();
                }

                this.chunkBytes -= bytesToRead;
            }

            if (this.chunkBytes == 0)
            {
                this.stream.ReadLine();
                if (bytesRead == 0)
                {
                    this.eos = true;
                }
            }

            return bytesRead;
        }

        // Underlying Stream does not support Seek()
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            this.stream.Dispose();
        }
    }
}
