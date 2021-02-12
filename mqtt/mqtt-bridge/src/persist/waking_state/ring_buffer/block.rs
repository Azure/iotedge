use std::{
    fmt::Debug,
    hash::{Hash, Hasher},
};

use lazy_static::lazy_static;
use serde::{Deserialize, Serialize};

use crate::persist::waking_state::ring_buffer::{
    error::{BlockError, RingBufferError},
    StorageResult,
};

lazy_static! {
    pub(crate) static ref SERIALIZED_BLOCK_SIZE: u64 =
        bincode::serialized_size(&BlockHeaderWithHash::new(0, 0, 0, 0)).unwrap();
}

/// A constant set bytes to help determine if a set of data comprises a block.
pub const BLOCK_HINT: u32 = 0xdead_beef;

/// + --------------+------+---------+
/// | `BlockHeader` | hash | data... |
/// +---------------+------+---------+
///
/// The `BlockHeader` contains attributes meaningful for data storage and
/// validation.
#[derive(Copy, Clone, Debug, Deserialize, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockHeader {
    // The hint comes first so we can skip it when checking for empty block
    // as the hint is always present.
    hint: u32,
    // variable fields
    // A hash over the entire data that follows the header, this provides
    // integrity check.
    data_hash: u64,
    // The size of the data after the header.
    data_size: u32,
    // The ordering of blocks, i.e. 1, 2, 3...
    order: u128,
    // A flag for determining if a block and data pair can be written over.
    // Default state is the negative so to allow empty (all 0) data to
    // deserialize in a way that makes sense (false).
    should_not_overwrite: bool,
    // The index of the write pointer when the block is created.
    write_index: u32,
}

impl BlockHeader {
    pub fn new(data_hash: u64, data_size: u32, order: u128, write_index: u32) -> Self {
        Self {
            data_hash,
            data_size,
            hint: BLOCK_HINT,
            order,
            should_not_overwrite: true,
            write_index,
        }
    }

    pub fn hint(&self) -> u32 {
        self.hint
    }

    pub fn data_size(&self) -> u32 {
        self.data_size
    }

    pub fn order(&self) -> u128 {
        self.order
    }

    pub fn write_index(&self) -> u32 {
        self.write_index
    }

    pub fn data_hash(&self) -> u64 {
        self.data_hash
    }

    pub fn should_not_overwrite(&self) -> bool {
        self.should_not_overwrite
    }

    pub fn set_should_not_overwrite(&mut self, value: bool) {
        self.should_not_overwrite = value
    }
}

/// + ----------------------+---------+
/// | `BlockHeaderWithHash` | data... |
/// +-----------------------+---------+
///
/// The `BlockHeaderWithHash` is a wrapper over the `BlockHeader` but also
/// contains a hash over the header for validation.
#[derive(Copy, Clone, Debug, Deserialize, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockHeaderWithHash {
    inner: BlockHeader,
    // A hash over the entire block header to guarantee integrity.
    block_hash: u64,
}

impl BlockHeaderWithHash {
    pub fn new(data_hash: u64, data_size: u32, order: u128, write_index: u32) -> Self {
        let header = BlockHeader::new(data_hash, data_size, order, write_index);
        let header_hash = calculate_hash(&header);
        Self {
            inner: header,
            block_hash: header_hash,
        }
    }

    pub fn block_hash(&self) -> u64 {
        self.block_hash
    }

    pub fn inner(&self) -> &BlockHeader {
        &self.inner
    }

    pub fn inner_mut(&mut self) -> &mut BlockHeader {
        &mut self.inner
    }
}

/// A utility fn to calculate any entity that derives Hash.
pub(crate) fn calculate_hash<T>(t: &T) -> u64
where
    T: Hash + ?Sized,
{
    let mut hasher = rustc_hash::FxHasher::default();
    t.hash(&mut hasher);
    hasher.finish()
}

/// A utility fn that validates the integrity of both the block and data.
pub(crate) fn validate(block: &BlockHeaderWithHash, data: &[u8]) -> StorageResult<()> {
    let actual_block_hash = block.block_hash();
    let block_hash = calculate_hash(&block.inner);
    if actual_block_hash != block_hash {
        return Err(RingBufferError::Validate(BlockError::BlockHash {
            found: actual_block_hash,
            expected: block_hash,
        })
        .into());
    }

    let inner_block = block.inner;
    let actual_data_size = inner_block.data_size();

    #[allow(clippy::cast_possible_truncation)]
    let data_size = data.len() as u32;
    if actual_data_size != data_size {
        return Err(RingBufferError::Validate(BlockError::DataSize {
            found: actual_data_size,
            expected: data_size,
        })
        .into());
    }

    let data_hash = calculate_hash(data);
    let actual_data_hash = inner_block.data_hash();
    if actual_data_hash != data_hash {
        return Err(RingBufferError::Validate(BlockError::DataHash {
            found: actual_data_hash,
            expected: data_hash,
        })
        .into());
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use std::{
        collections::hash_map::DefaultHasher,
        hash::{Hash, Hasher},
        iter,
    };

    use matches::assert_matches;
    use rand::{distributions::Alphanumeric, thread_rng, Rng};

    use crate::persist::StorageError;

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
        let mut hasher = DefaultHasher::new();
        data.hash(&mut hasher);
        let data_hash = hasher.finish();

        let result = bincode::serialized_size(&data);
        assert_matches!(result, Ok(_));
        
        #[allow(clippy::cast_possible_truncation)]
        let data_size = result.unwrap() as u32;

        let block_header_with_hash = BlockHeaderWithHash::new(data_hash, data_size, 0, 0);

        let result = bincode::serialize(&block_header_with_hash);
        assert_matches!(result, Ok(_));
        let serialized_block = result.unwrap();

        let result = bincode::deserialize::<BlockHeaderWithHash>(&serialized_block);
        assert_matches!(result, Ok(_));
        let deserialized_block = result.unwrap();

        let result = bincode::serialize(&data);
        assert_matches!(result, Ok(_));
        let serialized_data = result.unwrap();

        let result = bincode::deserialize::<String>(&serialized_data);
        assert_matches!(result, Ok(_));
        let deserialized_data = result.unwrap();

        let block_hash = calculate_hash(&block_header_with_hash.inner);
        assert_eq!(block_hash, deserialized_block.block_hash());

        let deserialized_header = deserialized_block.inner;
        assert_eq!(0, deserialized_header.write_index());
        assert_eq!(data_hash, deserialized_header.data_hash());
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
    fn validate_errs_when_data_hash_do_not_match() {
        let block = BlockHeaderWithHash::new(0x0000_0bad, 11, 0, 0);
        let data = b"Hello World";
        let result = validate(&block, data);
        let _expected = calculate_hash(&data);
        assert_matches!(
            result,
            Err(StorageError::RingBuffer(RingBufferError::Validate(
                BlockError::DataHash {
                    found: 0x0000_0bad,
                    expected: _expected,
                }
            )))
        );
    }

    #[test]
    fn validate_errs_when_data_size_do_not_match() {
        let data = b"Hello World";
        let data_hash = calculate_hash(&data);
        let block = BlockHeaderWithHash::new(data_hash, 0, 0, 0);
        let result = validate(&block, data);
        let expected_result = bincode::serialize(&data);
        assert_matches!(expected_result, Ok(_));
        let _expected = expected_result.unwrap();
        assert_matches!(
            result,
            Err(StorageError::RingBuffer(RingBufferError::Validate(
                BlockError::DataSize {
                    found: 0,
                    expected: _expected,
                }
            )))
        );
    }

    #[test]
    fn validate_errs_when_block_hash_do_not_match() {
        let data = b"Hello World";
        let data_hash = calculate_hash(&data);
        let mut block = BlockHeaderWithHash::new(data_hash, 19, 1, 0);
        block.block_hash = 0x1;
        let result = validate(&block, data);
        assert_matches!(
            result,
            Err(StorageError::RingBuffer(RingBufferError::Validate(_)))
        );
    }
}
