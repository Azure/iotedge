use bytes::Bytes;
use reqwest::Response;

enum BlobKind {
    Streaming(Response),
    Immediate(Option<Bytes>),
}

pub struct Blob {
    inner: BlobKind,
}

impl Blob {
    /// Create a new streaming Blob from a response
    pub(crate) fn new(res: Response) -> Blob {
        Blob {
            inner: BlobKind::Streaming(res),
        }
    }

    /// Create a new Immediate blob from a set of bytes
    pub(crate) fn new_immediate(data: Bytes) -> Blob {
        Blob {
            inner: BlobKind::Immediate(Some(data)),
        }
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

    /// Get the full blob as Bytes.
    pub async fn bytes(self) -> reqwest::Result<Bytes> {
        match self.inner {
            BlobKind::Streaming(res) => res.bytes().await,
            BlobKind::Immediate(data) => Ok(data.unwrap_or_default()),
        }
    }

    /// Return the length of the blob, if known.
    ///
    /// Reasons it may not be known
    /// - The server didn't send a content-length header.
    /// - The response is gzipped and automatically decoded (thus changing the
    ///   actual decoded length).
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
}
