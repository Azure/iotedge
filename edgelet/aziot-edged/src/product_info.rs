use std::convert::AsRef;
use std::fmt;
use std::fs;
use std::io::{self, BufRead};
use std::path::Path;
use std::process::{Command, Output};

use serde::Deserialize;

#[derive(Debug, Deserialize, PartialEq)]
pub(crate) struct ProductInfo {
    product: Vec<Product>,
}

#[derive(Debug, Deserialize, PartialEq)]
struct Product {
    name: String,
    version: String,
    comment: Option<String>,
}

#[inline]
fn read_utf8(v: Vec<u8>) -> io::Result<String> {
    Ok(String::from_utf8(v)
        .map_err(|e| io::Error::new(io::ErrorKind::InvalidData, e))?
        .trim()
        .to_owned())
}

#[inline]
fn read_output(out: Output) -> io::Result<String> {
    match out.status.code() {
        Some(0) => read_utf8(out.stdout),
        Some(code) => Err(io::Error::from_raw_os_error(code)),
        _ => Err(io::Error::from(io::ErrorKind::Other)),
    }
}

#[inline]
fn strip_quotes(s: &str) -> io::Result<&str> {
    s.strip_prefix('\"')
        .and_then(|s| s.strip_suffix('\"'))
        .ok_or_else(|| io::Error::new(io::ErrorKind::InvalidData, "mismatched quotes"))
}

impl ProductInfo {
    pub fn try_load(p: impl AsRef<Path>) -> io::Result<Self> {
        let bytes = fs::read(p)?;

        toml::de::from_slice(&bytes).map_err(|e| io::Error::new(io::ErrorKind::InvalidData, e))
    }

    pub fn from_system() -> io::Result<Self> {
        let mut product = vec![];

        // NOTE: operating system
        product.push({
            let b = io::BufReader::new(
                fs::File::open("/etc/os-release")
                    .or_else(|_| fs::File::open("/usr/lib/os-release"))?,
            );

            let mut name = None;
            let mut version = None;
            let mut comment = None;

            for line in b.lines() {
                match line?.trim().split_once("=") {
                    Some(("ID", value)) => name = Some(strip_quotes(value)?.to_owned()),
                    Some(("VERSION_ID", value)) => version = Some(strip_quotes(value)?.to_owned()),
                    Some(("PRETTY_NAME", value)) => comment = Some(strip_quotes(value)?.to_owned()),
                    _ => (),
                }
            }

            Product {
                name: name
                    .ok_or_else(|| io::Error::new(io::ErrorKind::NotFound, "os-release: ID"))?,
                version: version.ok_or_else(|| {
                    io::Error::new(io::ErrorKind::NotFound, "os-release: VERSION_ID")
                })?,
                comment,
            }
        });

        // NOTE: kernel
        product.push(Product {
            name: Command::new("uname")
                .arg("-s")
                .output()
                .and_then(read_output)?,
            version: Command::new("uname")
                .arg("-r")
                .output()
                .and_then(read_output)?,
            comment: Some(
                Command::new("uname")
                    .arg("-v")
                    .output()
                    .and_then(read_output)?,
            ),
        });

        // NOTE: device
        product.push(Product {
            name: read_utf8(fs::read("/sys/devices/virtual/dmi/id/product_name")?)?,
            version: read_utf8(fs::read("/sys/devices/virtual/dmi/id/product_version")?)?,
            comment: Some(read_utf8(fs::read(
                "/sys/devices/virtual/dmi/id/sys_vendor",
            )?)?),
        });

        Ok(Self { product })
    }
}

impl fmt::Display for Product {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}/{}", self.name, self.version)?;

        if let Some(comment) = &self.comment {
            write!(f, " ({})", comment)?;
        };

        Ok(())
    }
}

impl fmt::Display for ProductInfo {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        if let Some(first) = self.product.first() {
            write!(f, "{}", first)?;

            // NOTE: will not panic
            for product in &self.product[1..] {
                write!(f, " {}", product)?;
            }
        }

        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    impl From<(&str, &str)> for Product {
        fn from(value: (&str, &str)) -> Self {
            Product {
                name: value.0.to_owned(),
                version: value.1.to_owned(),
                comment: None,
            }
        }
    }

    impl From<(&str, &str, &str)> for Product {
        fn from(value: (&str, &str, &str)) -> Self {
            Product {
                name: value.0.to_owned(),
                version: value.1.to_owned(),
                comment: Some(value.2.to_owned()),
            }
        }
    }

    #[test]
    fn product_string_no_comment() {
        let p: Product = ("FOO", "BAR").into();

        assert_eq!("FOO/BAR", p.to_string());
    }

    #[test]
    fn product_string_with_comment() {
        let p: Product = ("FOO", "BAR", "BAZ").into();

        assert_eq!("FOO/BAR (BAZ)", p.to_string());
    }

    #[test]
    fn multiple_products() {
        let pinfo = ProductInfo {
            product: vec![
                ("FOO", "BAR").into(),
                ("A", "B", "C").into(),
                ("name", "version", "comment").into(),
            ],
        };

        assert_eq!("FOO/BAR A/B (C) name/version (comment)", pinfo.to_string());
    }
}
