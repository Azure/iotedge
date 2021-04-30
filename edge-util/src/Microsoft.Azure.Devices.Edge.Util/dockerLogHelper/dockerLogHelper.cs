// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Util.DockerLogHelper
{
    using System.IO;
    using System.Threading.Tasks;

    public sealed class DockerLogHelper
    {
        public static Task<Stream> GetLogTail(Stream originalStream, int numOfLine)
        {
            // Implement my own tail:
            // 1. Seek to the end of the stream, find t-number of newline backwards from the end
            // 2. Move the seek cursor to the beginning of the t-number of line
            int count = 0;
            byte[] buffer = new byte[1];
            bool isBeyondStream = false;

            Stream stream = new MemoryStream();
            originalStream.CopyTo(stream);

            // read to the end.
            stream.Seek(0, SeekOrigin.End);

            // read backwards (numOfLine+1) lines
            while (count <= numOfLine)
            {
                try
                {
                    stream.Seek(-1, SeekOrigin.Current);
                }
                catch (IOException)
                {
                    // this can happen if the seek goes beyond the beginning of the stream
                    isBeyondStream = true;
                    break;
                }

                stream.Read(buffer, 0, 1);
                if (buffer[0] == '\n')
                {
                    count++;
                }

                // stream.Read() advances the position, so we need to go back again
                stream.Seek(-1, SeekOrigin.Current);
            }

            if (!isBeyondStream)
            {
                // go past the last '\n'
                stream.Seek(1, SeekOrigin.Current);
            }
            else
            {
                stream.Seek(0, SeekOrigin.Begin);
            }

            return Task.Run(() => stream);
        }
    }
}