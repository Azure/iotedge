// Copyright (c) Microsoft. All rights reserved.

#![expect(
    clippy::doc_link_with_quotes,
    clippy::doc_markdown,
    clippy::empty_line_after_doc_comments,
    clippy::new_without_default,
    clippy::struct_field_names
)]
#![cfg(not(tarpaulin_include))]

pub mod apis;

#[expect(non_snake_case)]
pub mod models;

pub use apis::{DockerApi, DockerApiClient};
