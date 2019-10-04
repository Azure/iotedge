use std::fmt;
use std::str::FromStr;

use sha2::digest::DynDigest;

#[derive(Debug, Copy, Clone)]
pub enum Algorithm {
    Sha256,
    Sha384,
    Sha512,
}

impl Algorithm {
    /// Return digest length (in bytes)
    pub fn digest_len(self) -> usize {
        use self::Algorithm::*;
        match self {
            Sha256 => 256 / 8,
            Sha384 => 384 / 8,
            Sha512 => 512 / 8,
        }
    }

    /// Return a new instance of a boxed digestor
    pub fn new_boxed_digest(self) -> Box<dyn DynDigest> {
        use self::Algorithm::*;
        match self {
            Sha256 => Box::new(sha2::Sha256::default()),
            Sha384 => Box::new(sha2::Sha384::default()),
            Sha512 => Box::new(sha2::Sha512::default()),
        }
    }
}

impl fmt::Display for Algorithm {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        use self::Algorithm::*;
        let s = match self {
            Sha256 => "sha256",
            Sha384 => "sha384",
            Sha512 => "sha512",
        };
        f.write_str(s)
    }
}

impl FromStr for Algorithm {
    type Err = ();
    fn from_str(s: &str) -> Result<Algorithm, ()> {
        use self::Algorithm::*;
        let algorithm = match s {
            "sha256" => Sha256,
            "sha384" => Sha384,
            "sha512" => Sha512,
            _ => return Err(()),
        };
        Ok(algorithm)
    }
}
