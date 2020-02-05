use bytes::Bytes;
use futures::{stream, Stream};
use reqwest::Response;

use oci_image::v1::Descriptor;

#[derive(Debug)]
enum BlobKind {
    Streaming(Response),
    Immediate(Option<Bytes>),
}

/// A Blob associates a datastream with it's OCI descriptor.
#[derive(Debug)]
pub struct Blob {
    descriptor: Descriptor,
    inner: BlobKind,
}

impl Blob {
    /// Create a new streaming Blob from a response
    // TODO: use Stream instead of Response if/when reqwest stabilizes the impl
    pub fn new_streaming(res: Response, descriptor: Descriptor) -> Blob {
        Blob {
            descriptor,
            inner: BlobKind::Streaming(res),
        }
    }

    /// Create a new Immediate blob from a set of bytes
    pub fn new_immediate(data: Bytes, descriptor: Descriptor) -> Blob {
        Blob {
            descriptor,
            inner: BlobKind::Immediate(Some(data)),
        }
    }

    /// Get a reference to the blob's Descriptor.
    pub fn descriptor(&self) -> &Descriptor {
        &self.descriptor
    }

    /// Return the length of the blob, if known.
    ///
    /// Reasons it may not be known
    /// - The server didn't send a content-length header.
    /// - The response is gzipped and automatically decoded (thus changing the
    ///   actual decoded length).
    ///
    /// **NOTE:** this may be different from `descriptor().size`, as Blobs can
    /// represent partial downloads!
    pub fn len(&self) -> Option<u64> {
        match &self.inner {
            BlobKind::Streaming(res) => res.content_length(),
            BlobKind::Immediate(data) => data.as_ref().map(|b| b.len() as u64),
        }
    }

    /// Return if the blob is empty, if known.
    pub fn is_empty(&self) -> Option<bool> {
        self.len().map(|len| len == 0)
    }

    /// Stream a chunk of the blob.
    ///
    /// When the blob has been exhausted, this will return None.
    pub async fn chunk(&mut self) -> reqwest::Result<Option<Bytes>> {
        match &mut self.inner {
            BlobKind::Streaming(res) => res.chunk().await,
            BlobKind::Immediate(data) => Ok(data.take()),
        }
    }

    /// Consumes self, and downloads the entire blob.
    ///
    /// **WARNING:** Blobs can range in size from a few Kb (e.g: a manifest) to
    /// several Gb (e.g: an OS base layer). Calling `.bytes()` on the latter
    /// case can quickly use up system resources. When working with large blobs,
    /// use `.chunk()` or `into_steam()` instead.
    pub async fn bytes(self) -> reqwest::Result<Bytes> {
        match self.inner {
            BlobKind::Streaming(res) => res.bytes().await,
            BlobKind::Immediate(data) => Ok(data.unwrap_or_default()),
        }
    }

    /// Consumes self, returning a Stream of Bytes.
    pub fn into_stream(self) -> impl Stream<Item = reqwest::Result<Bytes>> {
        stream::unfold((self, false), |(mut blob, has_errored)| {
            async move {
                if has_errored {
                    return None;
                }
                match blob.chunk().await {
                    Ok(Some(val)) => Some((Ok(val), (blob, false))),
                    Ok(None) => None,
                    Err(e) => Some((Err(e), (blob, true))),
                }
            }
        })
    }
}
