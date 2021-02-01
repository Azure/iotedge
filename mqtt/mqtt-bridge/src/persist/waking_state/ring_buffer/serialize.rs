use serde::{Deserialize, Serialize};
use bincode::Result as BincodeResult;

#[allow(dead_code)]
pub(crate) fn binary_serialize<T>(something: &T) -> BincodeResult<Vec<u8>>
where
    T: Serialize,
{
    bincode::serialize(something)
}

#[allow(dead_code)]
pub(crate) fn binary_serialize_size<T>(something: &T) -> BincodeResult<usize>
where
    T: Serialize,
{
    bincode::serialized_size(something).map(|x| x as usize)
}

#[allow(dead_code)]
pub(crate) fn binary_deserialize<'de, T>(bytes: &'de [u8]) -> BincodeResult<T>
where
    T: Deserialize<'de>,
{
    bincode::deserialize::<T>(bytes)
}