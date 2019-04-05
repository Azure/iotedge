#![deny(rust_2018_idioms, warnings)]
#![deny(clippy::all, clippy::pedantic)]
#![allow(
    clippy::doc_markdown,
    clippy::module_name_repetitions,
    clippy::use_self
)]

use std::net::{SocketAddr, ToSocketAddrs, UdpSocket};

use failure::ResultExt;

mod error;
pub use error::{BadServerResponseReason, Error, ErrorKind};

/// The result of [`query`]
#[derive(Debug)]
pub struct SntpTimeQueryResult {
    pub local_clock_offset: chrono::Duration,
    pub round_trip_delay: chrono::Duration,
}

/// Executes an SNTP query against the NTPv3 server at the given address.
///
/// Ref: <https://tools.ietf.org/html/rfc2030>
pub fn query<A>(addr: &A) -> Result<SntpTimeQueryResult, Error>
where
    A: ToSocketAddrs,
{
    let addr = addr
        .to_socket_addrs()
        .map_err(|err| ErrorKind::ResolveNtpPoolHostname(Some(err)))?
        .next()
        .ok_or(ErrorKind::ResolveNtpPoolHostname(None))?;

    let socket = UdpSocket::bind("0.0.0.0:0").context(ErrorKind::BindLocalSocket)?;
    socket
        .set_read_timeout(Some(std::time::Duration::from_secs(10)))
        .context(ErrorKind::SetReadTimeoutOnSocket)?;
    socket
        .set_write_timeout(Some(std::time::Duration::from_secs(10)))
        .context(ErrorKind::SetWriteTimeoutOnSocket)?;

    let mut num_retries_remaining = 3;
    while num_retries_remaining > 0 {
        match query_inner(&socket, addr) {
            Ok(result) => return Ok(result),
            Err(err) => {
                let is_retriable = match err.kind() {
                    ErrorKind::SendClientRequest(err) | ErrorKind::ReceiveServerResponse(err) => {
                        err.kind() == std::io::ErrorKind::TimedOut || // Windows
                        err.kind() == std::io::ErrorKind::WouldBlock // Unix
                    }

                    _ => false,
                };
                if is_retriable {
                    num_retries_remaining -= 1;
                    if num_retries_remaining == 0 {
                        return Err(err);
                    }
                } else {
                    return Err(err);
                }
            }
        }
    }

    unreachable!();
}

fn query_inner(socket: &UdpSocket, addr: SocketAddr) -> Result<SntpTimeQueryResult, Error> {
    let request_transmit_timestamp = {
        let (buf, request_transmit_timestamp) = create_client_request();

        #[cfg(test)]
        std::thread::sleep(std::time::Duration::from_secs(5)); // simulate network delay

        let mut buf = &buf[..];
        while !buf.is_empty() {
            let sent = socket
                .send_to(buf, addr)
                .map_err(ErrorKind::SendClientRequest)?;
            buf = &buf[sent..];
        }

        request_transmit_timestamp
    };

    let result = {
        let mut buf = [0_u8; 48];

        {
            let mut buf = &mut buf[..];
            while !buf.is_empty() {
                let (received, received_from) = socket
                    .recv_from(buf)
                    .map_err(ErrorKind::ReceiveServerResponse)?;
                if received_from == addr {
                    buf = &mut buf[received..];
                }
            }
        }

        #[cfg(test)]
        std::thread::sleep(std::time::Duration::from_secs(5)); // simulate network delay

        parse_server_response(buf, request_transmit_timestamp)?
    };

    Ok(result)
}

fn create_client_request() -> ([u8; 48], chrono::DateTime<chrono::Utc>) {
    let sntp_epoch = sntp_epoch();

    let mut buf = [0_u8; 48];
    buf[0] = 0b00_011_011; // version_number: 3, mode: 3 (client)

    let transmit_timestamp = chrono::Utc::now();

    #[cfg(test)]
    let transmit_timestamp = transmit_timestamp - chrono::Duration::seconds(30); // simulate unsynced local clock

    let mut duration_since_sntp_epoch = transmit_timestamp - sntp_epoch;

    let integral_part = duration_since_sntp_epoch.num_seconds();
    duration_since_sntp_epoch =
        duration_since_sntp_epoch - chrono::Duration::seconds(integral_part);

    assert!(integral_part >= 0 && integral_part < i64::from(u32::max_value()));
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    let integral_part = (integral_part as u32).to_be_bytes();
    buf[40..44].copy_from_slice(&integral_part[..]);

    let fractional_part = duration_since_sntp_epoch
        .num_nanoseconds()
        .expect("can't overflow nanoseconds");
    let fractional_part = (fractional_part << 32) / 1_000_000_000;
    assert!(fractional_part >= 0 && fractional_part < i64::from(u32::max_value()));
    #[allow(clippy::cast_possible_truncation, clippy::cast_sign_loss)]
    let fractional_part = (fractional_part as u32).to_be_bytes();
    buf[44..48].copy_from_slice(&fractional_part[..]);

    let packet = Packet::parse(buf, sntp_epoch);
    #[cfg(test)]
    let packet = dbg!(packet);

    // Re-extract transmit timestamp from the packet. This may not be the same as the original `transmit_timestamp`
    // that was serialized into the packet due to rounding. Specifically, it's usually off by 1ns.
    let transmit_timestamp = packet.transmit_timestamp;

    (buf, transmit_timestamp)
}

fn parse_server_response(
    buf: [u8; 48],
    request_transmit_timestamp: chrono::DateTime<chrono::Utc>,
) -> Result<SntpTimeQueryResult, Error> {
    let sntp_epoch = sntp_epoch();

    let destination_timestamp = chrono::Utc::now();

    #[cfg(test)]
    let destination_timestamp = destination_timestamp - chrono::Duration::seconds(30); // simulate unsynced local clock

    let packet = Packet::parse(buf, sntp_epoch);
    #[cfg(test)]
    let packet = dbg!(packet);

    match packet.leap_indicator {
        0..=2 => (),
        leap_indicator => {
            return Err(
                ErrorKind::BadServerResponse(BadServerResponseReason::LeapIndicator(
                    leap_indicator,
                ))
                .into(),
            );
        }
    };

    if packet.version_number != 3 {
        return Err(
            ErrorKind::BadServerResponse(BadServerResponseReason::VersionNumber(
                packet.version_number,
            ))
            .into(),
        );
    }

    if packet.mode != 4 {
        return Err(ErrorKind::BadServerResponse(BadServerResponseReason::Mode(packet.mode)).into());
    }

    if packet.mode != 4 {
        return Err(ErrorKind::BadServerResponse(BadServerResponseReason::Mode(packet.mode)).into());
    }

    if packet.originate_timestamp != request_transmit_timestamp {
        return Err(
            ErrorKind::BadServerResponse(BadServerResponseReason::OriginateTimestamp {
                expected: request_transmit_timestamp,
                actual: packet.originate_timestamp,
            })
            .into(),
        );
    }

    Ok(SntpTimeQueryResult {
        local_clock_offset: ((packet.receive_timestamp - request_transmit_timestamp)
            + (packet.transmit_timestamp - destination_timestamp))
            / 2,

        round_trip_delay: (destination_timestamp - request_transmit_timestamp)
            - (packet.receive_timestamp - packet.transmit_timestamp),
    })
}

fn sntp_epoch() -> chrono::DateTime<chrono::Utc> {
    chrono::DateTime::<chrono::Utc>::from_utc(
        chrono::NaiveDate::from_ymd(1900, 1, 1).and_time(chrono::NaiveTime::from_hms(0, 0, 0)),
        chrono::Utc,
    )
}

#[derive(Debug)]
struct Packet {
    leap_indicator: u8,
    version_number: u8,
    mode: u8,
    stratum: u8,
    poll_interval: u8,
    precision: u8,
    root_delay: u32,
    root_dispersion: u32,
    reference_identifier: u32,
    reference_timestamp: chrono::DateTime<chrono::Utc>,
    originate_timestamp: chrono::DateTime<chrono::Utc>,
    receive_timestamp: chrono::DateTime<chrono::Utc>,
    transmit_timestamp: chrono::DateTime<chrono::Utc>,
}

impl Packet {
    fn parse(buf: [u8; 48], sntp_epoch: chrono::DateTime<chrono::Utc>) -> Self {
        let leap_indicator = (buf[0] & 0b11_000_000) >> 6;
        let version_number = (buf[0] & 0b00_111_000) >> 3;
        let mode = buf[0] & 0b00_000_111;
        let stratum = buf[1];
        let poll_interval = buf[2];
        let precision = buf[3];
        let root_delay = u32::from_be_bytes([buf[4], buf[5], buf[6], buf[7]]);
        let root_dispersion = u32::from_be_bytes([buf[8], buf[9], buf[10], buf[11]]);
        let reference_identifier = u32::from_be_bytes([buf[12], buf[13], buf[14], buf[15]]);
        let reference_timestamp = deserialize_timestamp(
            [
                buf[16], buf[17], buf[18], buf[19], buf[20], buf[21], buf[22], buf[23],
            ],
            sntp_epoch,
        );
        let originate_timestamp = deserialize_timestamp(
            [
                buf[24], buf[25], buf[26], buf[27], buf[28], buf[29], buf[30], buf[31],
            ],
            sntp_epoch,
        );
        let receive_timestamp = deserialize_timestamp(
            [
                buf[32], buf[33], buf[34], buf[35], buf[36], buf[37], buf[38], buf[39],
            ],
            sntp_epoch,
        );
        let transmit_timestamp = deserialize_timestamp(
            [
                buf[40], buf[41], buf[42], buf[43], buf[44], buf[45], buf[46], buf[47],
            ],
            sntp_epoch,
        );

        Packet {
            leap_indicator,
            version_number,
            mode,
            stratum,
            poll_interval,
            precision,
            root_delay,
            root_dispersion,
            reference_identifier,
            reference_timestamp,
            originate_timestamp,
            receive_timestamp,
            transmit_timestamp,
        }
    }
}

fn deserialize_timestamp(
    raw: [u8; 8],
    sntp_epoch: chrono::DateTime<chrono::Utc>,
) -> chrono::DateTime<chrono::Utc> {
    let integral_part = i64::from(u32::from_be_bytes([raw[0], raw[1], raw[2], raw[3]]));
    let fractional_part = i64::from(u32::from_be_bytes([raw[4], raw[5], raw[6], raw[7]]));
    let duration_since_sntp_epoch = chrono::Duration::nanoseconds(
        integral_part * 1_000_000_000 + ((fractional_part * 1_000_000_000) >> 32),
    );

    sntp_epoch + duration_since_sntp_epoch
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn it_works() -> Result<(), Error> {
        let SntpTimeQueryResult {
            local_clock_offset,
            round_trip_delay,
        } = query(&("pool.ntp.org", 123))?;

        println!("local clock offset: {}", local_clock_offset);
        println!("round-trip delay: {}", round_trip_delay);

        assert!(
            (local_clock_offset - chrono::Duration::seconds(30))
                .num_seconds()
                .abs()
                < 1
        );
        assert!(
            (round_trip_delay - chrono::Duration::seconds(10))
                .num_seconds()
                .abs()
                < 1
        );

        Ok(())
    }
}
