use std::convert::TryInto;

use chrono::{DateTime, Duration, Local};
use failure::ResultExt;
use humantime::parse_duration;

use crate::error::{Error, ErrorKind};

pub fn parse_since(since: &str) -> Result<i32, Error> {
    if let Ok(datetime) = DateTime::parse_from_rfc3339(since) {
        let temp: Result<i32, _> = datetime.timestamp().try_into();
        Ok(temp.context(ErrorKind::ParseSince)?)
    } else if let Ok(epoch) = since.parse() {
        Ok(epoch)
    } else if let Ok(duration) = parse_duration(since) {
        let nano: Result<i64, _> = duration.as_nanos().try_into();
        let nano = nano.context(ErrorKind::ParseSince)?;

        let temp: Result<i32, _> = (Local::now() - Duration::nanoseconds(nano))
            .timestamp()
            .try_into();
        Ok(temp.context(ErrorKind::ParseSince)?)
    } else {
        Err(Error::from(ErrorKind::ParseSince))
    }
}

#[cfg(test)]
mod tests {
    use super::{parse_since, Duration, Local, TryInto};

    #[test]
    fn parse_rfc3339() {
        assert_eq!(
            parse_since("2019-09-27T16:00:00+00:00").unwrap(),
            1_569_600_000
        );
    }

    #[test]
    fn parse_english() {
        assert_near(
            parse_since("1 hour").unwrap(),
            (Local::now() - Duration::hours(1))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );

        assert_near(
            parse_since("1h").unwrap(),
            (Local::now() - Duration::hours(1))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );

        assert_near(
            parse_since("1 hour 20 minutes").unwrap(),
            (Local::now() - Duration::hours(1) - Duration::minutes(20))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );

        assert_near(
            parse_since("1 hour 20m").unwrap(),
            (Local::now() - Duration::hours(1) - Duration::minutes(20))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );

        assert_near(
            parse_since("1 day").unwrap(),
            (Local::now() - Duration::days(1))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );

        assert_near(
            parse_since("1d").unwrap(),
            (Local::now() - Duration::days(1))
                .timestamp()
                .try_into()
                .unwrap(),
            10,
        );
    }

    #[test]
    fn parse_unix() {
        assert_eq!(parse_since("1569600000").unwrap(), 1_569_600_000);
    }

    #[test]
    fn parse_default() {
        let _ = parse_since("asdfasdf").unwrap_err();
    }

    fn assert_near(a: i32, b: i32, tol: i32) {
        assert!((a - b).abs() < tol)
    }
}
