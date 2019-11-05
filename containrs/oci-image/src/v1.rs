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
use serde::{ser, Serialize, Serializer};

/// newtype for an i32 whose value is guaranteed to be 2
#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, Clone)]
#[serde(transparent)]
pub struct SchemaVersion {
    #[serde(
        deserialize_with = "de_validate_schema_is_2",
        serialize_with = "ser_validate_schema_is_2"
    )]
    version: i32,
}

impl SchemaVersion {
    pub fn new() -> SchemaVersion {
        SchemaVersion::default()
    }
}

impl Default for SchemaVersion {
    fn default() -> SchemaVersion {
        SchemaVersion { version: 2 }
    }
}

fn de_validate_schema_is_2<'de, D>(des: D) -> Result<i32, D::Error>
where
    D: Deserializer<'de>,
{
    match i32::deserialize(des)? {
        2 => Ok(2),
        _ => Err(de::Error::custom(
            "oci_image::v1 requires .schemaVersion = 2",
        )),
    }
}

#[allow(clippy::trivially_copy_pass_by_ref)]
fn ser_validate_schema_is_2<S>(value: &i32, serializer: S) -> Result<S::Ok, S::Error>
where
    S: Serializer,
{
    match value {
        2 => serializer.serialize_i32(2),
        _ => Err(ser::Error::custom(
            "oci_image::v1 requires .schemaVersion = 2",
        )),
    }
}
