use bincode::{deserialize, serialize, serialized_size, Result as BincodeResult};
use serde::{Deserialize, Serialize};
use std::fmt::Debug;

#[allow(dead_code)]
pub(crate) fn binary_serialize<T>(something: &T) -> BincodeResult<Vec<u8>>
where
    T: Serialize + Debug,
{
    serialize(something)
}

#[allow(dead_code)]
pub(crate) fn binary_serialize_size<T>(something: &T) -> BincodeResult<usize>
where
    T: Serialize + Debug,
{
    serialized_size(something).map(|x| x as usize)
}

#[allow(dead_code)]
pub(crate) fn binary_deserialize<'de, T>(bytes: &'de [u8]) -> BincodeResult<T>
where
    T: Deserialize<'de> + Debug,
{
    deserialize::<T>(bytes)
}
