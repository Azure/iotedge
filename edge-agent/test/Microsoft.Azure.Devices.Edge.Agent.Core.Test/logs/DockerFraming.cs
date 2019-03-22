// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Test.Logs
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class DockerFraming
    {
        public static byte[] Frame(IEnumerable<string> logTexts)
        {
            var outputBytes = new List<byte>();
            foreach (string text in logTexts)
            {
                outputBytes.AddRange(Frame(text));
            }

            return outputBytes.ToArray();
        }

        public static byte[] Frame(string text)
        {
            byte streamByte = 01;
            var padding = new byte[3];
            var outputBytes = new List<byte>();
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            byte[] lenBytes = GetLengthBytes(textBytes.Length);
            outputBytes.Add(streamByte);
            outputBytes.AddRange(padding);
            outputBytes.AddRange(lenBytes);
            outputBytes.AddRange(textBytes);
            return outputBytes.ToArray();
        }

        static byte[] GetLengthBytes(int len)
        {
            byte[] intBytes = BitConverter.GetBytes(len);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(intBytes);
            }

            byte[] result = intBytes;
            return result;
        }
    }
}
