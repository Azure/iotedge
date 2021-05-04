// Copyright (c) Microsoft. All rights reserved.

use serde::Serialize;

pub struct JsonConnector {
    //body: Vec<u8>,
}

impl JsonConnector {
    pub fn new<T: Serialize>(body: &T) -> JsonConnector {
        let body = serde_json::to_string(body).unwrap();
        let _body: Vec<u8> = format!(
            "HTTP/1.1 200 OK\r\n\
             Content-Type: application/json; charset=utf-8\r\n\
             Content-Length: {}\r\n\
             \r\n\
             {}",
            body.len(),
            body,
        )
        .into();

        JsonConnector {}
    }
}
