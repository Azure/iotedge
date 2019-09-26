use std::error::Error;
use std::fmt;
use std::ops::{Bound, RangeBounds};
use std::str::FromStr;

/// A Range type that implements [FromStr].
/// Can only be instantiated via `parse()`.
#[derive(Clone, Copy, Hash, PartialEq, Eq)]
pub struct ParsableRange<T>((Bound<T>, Bound<T>));

impl<T: fmt::Debug> fmt::Debug for ParsableRange<T> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        use Bound::*;
        match (self.0).0 {
            Included(ref start) => write!(f, "{:?}", start)?,
            Excluded(ref start) => write!(f, "{:?}", start)?,
            Unbounded => {}
        }

        match (self.0).1 {
            Included(ref end) => write!(f, "..={:?}", end)?,
            Excluded(ref end) => write!(f, "..{:?}", end)?,
            Unbounded => write!(f, "..")?,
        }

        Ok(())
    }
}

impl<T> RangeBounds<T> for ParsableRange<T> {
    fn start_bound(&self) -> Bound<&T> {
        self.0.start_bound()
    }

    fn end_bound(&self) -> Bound<&T> {
        self.0.end_bound()
    }
}

/// Errors that may occur while parsing a ParsableRange from a &str.
#[derive(Debug)]
pub enum ParsableRangeError<T>
where
    T: FromStr,
    T::Err: fmt::Debug,
{
    Parse(T::Err),
    Invalid,
}

impl<T> fmt::Display for ParsableRangeError<T>
where
    T: FromStr + fmt::Debug,
    T::Err: fmt::Debug,
{
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ParsableRangeError::Parse(e) => write!(f, "Failed to parse range bound: {:?}", e),
            ParsableRangeError::Invalid => write!(f, "Invalid range syntax"),
        }
    }
}

impl<T> Error for ParsableRangeError<T>
where
    T: FromStr + fmt::Debug + fmt::Display,
    T::Err: Error,
{
    fn description(&self) -> &str {
        "Failed to parse ParsableRange"
    }

    fn cause(&self) -> Option<&dyn Error> {
        match self {
            ParsableRangeError::Parse(e) => Some(e),
            _ => None,
        }
    }
}

impl<T: FromStr + Default> std::str::FromStr for ParsableRange<T>
where
    T: FromStr + Default,
    T::Err: fmt::Debug,
{
    type Err = ParsableRangeError<T>;

    fn from_str(s: &str) -> Result<ParsableRange<T>, ParsableRangeError<T>> {
        let mut start_end = s.split("..");

        let start = start_end.next().ok_or(ParsableRangeError::Invalid)?;
        let end = start_end.next().ok_or(ParsableRangeError::Invalid)?;
        if start_end.next().is_some() {
            return Err(ParsableRangeError::Invalid);
        }

        let start: Bound<T> = if start.is_empty() {
            Bound::Unbounded
        } else {
            Bound::Included(start.parse().map_err(ParsableRangeError::Parse)?)
        };
        let end: Bound<T> = if end.is_empty() {
            Bound::Unbounded
        } else if end.starts_with('=') {
            Bound::Included(end[1..].parse().map_err(ParsableRangeError::Parse)?)
        } else {
            Bound::Excluded(end.parse().map_err(ParsableRangeError::Parse)?)
        };
        Ok(ParsableRange((start, end)))
    }
}
