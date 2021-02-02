use std::{
    collections::hash_map::DefaultHasher,
    fmt::Debug,
    hash::{Hash, Hasher},
};

use serde::{Deserialize, Serialize};

use crate::persist::waking_state::ring_buffer::{
    error::{BlockError, RingBufferError},
    serialize::{binary_serialize, binary_serialize_size},
    StorageResult,
};

#[allow(dead_code)]
pub const BLOCK_HINT: usize = 0xdead_beef;

/// + ------------+------+---------+
/// | BlockHeader | hash | data... |
/// +-------------+------+---------+
///
/// The BlockHeader contains attributes meaningful for data storage and
/// validation.
#[allow(dead_code)]
#[derive(Copy, Clone, Debug, Deserialize, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockHeader {
    // hint comes first so we can skip it when checking for empty block
    // as the hint is always present.
    hint: usize,
    // variable fields
    data_hash: u64,
    data_size: usize,
    order: usize,
    read_index: usize,
    should_not_overwrite: bool,
    write_index: usize,
}

#[allow(dead_code)]
impl BlockHeader {
    pub fn new(
        data_hash: u64,
        data_size: usize,
        order: usize,
        read_index: usize,
        write_index: usize,
    ) -> Self {
        Self {
            data_hash,
            data_size,
            hint: BLOCK_HINT,
            order,
            read_index,
            should_not_overwrite: true,
            write_index,
        }
    }

    pub fn hint(&self) -> usize {
        self.hint
    }

    pub fn data_size(&self) -> usize {
        self.data_size
    }

    pub fn order(&self) -> usize {
        self.order
    }

    pub fn write_index(&self) -> usize {
        self.write_index
    }

    pub fn read_index(&self) -> usize {
        self.read_index
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

/// + --------------------+---------+
/// | BlockHeaderWithHash | data... |
/// +---------------------+---------+
///
/// The BlockHeaderWithHash is a wrapper over the BlockHeader but also
/// contains a hash over the header for validation.
#[allow(dead_code)]
#[derive(Copy, Clone, Debug, Deserialize, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockHeaderWithHash {
    inner: BlockHeader,
    block_hash: u64,
}

#[allow(dead_code)]
impl BlockHeaderWithHash {
    pub fn new(
        data_hash: u64,
        data_size: usize,
        order: usize,
        read_index: usize,
        write_index: usize,
    ) -> Self {
        let header = BlockHeader::new(data_hash, data_size, order, read_index, write_index);
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

pub fn serialized_block_size() -> bincode::Result<usize> {
    binary_serialize_size(&BlockHeaderWithHash::new(0, 0, 0, 0, 0))
}

/// A typed representation of byte data that should always follow a
/// BlockHeaderWithHash.
#[allow(dead_code)]
#[derive(Clone, Debug, Deserialize, Hash, Serialize)]
#[repr(C)]
pub(crate) struct Data {
    inner: Vec<u8>,
}

#[allow(dead_code)]
impl Data {
    pub fn new<'a>(data: &'a [u8]) -> Self {
        Self {
            inner: Vec::from(data),
        }
    }

    pub fn inner(&self) -> &[u8] {
        &self.inner
    }
}

/// A utility fn to calculate any entity that derives Hash.
#[allow(dead_code)]
pub(crate) fn calculate_hash<T>(t: &T) -> u64
where
    T: Hash + ?Sized,
{
    let mut hasher = DefaultHasher::new();
    t.hash(&mut hasher);
    hasher.finish()
}

#[allow(dead_code)]
pub(crate) fn validate<'a>(block: &BlockHeaderWithHash, data: &Data) -> StorageResult<()> {
    let data_hash = calculate_hash(&data.inner);
    let data_size = binary_serialize(data)?.len();
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
    if actual_data_size != data_size {
        return Err(RingBufferError::Validate(BlockError::DataSize {
            found: actual_data_size,
            expected: data_size,
        })
        .into());
    }
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
    };

    use matches::assert_matches;

    use crate::persist::{waking_state::ring_buffer::serialize::binary_deserialize, StorageError};

    use super::*;

    fn random_data(data_length: usize) -> Vec<u8> {
        (0..data_length).map(|_| rand::random::<u8>()).collect()
    }

    fn test_serialize_helper(inner_data: &[u8]) {
        let mut hasher = DefaultHasher::new();
        inner_data.hash(&mut hasher);
        let data_hash = hasher.finish();

        let block_header_with_hash = BlockHeaderWithHash::new(data_hash, inner_data.len(), 0, 0, 0);
        let data = Data::new(inner_data);

        let result = binary_serialize(&block_header_with_hash);
        assert!(result.is_ok());
        let serialized_block = result.unwrap();

        let result = binary_deserialize::<BlockHeaderWithHash>(&serialized_block);
        assert!(result.is_ok());
        let deserialized_block = result.unwrap();

        let result = binary_serialize(&data);
        assert!(result.is_ok());
        let serialized_data = result.unwrap();

        let result = binary_deserialize::<Data>(&serialized_data);
        assert!(result.is_ok());
        let deserialized_data = result.unwrap();

        let block_hash = calculate_hash(&block_header_with_hash.inner);
        assert_eq!(block_hash, deserialized_block.block_hash());

        let deserialized_header = deserialized_block.inner;
        assert_eq!(0, deserialized_header.read_index());
        assert_eq!(0, deserialized_header.write_index());
        assert_eq!(data_hash, deserialized_header.data_hash());
        assert_eq!(inner_data.len(), deserialized_header.data_size());

        assert_eq!(inner_data, deserialized_data.inner);
    }

    #[test]
    fn it_deserializes_serialized_empty_content() {
        test_serialize_helper(b"");
    }

    #[test]
    fn it_deserializes_serialized_content() {
        test_serialize_helper(b"Hello World");
    }

    #[test]
    fn it_deserializes_serialized_random_content() {
        test_serialize_helper(&random_data(100));
    }

    #[test]
    fn validate_errs_when_data_hash_do_not_match() {
        let block = BlockHeaderWithHash::new(0x0000_0bad, 19, 0, 0, 0);
        let data = Data::new(b"Hello World");
        let result = validate(&block, &data);
        assert!(result.is_err());
        let _expected = calculate_hash(&data);
        assert_matches!(
            result.unwrap_err(),
            StorageError::RingBuffer(RingBufferError::Validate(BlockError::DataHash {
                found: 0x0000_0bad,
                expected: _expected,
            }))
        );
    }

    #[test]
    fn validate_errs_when_data_size_do_not_match() {
        let data = Data::new(b"Hello World");
        let data_hash = calculate_hash(&data);
        let block = BlockHeaderWithHash::new(data_hash, 0, 0, 0, 0);
        let result = validate(&block, &data);
        assert!(result.is_err());
        let expected_result = binary_serialize_size(&data);
        assert!(expected_result.is_ok());
        let _expected = expected_result.unwrap();
        assert_matches!(
            result.unwrap_err(),
            StorageError::RingBuffer(RingBufferError::Validate(BlockError::DataSize {
                found: 0,
                expected: _expected,
            }))
        );
    }

    #[test]
    fn validate_errs_when_block_hash_do_not_match() {
        let data = Data::new(b"Hello World");
        let data_hash = calculate_hash(&data);
        let mut block = BlockHeaderWithHash::new(data_hash, 19, 1, 0, 0);
        block.block_hash = 0x1;
        let result = validate(&block, &data);
        assert!(result.is_err());
        assert_matches!(
            result.unwrap_err(),
            StorageError::RingBuffer(RingBufferError::Validate(BlockError::BlockHash {
                found: 0x1,
                expected: 0x7e02_796d_a121_4d26,
            }))
        );
    }
}
