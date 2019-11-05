mod config;
mod descriptor;
mod index;
mod manifest;

pub mod annotations;
pub mod image_layout;
pub mod media_type;

pub use annotations::*;
pub use config::*;
pub use descriptor::*;
pub use image_layout::*;
pub use index::*;
pub use manifest::*;

use crate::MediaType;

use serde::{de, Deserialize, Deserializer};

fn validate_schema_is_2<'de, D>(des: D) -> Result<i32, D::Error>
where
    D: Deserializer<'de>,
{
    match i32::deserialize(des)? {
        2 => Ok(2),
        _ => Err(de::Error::custom(
            "v1::Manifest must have .schemaVersion = 2",
        )),
    }
}
