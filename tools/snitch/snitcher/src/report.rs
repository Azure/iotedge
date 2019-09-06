// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;
use std::io::Write;

use bytes::Bytes;
use chrono::{DateTime, Utc};
use libflate::gzip::Encoder as GzipEncoder;
use serde::{Deserialize, Serialize};
use tar::{Builder as TarBuilder, Header as TarHeader};

use crate::error::Result;

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Interval {
    missed_messages_count: u64,
    start_date_time: DateTime<Utc>,
    end_date_time: DateTime<Utc>,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DeviceAnalysis {
    message_analysis: Vec<MessageAnalysis>,
    dm_analysis: Vec<DirectMethodsAnalysis>
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MessageAnalysis {
    module_id: String,
    status_code: u16,
    status_message: String,
    received_messages_count: u64,
    last_message_received_at: DateTime<Utc>,
    missed_messages: Vec<Interval>,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DirectMethodsAnalysis {
    module_id: String,
    status_codes: Vec<DmStatusCode>
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DmStatusCode {
    status_code: String,
    count: u64,
    last_received_at: DateTime<Utc>
}

#[derive(Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Report {
    id: String,
    #[serde(skip)]
    files: Vec<(String, Bytes)>,
    notes: Vec<String>,
    message_analysis: Option<Vec<MessageAnalysis>>,
    attachments: HashMap<String, String>,
}

impl Report {
    pub fn new(id: String) -> Report {
        Report {
            id,
            files: vec![],
            notes: vec![],
            message_analysis: None,
            attachments: HashMap::new(),
        }
    }

    pub fn id(&self) -> &str {
        &self.id
    }

    pub fn set_message_analysis(&mut self, analysis: Vec<MessageAnalysis>) {
        self.message_analysis = Some(analysis);
    }

    pub fn add_file(&mut self, name: &str, data: &[u8]) -> &Self {
        self.files.push((name.to_owned(), Bytes::from(data)));
        self
    }

    pub fn add_notes(&mut self, notes: String) -> &Self {
        self.notes.push(notes);
        self
    }

    pub fn add_attachment(&mut self, name: &str, value: &str) {
        self.attachments.insert(name.to_owned(), value.to_owned());
    }

    pub fn write_files<W: Write>(&self, writer: W) -> Result<W> {
        // make a gzip from the tar
        let encoder = GzipEncoder::new(writer)?;

        // build a tar with all the file data
        let mut builder = TarBuilder::new(encoder);
        for (name, bytes) in &self.files {
            let mut header = TarHeader::new_gnu();
            header.set_path(name.as_str())?;
            header.set_size(bytes.len() as u64);
            header.set_cksum();

            builder.append(&header, bytes.as_ref())?;
        }
        builder.finish()?;

        // this is basically a series of unwraps to get at W:
        //  TarBuilder -> GzipEncoder<W> -> W
        Ok(builder.into_inner()?.finish().into_result()?)
    }
}
