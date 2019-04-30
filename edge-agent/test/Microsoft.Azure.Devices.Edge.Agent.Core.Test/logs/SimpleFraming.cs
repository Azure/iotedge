// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System;
    using System.Collections.Generic;

    public static class SimpleFraming
    {
        public static IList<byte[]> Parse(byte[] buf)
        {
            int ctr = 0;
            var list = new List<byte[]>();
            while (ctr < buf.Length)
            {
                byte[] lenBytes = new ArraySegment<byte>(buf, ctr, 4).ToArray();
                ctr += 4;
                int len = GetLen(lenBytes);
                byte[] payloadBytes = new ArraySegment<byte>(buf, ctr, len).ToArray();
                ctr += len;
                list.Add(payloadBytes);
            }

            return list;
        }

        static int GetLen(byte[] bytes)
        {
            Array.Reverse(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
    }
}
