//// Copyright (c) Microsoft. All rights reserved.
//namespace Microsoft.Azure.Devices.Edge.Util.Uds
//{
//    using System.Collections.Generic;
//    using System.IO;
//    using System.Net;
//    using System.Net.Http;
//    using System.Threading.Tasks;

//    public class HttpResponseContent : HttpContent
//    {
//        Stream responseStream;

//        public HttpResponseContent()
//        {

//        }

//        internal static HttpResponseContent Create(bool isChunked, Stream stream, IDictionary<string, string> contentHeaders)
//        {
            
//        }

//        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
//            => this.responseStream.CopyToAsync(stream);

//        protected override bool TryComputeLength(out long length)
//        {
//            length = 0;
//            return false;
//        }

//        protected override void Dispose(bool disposing)
//        {
//            try
//            {
//                if (disposing)
//                {
//                    this.responseStream.Dispose();
//                }
//            }
//            finally
//            {
//                base.Dispose(disposing);
//            }
//        }
//    }
//}
