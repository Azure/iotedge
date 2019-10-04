// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.Uds
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    sealed class UnixDomainSocketEndPoint : EndPoint
    {
        const AddressFamily EndPointAddressFamily = AddressFamily.Unix;

        static readonly Encoding PathEncoding = Encoding.UTF8;

        static readonly int NativePathOffset = 2; // = offsetof(struct sockaddr_un, sun_path). It's the same on Linux and OSX

        static readonly int NativePathLength = 91; // sockaddr_un.sun_path at http://pubs.opengroup.org/onlinepubs/9699919799/basedefs/sys_un.h.html, -1 for terminator

        static readonly int NativeAddressSize = NativePathOffset + NativePathLength;

        readonly string path;

        readonly byte[] encodedPath;

        public UnixDomainSocketEndPoint(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            this.path = path;
            this.encodedPath = PathEncoding.GetBytes(this.path);

            if (path.Length == 0 || this.encodedPath.Length > NativePathLength)
            {
                throw new ArgumentOutOfRangeException(nameof(path), path);
            }
        }

        internal UnixDomainSocketEndPoint(SocketAddress socketAddress)
        {
            if (socketAddress == null)
            {
                throw new ArgumentNullException(nameof(socketAddress));
            }

            if (socketAddress.Family != EndPointAddressFamily ||
                socketAddress.Size > NativeAddressSize)
            {
                throw new ArgumentOutOfRangeException(nameof(socketAddress));
            }

            if (socketAddress.Size > NativePathOffset)
            {
                this.encodedPath = new byte[socketAddress.Size - NativePathOffset];
                for (int i = 0; i < this.encodedPath.Length; i++)
                {
                    this.encodedPath[i] = socketAddress[NativePathOffset + i];
                }

                this.path = PathEncoding.GetString(this.encodedPath, 0, this.encodedPath.Length);
            }
            else
            {
                this.encodedPath = Array.Empty<byte>();
                this.path = string.Empty;
            }
        }

        public override AddressFamily AddressFamily => EndPointAddressFamily;

        public override SocketAddress Serialize()
        {
            var result = new SocketAddress(AddressFamily.Unix, NativeAddressSize);
            Debug.Assert(this.encodedPath.Length + NativePathOffset <= result.Size, "Expected path to fit in address");

            for (int index = 0; index < this.encodedPath.Length; index++)
            {
                result[NativePathOffset + index] = this.encodedPath[index];
            }

            result[NativePathOffset + this.encodedPath.Length] = 0; // path must be null-terminated

            return result;
        }

        public override EndPoint Create(SocketAddress socketAddress) => new UnixDomainSocketEndPoint(socketAddress);

        public override string ToString() => this.path;
    }
}
