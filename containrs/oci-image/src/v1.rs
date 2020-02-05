pub mod annotations;
pub mod media_type;
pub mod util;

mod config;
mod descriptor;
mod index;
mod manifest;

pub use annotations::Annotations;
pub use config::*;
pub use descriptor::*;
pub use index::*;
pub use manifest::*;

use oci_common::fixed_newtype;

fixed_newtype! {
    /// oci_image::v1 requires that all .schemaVersion fields to be equal to 2
    pub struct SchemaVersion2(i32) == 2i32;
    else "oci_image::v1 requires .schemaVersion = 2";
}
