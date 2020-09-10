use std::{
    pin::Pin,
    task::{Context, Poll},
};

use futures_util::{
    stream::{Fuse, Stream},
    StreamExt,
};
use pin_project::pin_project;

/// This function will attempt to pull items from both streams in ordered
/// fasion. First stream will be polled until it is able to yield an item.
/// Second stream will be polled only when there are no items available in
/// the first stream.
///
/// After one of the two input stream completes, the remaining one will
/// **not** be polled. The returned stream completes when either of input
/// streams have completed.
///
/// Note that this function consumes both streams and returns a wrapped
/// version of them.
pub fn select_ordered<St1, St2>(stream1: St1, stream2: St2) -> SelectOrdered<St1, St2>
where
    St1: Stream,
    St2: Stream<Item = St1::Item>,
{
    SelectOrdered {
        stream1: stream1.fuse(),
        stream2: stream2.fuse(),
    }
}

/// Stream for the [`select_ordered()`] function.
#[pin_project(project = SelectOrderedProj)]
#[must_use = "streams do nothing unless polled"]
pub struct SelectOrdered<St1, St2> {
    #[pin]
    stream1: Fuse<St1>,

    #[pin]
    stream2: Fuse<St2>,
}

impl<St1, St2> Stream for SelectOrdered<St1, St2>
where
    St1: Stream,
    St2: Stream<Item = St1::Item>,
{
    type Item = St1::Item;

    fn poll_next(self: Pin<&mut Self>, cx: &mut Context<'_>) -> Poll<Option<Self::Item>> {
        let SelectOrderedProj { stream1, stream2 } = self.project();

        // poll next item from the first stream
        let a_done = match stream1.poll_next(cx) {
            Poll::Ready(Some(item)) => {
                return Poll::Ready(Some(item));
            }
            Poll::Ready(None) => true,
            Poll::Pending => false,
        };

        // if the first stream is not ready to yield, poll second stream
        match stream2.poll_next(cx) {
            Poll::Ready(Some(item)) => Poll::Ready(Some(item)),
            Poll::Ready(None) if a_done => Poll::Ready(None),
            Poll::Ready(None) | Poll::Pending => Poll::Pending,
        }
    }
}
