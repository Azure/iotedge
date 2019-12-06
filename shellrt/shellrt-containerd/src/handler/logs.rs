use log::*;

use cri_grpc::{client::RuntimeServiceClient, ContainerStatusRequest};
use shellrt_api::v0::{request, response};

use crate::error::*;
use crate::util::module_to_container_id;

pub struct LogsHandler {
    grpc_uri: String,
}

impl LogsHandler {
    pub fn new(grpc_uri: String) -> LogsHandler {
        LogsHandler { grpc_uri }
    }

    pub async fn handle(
        self,
        log_options: request::Logs,
    ) -> Result<(response::Logs, Option<crate::ResponseThunk>)> {
        let mut cri_client = RuntimeServiceClient::connect(self.grpc_uri.clone())
            .await
            .context(ErrorKind::GrpcConnect)?;

        let status = cri_client
            .container_status(ContainerStatusRequest {
                container_id: module_to_container_id(cri_client.clone(), &log_options.name).await?,
                verbose: false,
            })
            .await
            .context(ErrorKind::GrpcConnect)?
            .into_inner()
            .status
            .expect("somehow received a null status response");

        debug!("Opening log file: {}", status.log_path);
        let mut logs = tokio::fs::File::open(status.log_path)
            .await
            .context(ErrorKind::OpenLogFile)?;

        let thunk: crate::ResponseThunk = Box::new(move |mut output| {
            Box::pin(async move {
                // TODO: implement the various log options (i.e: tail, since, follow)
                //
                // this is a non-trivial set of operations, and will be tricky to get right...
                // - "tail" and "since" shouldn't be too tricky to implement:
                //   - Seek to the end of the file
                //   - Read backwards until the specified time/number of lines is reached
                //     - There's not built-in way to reverse-read a file, but it's doable
                // - "follow" will be the trickiest to implement...
                //   - The current naiive implementation closes the `logs` stream the moment an
                //     EOF is encountered. What _should_ happen is that once the EOF is
                //     encountered, the implementation waits for a bit, and tries to read the
                //     file again, checking if any new data has appeared. This gets even more
                //     complicated once you factor in log-rotation, which might swap out the
                //     actual file descriptor the reader is backed by...
                //   - Another complication is around how to _end_ the log stream if the
                //     container is killed. Docker's leverages its internal pub/sub mechanism to
                //     terminate the stream, but that's not directly exposed anywhere.
                //     - Option 1: periodically perform a container status request to see if the
                //       container is still alive
                //     - Option 2: go through the process of setting up the containerd
                //       `events.proto` infrastructure, and get notified once a container dies
                //       via gRPC.
                //
                // This is quite a bit of work, and given that I end my internship in less than
                // 2 weeks, it's probably best to leave this basic implementation as-is, and try
                // to get more core functionality up and running.
                tokio_polyfill::copy(&mut logs, &mut output).await.map(drop)
            })
        });

        Ok((response::Logs {}, Some(thunk)))
    }
}

// XXX: remove once on tokio 0.2.x (non alpha)
mod tokio_polyfill {
    use tokio::io::{AsyncRead, AsyncWrite};

    use std::future::Future;
    use std::io;
    use std::pin::Pin;
    use std::task::{Context, Poll};

    /// A future that asynchronously copies the entire contents of a reader into
    /// a writer.
    ///
    /// This struct is generally created by calling [`copy`][copy]. Please
    /// see the documentation of `copy()` for more details.
    ///
    /// [copy]: fn.copy.html
    #[derive(Debug)]
    #[must_use = "futures do nothing unless you `.await` or poll them"]
    pub struct Copy<'a, R: ?Sized, W: ?Sized> {
        reader: &'a mut R,
        read_done: bool,
        writer: &'a mut W,
        pos: usize,
        cap: usize,
        amt: u64,
        buf: Box<[u8]>,
    }

    /// Asynchronously copies the entire contents of a reader into a writer.
    ///
    /// This function returns a future that will continuously read data from
    /// `reader` and then write it into `writer` in a streaming fashion until
    /// `reader` returns EOF.
    ///
    /// On success, the total number of bytes that were copied from `reader` to
    /// `writer` is returned.
    ///
    /// This is an asynchronous version of [`std::io::copy`][std].
    ///
    /// # Errors
    ///
    /// The returned future will finish with an error will return an error
    /// immediately if any call to `poll_read` or `poll_write` returns an error.
    ///
    /// # Examples
    ///
    /// ```
    /// use tokio::io;
    ///
    /// # async fn dox() -> std::io::Result<()> {
    /// let mut reader: &[u8] = b"hello";
    /// let mut writer: Vec<u8> = vec![];
    ///
    /// io::copy(&mut reader, &mut writer).await?;
    ///
    /// assert_eq!(&b"hello"[..], &writer[..]);
    /// # Ok(())
    /// # }
    /// ```
    ///
    /// [std]: https://doc.rust-lang.org/std/io/fn.copy.html
    pub fn copy<'a, R, W>(reader: &'a mut R, writer: &'a mut W) -> Copy<'a, R, W>
    where
        R: AsyncRead + Unpin + ?Sized,
        W: AsyncWrite + Unpin + ?Sized,
    {
        Copy {
            reader,
            read_done: false,
            writer,
            amt: 0,
            pos: 0,
            cap: 0,
            buf: Box::new([0; 2048]),
        }
    }

    macro_rules! ready {
        ($e:expr $(,)?) => {
            match $e {
                std::task::Poll::Ready(t) => t,
                std::task::Poll::Pending => return std::task::Poll::Pending,
            }
        };
    }

    impl<R, W> Future for Copy<'_, R, W>
    where
        R: AsyncRead + Unpin + ?Sized,
        W: AsyncWrite + Unpin + ?Sized,
    {
        type Output = io::Result<u64>;

        fn poll(mut self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<io::Result<u64>> {
            loop {
                // If our buffer is empty, then we need to read some data to
                // continue.
                if self.pos == self.cap && !self.read_done {
                    let me = &mut *self;
                    let n = ready!(Pin::new(&mut *me.reader).poll_read(cx, &mut me.buf))?;
                    if n == 0 {
                        self.read_done = true;
                    } else {
                        self.pos = 0;
                        self.cap = n;
                    }
                }

                // If our buffer has some data, let's write it out!
                while self.pos < self.cap {
                    let me = &mut *self;
                    let i =
                        ready!(Pin::new(&mut *me.writer).poll_write(cx, &me.buf[me.pos..me.cap]))?;
                    if i == 0 {
                        return Poll::Ready(Err(io::Error::new(
                            io::ErrorKind::WriteZero,
                            "write zero byte into writer",
                        )));
                    } else {
                        self.pos += i;
                        self.amt += i as u64;
                    }
                }

                // If we've written all the data and we've seen EOF, flush out the
                // data and finish the transfer.
                if self.pos == self.cap && self.read_done {
                    let me = &mut *self;
                    ready!(Pin::new(&mut *me.writer).poll_flush(cx))?;
                    return Poll::Ready(Ok(self.amt));
                }
            }
        }
    }
}
