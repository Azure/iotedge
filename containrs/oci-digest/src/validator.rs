use sha2::digest::DynDigest;

use crate::algorithms::Algorithm;
use crate::digest::Digest;

const EXPECT_VALID: &str = "digest struct should never be backed by a malformed string";

pub struct Validator {
    expect_digest: Vec<u8>,
    digest: Box<dyn DynDigest>,
}

impl Validator {
    pub(crate) fn new(digest: &Digest) -> Validator {
        let mut parts = digest.as_str().split(':');

        let algorithm = parts
            .next()
            .expect(EXPECT_VALID)
            .parse::<Algorithm>()
            .expect(EXPECT_VALID);
        let expect_digest = parts.next().expect(EXPECT_VALID);

        Validator {
            expect_digest: hex::decode(expect_digest).expect(EXPECT_VALID),
            digest: algorithm.new_boxed_digest(),
        }
    }

    /// Digest input data.
    ///
    /// This method can be called repeatedly for use with streaming messages.
    pub fn input(&mut self, data: &[u8]) {
        self.digest.input(data)
    }

    /// Consumes the validator, returning true if input data's digest matches
    /// the expected digest.
    pub fn validate(self) -> bool {
        self.digest.result()[..] == self.expect_digest[..]
    }
}
