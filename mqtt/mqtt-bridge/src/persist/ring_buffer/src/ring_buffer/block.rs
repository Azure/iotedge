use getset::Getters;
use std::mem::size_of;
use std::{
    collections::hash_map::DefaultHasher,
    hash::{Hash, Hasher},
};

use serde::{Deserialize, Serialize};

use crate::ring_buffer::{RingBufferError, RingBufferResult};

#[derive(Clone, Debug, Deserialize, Getters, Hash, Serialize)]
#[repr(C)]
pub(crate) struct BlockAttributes {
    #[getset(get = "pub")]
    block_size: usize,
    #[getset(get = "pub")]
    data_size: usize,
    #[getset(get = "pub")]
    index: usize,
    #[getset(get = "pub")]
    data_hash_offset: usize,
}

impl BlockAttributes {
    pub fn new(block_size: usize, data_size: usize, index: usize) -> Self {
        Self {
            block_size,
            data_size,
            index,
            // u64 is size of the hash in the HashedBlock
            data_hash_offset: block_size - size_of::<u64>(),
        }
    }
}

#[derive(Clone, Debug, Deserialize, Getters, Hash, Serialize)]
#[repr(C)]
pub(crate) struct FixedBlock {
    #[getset(get = "pub")]
    attributes: BlockAttributes,
    #[getset(get = "pub")]
    attributes_hash: u64,
    #[getset(get = "pub")]
    data: Vec<u8>,
    #[getset(get = "pub")]
    padding: Vec<u8>,
}

pub(crate) fn serialized_block_size_minus_data() -> usize {
    let attr = BlockAttributes {
        block_size: 0,
        data_size: 0,
        index: 0,
        data_hash_offset: 0,
    };
    let fixed_block = FixedBlock {
        attributes: attr,
        attributes_hash: 0,
        data: Vec::default(),
        padding: Vec::default(),
    };
    let hash_block = HashedBlock {
        fixed_block,
        hash: 0,
    };
    bincode::serialized_size(&hash_block).unwrap() as usize
}

pub(crate) fn calculate_hash<T>(t: &T) -> u64
where
    T: Hash + ?Sized,
{
    let mut hasher = DefaultHasher::new();
    t.hash(&mut hasher);
    hasher.finish()
}

impl FixedBlock {
    pub fn new(block_size: usize, data: Vec<u8>, index: usize) -> Self {
        // When Vec<8> is serialized it is 8 bytes and not 16, so add 8 to correct offset
        let data_size = data.len();
        let attributes = BlockAttributes::new(block_size, data_size, index);
        let attributes_hash = calculate_hash(&attributes);
        let minus_data_size = serialized_block_size_minus_data();
        if block_size - minus_data_size < data_size {
            panic!(
                "Invalid block_size or data_size: bs {:?} ds {:?} mds {:?}",
                block_size, data_size, minus_data_size
            );
        }
        Self {
            attributes,
            attributes_hash,
            data,
            padding: vec![0; block_size - minus_data_size - data_size],
        }
    }
}

#[derive(Clone, Debug, Deserialize, Getters, Serialize)]
#[repr(C)]
pub(crate) struct HashedBlock {
    #[getset(get = "pub")]
    fixed_block: FixedBlock,
    #[getset(get = "pub")]
    hash: u64,
}

impl HashedBlock {
    pub fn new(fixed_block: FixedBlock, hash: u64) -> Self {
        Self { fixed_block, hash }
    }
}

pub(crate) fn binary_serialize(hashed_block: &HashedBlock) -> RingBufferResult<Vec<u8>> {
    Ok(bincode::serialize(hashed_block).map_err(|err| {
        RingBufferError::from_err(format!("Failed to serialize {:?}", hashed_block), err)
    })?)
}

pub(crate) fn binary_deserialize(bytes: &[u8]) -> RingBufferResult<HashedBlock> {
    Ok(bincode::deserialize::<HashedBlock>(bytes).map_err(|err| {
        RingBufferError::from_err(format!("Failed to deserialize {:?}", bytes), err)
    })?)
}

#[cfg(test)]
mod tests {

    use std::collections::hash_map::DefaultHasher;

    use super::*;

    const BLOCK_SIZE: usize = 256;

    fn random_data(data_length: usize) -> Vec<u8> {
        (0..data_length).map(|_| rand::random::<u8>()).collect()
    }

    mod serialize {
        use std::hash::Hash;
        use std::hash::Hasher;

        use super::*;

        fn test_serialize_helper(inner_data: &[u8]) {
            let mut hasher = DefaultHasher::new();
            inner_data.hash(&mut hasher);
            let hash = hasher.finish();
            let data = Vec::from(inner_data);
            let fixed_block = FixedBlock::new(BLOCK_SIZE, data.clone(), 0);
            let hash_block = HashedBlock::new(fixed_block, hash);
            let result = binary_serialize(&hash_block);
            assert!(result.is_ok());
            let serialized_hash_block = result.unwrap();
            assert_eq!(serialized_hash_block.len(), BLOCK_SIZE);
            let result = binary_deserialize(&serialized_hash_block);
            assert!(result.is_ok());
            let hash_block_deserialized = result.unwrap();
            assert_eq!(hash, *hash_block_deserialized.hash());
            let fixed_block_deserialized = hash_block_deserialized.fixed_block();
            assert_eq!(data.len(), fixed_block_deserialized.data().len());
            assert_eq!(data, *fixed_block_deserialized.data());
            assert_eq!(
                BLOCK_SIZE - serialized_block_size_minus_data() - data.len(),
                fixed_block_deserialized.padding().len()
            );
            let attributes_deserialized = fixed_block_deserialized.attributes();
            assert_eq!(BLOCK_SIZE, *attributes_deserialized.block_size());
            assert_eq!(data.len(), *attributes_deserialized.data_size());
            assert_eq!(0, *attributes_deserialized.index());
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

        #[test]
        fn test_deserialize() {
            let data = vec![
                128, 0, 0, 0, 0, 0, 0, 0, 16, 0, 0, 0, 0, 0, 0, 0, 116, 7, 0, 0, 0, 0, 0, 0, 120,
                0, 0, 0, 0, 0, 0, 0, 49, 98, 4, 71, 80, 76, 218, 27, 16, 0, 0, 0, 0, 0, 0, 0, 99,
                200, 252, 200, 64, 246, 168, 19, 73, 119, 163, 211, 223, 153, 209, 220, 48, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 72, 253,
                216, 32, 97, 21, 74, 64,
            ];
            let result = binary_deserialize(&data);
            assert!(result.is_ok());
            let hashed_block = result.unwrap();
            let fixed_block = hashed_block.fixed_block();
            let attributes = fixed_block.attributes();
            assert_eq!(attributes.block_size(), &128);
            assert_eq!(attributes.data_size(), &16);
            assert_eq!(attributes.index(), &1908);
            assert_eq!(attributes.data_hash_offset(), &120);
            let attr_hash = fixed_block.attributes_hash();
            assert_eq!(attr_hash, &2007000491619541553);
            let padding = fixed_block.padding();
            assert_eq!(padding.len(), 48);
            let data = fixed_block.data();
            assert_eq!(data.len(), 16);
            assert_eq!(
                data,
                &vec![99, 200, 252, 200, 64, 246, 168, 19, 73, 119, 163, 211, 223, 153, 209, 220]
            );
            let hash = hashed_block.hash();
            assert_eq!(hash, &4632538673611078984);
            assert_eq!(hash, &calculate_hash(data));
        }
    }
}
