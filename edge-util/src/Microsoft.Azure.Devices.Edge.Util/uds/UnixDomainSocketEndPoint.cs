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

        static readonly Encoding s_pathEncoding = Encoding.UTF8;

        static readonly int s_nativePathOffset = 2; // = offsetof(struct sockaddr_un, sun_path). It's the same on Linux and OSX

        static readonly int s_nativePathLength = 91; // sockaddr_un.sun_path at http://pubs.opengroup.org/onlinepubs/9699919799/basedefs/sys_un.h.html, -1 for terminator

        static readonly int s_nativeAddressSize = s_nativePathOffset + s_nativePathLength;

        readonly string _path;

        readonly byte[] _encodedPath;

        public UnixDomainSocketEndPoint(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            this._path = path;
            this._encodedPath = s_pathEncoding.GetBytes(this._path);

            if (path.Length == 0 || this._encodedPath.Length > s_nativePathLength)
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
                socketAddress.Size > s_nativeAddressSize)
            {
                throw new ArgumentOutOfRangeException(nameof(socketAddress));
            }

            if (socketAddress.Size > s_nativePathOffset)
            {
                this._encodedPath = new byte[socketAddress.Size - s_nativePathOffset];
                for (int i = 0; i < this._encodedPath.Length; i++)
                {
                    this._encodedPath[i] = socketAddress[s_nativePathOffset + i];
                }

                this._path = s_pathEncoding.GetString(this._encodedPath, 0, this._encodedPath.Length);
            }
            else
            {
                this._encodedPath = Array.Empty<byte>();
                this._path = string.Empty;
            }
        }

        public override AddressFamily AddressFamily => EndPointAddressFamily;

        public override SocketAddress Serialize()
        {
            var result = new SocketAddress(AddressFamily.Unix, s_nativeAddressSize);
            Debug.Assert(this._encodedPath.Length + s_nativePathOffset <= result.Size, "Expected path to fit in address");

            for (int index = 0; index < this._encodedPath.Length; index++)
            {
                result[s_nativePathOffset + index] = this._encodedPath[index];
            }

            result[s_nativePathOffset + this._encodedPath.Length] = 0; // path must be null-terminated

            return result;
        }

        public override EndPoint Create(SocketAddress socketAddress) => new UnixDomainSocketEndPoint(socketAddress);

        public override string ToString() => this._path;
    }
}
