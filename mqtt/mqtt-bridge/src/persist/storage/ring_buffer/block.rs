use getset::{Getters, MutGetters, Setters};
use serde::{Deserialize, Serialize};
use std::{
    collections::hash_map::DefaultHasher,
    fmt::Debug,
    hash::{Hash, Hasher},
};

use crate::persist::storage::serialize::binary_serialize;

use super::{error::BlockError, RingBufferResult};

/// A utility fn to calculate any entity that derives Hash.
pub(crate) fn calculate_hash<T>(t: &T) -> u64
where
    T: Hash + ?Sized,
{
    let mut hasher = DefaultHasher::new();
    t.hash(&mut hasher);
    hasher.finish()
}

/// + ------------+------+---------+
/// | BlockHeader | hash | data... |
/// +-------------+------+---------+
///
/// The BlockHeader contains attributes meaningful for data
/// storage and validation.
#[derive(Copy, Clone, Debug, Deserialize, Getters, Setters, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockHeader {
    #[getset(get = "pub")]
    data_size: usize,
    #[getset(get = "pub")]
    index: usize,
    #[getset(get = "pub")]
    data_hash: u64,
    #[getset(get = "pub", set = "pub")]
    should_not_overwrite: bool,
}

impl BlockHeader {
    pub fn new(data_size: usize, index: usize, data_hash: u64) -> Self {
        Self {
            data_size,
            index,
            data_hash,
            should_not_overwrite: true,
        }
    }
}

/// + --------------------+---------+
/// | BlockHeaderWithHash | data... |
/// +---------------------+---------+
///
/// The BlockHeaderWithHash is a wrapper over the BlockHeader
/// but also contains a hash over the header for validation.
#[derive(Copy, Clone, Debug, Deserialize, Getters, MutGetters, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockHeaderWithHash {
    #[getset(get = "pub", get_mut = "pub")]
    inner: BlockHeader,
    #[getset(get = "pub")]
    block_hash: u64,
}

impl BlockHeaderWithHash {
    pub fn new(data_size: usize, index: usize, data_hash: u64) -> Self {
        let header = BlockHeader::new(data_size, index, data_hash);
        let header_hash = calculate_hash(&header);
        Self {
            inner: header,
            block_hash: header_hash,
        }
    }
}

/// A typed representation of byte data that should always
/// follow a BlockHeaderWithHash.
#[derive(Clone, Debug, Deserialize, Getters, Hash, Serialize)]
#[repr(C)]
pub(crate) struct Data {
    #[getset(get = "pub")]
    inner: Vec<u8>,
}

impl Data {
    pub fn new(data: Vec<u8>) -> Self {
        Self { inner: data }
    }
}

pub(crate) fn validate(block: &BlockHeaderWithHash, data: &Data) -> RingBufferResult<()> {
    let data_hash = calculate_hash(&data.inner);
    let data_size = binary_serialize(data)?.len();
    let actual_block_hash = *block.block_hash();
    let block_hash = calculate_hash(&block.inner);
    if actual_block_hash != block_hash {
        return Err(BlockError::BlockHash {
            found: actual_block_hash,
            expected: block_hash,
        }
        .into());
    }
    let inner_block = block.inner;
    let actual_data_size = *inner_block.data_size();
    if actual_data_size != data_size {
        return Err(BlockError::DataSize {
            found: actual_data_size,
            expected: data_size,
        }
        .into());
    }
    let actual_data_hash = *inner_block.data_hash();
    if actual_data_hash != data_hash {
        return Err(BlockError::DataHash {
            found: actual_data_hash,
            expected: data_hash,
        }
        .into());
    }
    Ok(())
}

#[cfg(test)]
mod tests {

    use std::collections::hash_map::DefaultHasher;

    use super::*;

    fn random_data(data_length: usize) -> Vec<u8> {
        (0..data_length).map(|_| rand::random::<u8>()).collect()
    }

    mod serialize {
        use std::hash::Hash;
        use std::hash::Hasher;

        use crate::persist::storage::serialize::{binary_deserialize, binary_serialize};

        use super::*;

        #[derive(Clone, Debug, Deserialize, Getters, Hash, Serialize)]
        #[repr(C)]
        struct TestBlock {
            block_header_with_hash: BlockHeaderWithHash,
            data: Data,
        }

        fn test_serialize_helper(inner_data: &[u8]) {
            let mut hasher = DefaultHasher::new();
            inner_data.hash(&mut hasher);
            let data_hash = hasher.finish();

            let block_header_with_hash = BlockHeaderWithHash::new(inner_data.len(), 0, data_hash);
            let data = Data::new(Vec::from(inner_data));

            let test_block = TestBlock {
                block_header_with_hash,
                data,
            };

            let result = binary_serialize(&test_block);
            assert!(result.is_ok());
            let serialized_block = result.unwrap();

            let result = binary_deserialize::<TestBlock>(&serialized_block);
            assert!(result.is_ok());
            let deserialized_block = result.unwrap();

            let deserialized_header_with_hash = deserialized_block.block_header_with_hash;
            let block_hash = calculate_hash(&block_header_with_hash.inner);
            assert_eq!(block_hash, *deserialized_header_with_hash.block_hash());

            let deserialized_header = deserialized_header_with_hash.inner;
            assert_eq!(0, *deserialized_header.index());
            assert_eq!(data_hash, *deserialized_header.data_hash());
            assert_eq!(inner_data.len(), *deserialized_header.data_size());

            let deserialized_data = deserialized_block.data;
            assert_eq!(inner_data, deserialized_data.inner);
        }

        #[test]
        fn test_serialize_deserialize_without_data() {
            test_serialize_helper(b"");
        }

        #[test]
        fn test_serialize_deserialize_with_data() {
            test_serialize_helper(b"Hello World");
        }

        #[test]
        fn test_serialize_deserialize_with_random_data() {
            test_serialize_helper(&random_data(100));
        }
    }
}
