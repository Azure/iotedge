use serde::{Deserialize, Serialize};
use serde::{Deserializer, Serializer};

/// Custom `deserialize_with` function to parse top-level API JSON with a
/// "status" field as a std::Result.
pub fn deserialize<'de, T, E, D>(des: D) -> Result<Result<T, E>, D::Error>
where
    D: Deserializer<'de>,
    T: Deserialize<'de>,
    E: Deserialize<'de>,
{
    #[derive(Debug, Deserialize)]
    #[serde(tag = "status", rename_all = "snake_case")]
    pub enum ApiOutputResult<T, E> {
        Ok(T),
        Err(E),
    }

    Ok(match ApiOutputResult::<T, E>::deserialize(des)? {
        ApiOutputResult::Ok(v) => Ok(v),
        ApiOutputResult::Err(e) => Err(e),
    })
}

/// Custom `serialize_with` function to attach
/// `#[serde(tag = "status", rename_all = "snake_case")]` to std::Result.
pub fn serialize<S, T, E>(v: &Result<T, E>, ser: S) -> Result<S::Ok, S::Error>
where
    S: Serializer,
    T: Serialize,
    E: Serialize,
{
    #[derive(Serialize)]
    #[serde(tag = "status", rename_all = "snake_case")]
    pub enum ApiOutputResult<'a, T, E> {
        Ok(&'a T),
        Err(&'a E),
    }

    ApiOutputResult::<T, E>::serialize(
        &match v {
            Ok(v) => ApiOutputResult::Ok(v),
            Err(e) => ApiOutputResult::Err(e),
        },
        ser,
    )
}
