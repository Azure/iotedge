use bincode::Result as BincodeResult;
use lazy_static::lazy_static;
use serde::{Deserialize, Serialize};

use crate::persist::waking_state::{
    ring_buffer::error::{BlockError, RingBufferError},
    PersistResult,
};

lazy_static! {
    pub(crate) static ref SERIALIZED_BLOCK_SIZE: u64 = {
        let v1 = BlockHeaderV1::new(0, 0, 0, 0, 0);
        let versioned_block = BlockVersion::Version1(v1);

        bincode::serialized_size(
            &BlockHeaderWithCrc::new(versioned_block).expect("unable to create sample data block"),
        )
        .expect("unable to serialize sample data block")
    };
}

/// A constant set bytes to help determine if a set of data comprises a block.
pub const BLOCK_HINT: u32 = 0xdead_beef;

/// + --------------+------+---------+
/// | `BlockHeader` | crc  | data... |
/// +---------------+------+---------+
///
/// The `BlockHeader` contains attributes meaningful for data storage and
/// validation.
#[derive(Copy, Clone, Debug, Deserialize, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockHeaderV1 {
    // The hint comes first so we can skip it when checking for empty block
    // as the hint is always present.
    hint: u32,

    // variable fields
    // A crc over the entire data that follows the header, this provides
    // integrity check.
    data_crc: u32,

    // The size of the data after the header.
    data_size: u64,

    // The ordering of blocks, i.e. 1, 2, 3...
    order: u64,

    // A flag for determining if a block and data pair can be written over.
    // Default state is the negative so to allow empty (all 0) data to
    // deserialize in a way that makes sense (false).
    should_not_overwrite: bool,

    // The index of the write pointer when the block is created.
    write_index: u64,
}

impl BlockHeaderV1 {
    pub fn new(hint: u32, data_crc: u32, data_size: u64, order: u64, write_index: u64) -> Self {
        Self {
            data_crc,
            data_size,
            hint,
            order,
            should_not_overwrite: true,
            write_index,
        }
    }

    pub fn hint(&self) -> u32 {
        self.hint
    }

    pub fn data_size(&self) -> u64 {
        self.data_size
    }

    pub fn order(&self) -> u64 {
        self.order
    }

    pub fn write_index(&self) -> u64 {
        self.write_index
    }

    pub fn data_crc(&self) -> u32 {
        self.data_crc
    }

    pub fn should_not_overwrite(&self) -> bool {
        self.should_not_overwrite
    }

    pub fn set_should_not_overwrite(&mut self, value: bool) {
        self.should_not_overwrite = value
    }
}

/// `BlockVersion` is an enum containing ways in which we might
/// want to interpret data within a block
#[derive(Copy, Clone, Debug, Deserialize, Hash, Serialize)]
pub(crate) enum BlockVersion {
    Version1(BlockHeaderV1),
}

/// + ----------------------+---------+
/// | `BlockHeaderWithCrc`  | data... |
/// +-----------------------+---------+
///
/// The `BlockHeaderWithCrc` is a wrapper over the `BlockHeader` but also
/// contains a crc over the header for validation.
#[derive(Copy, Clone, Debug, Deserialize, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockHeaderWithCrc {
    inner: BlockVersion,
    // A crc over the entire block header to guarantee integrity.
    block_crc: u32,
}

impl BlockHeaderWithCrc {
    pub fn new(versioned_block: BlockVersion) -> Result<Self, BlockError> {
        let header = versioned_block;
        let header_crc = calculate_crc(&header).map_err(BlockError::BlockCreation)?;
        Ok(Self {
            inner: header,
            block_crc: header_crc,
        })
    }

    pub fn block_crc(&self) -> u32 {
        self.block_crc
    }

    pub fn inner(&self) -> &BlockVersion {
        &self.inner
    }

    pub fn inner_mut(&mut self) -> &mut BlockVersion {
        &mut self.inner
    }
}

/// A utility fn to calculate any crc over serialized objects.
pub(crate) fn calculate_crc<T>(t: &T) -> BincodeResult<u32>
where
    T: Serialize + ?Sized,
{
    let data = bincode::serialize(t)?;
    Ok(calculate_crc_over_bytes(&data))
}

pub(crate) fn calculate_crc_over_bytes(bytes: &[u8]) -> u32 {
    let mut hasher = crc32fast::Hasher::default();
    hasher.update(&bytes);
    hasher.finalize()
}

/// A utility fn that validates the integrity of both the block and data.
pub(crate) fn validate(block: &BlockHeaderWithCrc, data: &[u8]) -> PersistResult<()> {
    let actual_block_crc = block.block_crc();
    let block_crc = calculate_crc(&block.inner)?;
    if actual_block_crc != block_crc {
        return Err(RingBufferError::Validate(BlockError::BlockCrc {
            found: actual_block_crc,
            expected: block_crc,
        })
        .into());
    }

    let BlockVersion::Version1(inner_block) = block.inner;
    let actual_data_size = inner_block.data_size();

    let data_size = data.len() as u64;
    if actual_data_size != data_size {
        return Err(RingBufferError::Validate(BlockError::DataSize {
            found: actual_data_size,
            expected: data_size,
        })
        .into());
    }

    let data_crc = calculate_crc_over_bytes(data);
    let actual_data_crc = inner_block.data_crc();
    if actual_data_crc != data_crc {
        return Err(RingBufferError::Validate(BlockError::DataCrc {
            found: actual_data_crc,
            expected: data_crc,
        })
        .into());
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use std::iter;

    use matches::assert_matches;
    use rand::{distributions::Alphanumeric, thread_rng, Rng};

    use crate::persist::PersistError;

    use super::*;

    fn random_string(data_length: usize) -> String {
        let mut rng = thread_rng();
        iter::repeat(())
            .map(|()| rng.sample(Alphanumeric))
            .map(char::from)
            .take(data_length)
            .collect()
    }

    fn test_serialize_helper(data: &str) {
        let result = calculate_crc(data);
        assert_matches!(result, Ok(_));
        let data_crc = result.unwrap();

        let result = bincode::serialized_size(&data);
        assert_matches!(result, Ok(_));

        let data_size = result.unwrap();

        let v1 = BlockHeaderV1::new(BLOCK_HINT, data_crc, data_size, 0, 0);
        let versioned_block = BlockVersion::Version1(v1);

        let result = BlockHeaderWithCrc::new(versioned_block);
        assert_matches!(result, Ok(_));
        let block_header_with_crc = result.unwrap();

        let result = bincode::serialize(&block_header_with_crc);
        assert_matches!(result, Ok(_));
        let serialized_block = result.unwrap();

        let result = bincode::deserialize::<BlockHeaderWithCrc>(&serialized_block);
        assert_matches!(result, Ok(_));
        let deserialized_block = result.unwrap();

        let result = bincode::serialize(&data);
        assert_matches!(result, Ok(_));
        let serialized_data = result.unwrap();

        let result = bincode::deserialize::<String>(&serialized_data);
        assert_matches!(result, Ok(_));
        let deserialized_data = result.unwrap();

        let result = calculate_crc(&block_header_with_crc.inner);
        assert_matches!(result, Ok(_));
        let block_crc = result.unwrap();
        assert_eq!(block_crc, deserialized_block.block_crc());

        let BlockVersion::Version1(deserialized_header) = deserialized_block.inner;
        assert_eq!(0, deserialized_header.write_index());
        assert_eq!(data_crc, deserialized_header.data_crc());
        assert_eq!(data_size, deserialized_header.data_size());

        assert_eq!(data, deserialized_data);
    }

    #[test]
    fn it_deserializes_serialized_empty_content() {
        test_serialize_helper("");
    }

    #[test]
    fn it_deserializes_serialized_content() {
        test_serialize_helper("Hello World");
    }

    #[test]
    fn it_deserializes_serialized_random_content() {
        let random_str = random_string(100);
        test_serialize_helper(&random_str);
    }

    #[test]
    fn validate_errs_when_data_crc_do_not_match() {
        let v1 = BlockHeaderV1::new(BLOCK_HINT, 0x0000_0bad, 11, 0, 0);
        let versioned_block = BlockVersion::Version1(v1);
        let result = BlockHeaderWithCrc::new(versioned_block);
        assert_matches!(result, Ok(_));
        let block = result.unwrap();
        let data = b"Hello World";
        let result = validate(&block, data);
        let _expected = calculate_crc(&data);
        assert_matches!(
            result,
            Err(PersistError::RingBuffer(RingBufferError::Validate(
                BlockError::DataCrc {
                    found: 0x0000_0bad,
                    expected: _expected,
                }
            )))
        );
    }

    #[test]
    fn validate_errs_when_data_size_do_not_match() {
        let data = b"Hello World";
        let result = calculate_crc(&data);
        assert_matches!(result, Ok(_));
        let data_crc = result.unwrap();
        let v1 = BlockHeaderV1::new(BLOCK_HINT, data_crc, 0, 0, 0);
        let versioned_block = BlockVersion::Version1(v1);
        let result = BlockHeaderWithCrc::new(versioned_block);
        assert_matches!(result, Ok(_));
        let block = result.unwrap();
        let result = validate(&block, data);
        let expected_result = bincode::serialize(&data);
        assert_matches!(expected_result, Ok(_));
        let _expected = expected_result.unwrap();
        assert_matches!(
            result,
            Err(PersistError::RingBuffer(RingBufferError::Validate(
                BlockError::DataSize {
                    found: 0,
                    expected: _expected,
                }
            )))
        );
    }

    #[test]
    fn validate_errs_when_block_crc_do_not_match() {
        let data = b"Hello World";
        let result = calculate_crc(&data);
        assert_matches!(result, Ok(_));
        let data_crc = result.unwrap();
        let v1 = BlockHeaderV1::new(BLOCK_HINT, data_crc, 19, 1, 0);
        let versioned_block = BlockVersion::Version1(v1);
        let result = BlockHeaderWithCrc::new(versioned_block);
        assert_matches!(result, Ok(_));
        let mut block = result.unwrap();
        block.block_crc = 0x1;
        let result = validate(&block, data);
        assert_matches!(
            result,
            Err(PersistError::RingBuffer(RingBufferError::Validate(_)))
        );
    }
}
