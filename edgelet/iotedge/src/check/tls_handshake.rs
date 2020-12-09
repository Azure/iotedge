use std::convert::TryInto;
use std::net::TcpStream;
use std::str::FromStr;

use failure::{format_err, Context, Error, ResultExt};
use futures::Future;
use hyper::client::connect::{Connect, Destination};
use hyper::Uri;
use hyper_proxy::{Intercept, Proxy, ProxyConnector};
use hyper_tls::HttpsConnector;
use typed_headers::Credentials;

// Resolves the given `ToSocketAddrs`, then connects to the first address via TCP and completes a TLS handshake.
//
// `tls_hostname` is used for SNI validation and certificate hostname validation.
//
// `hostname_display` is used for the error messages.
pub fn resolve_and_tls_handshake(
    to_socket_addrs: &impl std::net::ToSocketAddrs,
    tls_hostname: &str,
    hostname_display: &str,
) -> Result<(), Error> {
    let host_addr = to_socket_addrs
        .to_socket_addrs()
        .with_context(|_| {
            format!(
                "Could not connect to {} : could not resolve hostname",
                hostname_display,
            )
        })?
        .next()
        .ok_or_else(|| {
            Context::new(format!(
                "Could not connect to {} : could not resolve hostname: no addresses found",
                hostname_display,
            ))
        })?;

    let stream = TcpStream::connect_timeout(&host_addr, std::time::Duration::from_secs(10))
        .with_context(|_| format!("Could not connect to {}", hostname_display))?;

    let tls_connector = native_tls::TlsConnector::new().with_context(|_| {
        format!(
            "Could not connect to {} : could not create TLS connector",
            hostname_display,
        )
    })?;
    let _ = tls_connector
        .connect(tls_hostname, stream)
        .with_context(|_| {
            format!(
                "Could not connect to {} : could not complete TLS handshake",
                hostname_display,
            )
        })?;

    Ok(())
}

pub fn resolve_and_tls_handshake_proxy(
    tls_hostname: String,
    port: Option<u16>,
    proxy: String,
) -> impl Future<Item = (), Error = Error> {
    futures::future::ok(())
        .and_then({
            move |_| -> Result<_, Error> {
                let proxy_uri = Uri::from_str(&proxy)
                    .with_context(|_| format!("Could not make proxi uri from {}", proxy))?;

                let credentials = proxy_uri
                    .authority_part()
                    .and_then(|authority| {
                        if let [userpass, _] =
                            &authority.as_str().split('@').collect::<Vec<&str>>()[..]
                        {
                            if let [username, password] =
                                &userpass.split(':').collect::<Vec<&str>>()[..]
                            {
                                return Some(Credentials::basic(username, password));
                            }
                        }
                        None
                    })
                    .transpose()
                    .with_context(|e| format!("Could not parse credentuals. Reason: {}", e))?;

                let mut proxy_obj = Proxy::new(Intercept::All, proxy_uri);
                if let Some(credentials) = credentials {
                    proxy_obj.set_authorization(credentials);
                }

                let http_connector = HttpsConnector::new(1)
                    .with_context(|_| "Could not make https connector".to_owned())?;
                let proxy_connector = ProxyConnector::from_proxy(http_connector, proxy_obj)
                    .with_context(|_| "Could not make proxy connector".to_owned())?;

                let port = port.map(|p| format!(":{}", p)).unwrap_or_default();
                let hostname = Uri::from_str(&format!("https://{}{}", tls_hostname, port))
                    .with_context(|_| {
                        format!("Could not make uri from {}{}", tls_hostname, port)
                    })?;

                let hostname: Result<Destination, _> = hostname.try_into();
                let hostname = hostname.with_context(|_| "Could not make hostname uri")?;
                Ok((proxy_connector, hostname))
            }
        })
        .and_then(move |(proxy_connector, hostname)| {
            proxy_connector
                .connect(hostname)
                .map_err(|e| format_err!("Could not connect: {}", e))
        })
        .map(drop)
}
