// Copyright (c) Microsoft. All rights reserved.

use std::collections::HashMap;

use futures::Future;
use hyper::service::Service;
use hyper::{Body, Error as HyperError, Method};
use serde_json::Value as JsonValue;

use client::Client;
use error::Error;

#[derive(Debug, Serialize, Deserialize)]
pub struct QueryResults {
    results: Vec<QueryResult>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct QueryResult {
    statement_id: u32,
    series: Vec<Series>,
}

#[derive(Debug, Serialize, Deserialize)]
pub struct Series {
    name: String,
    columns: Vec<String>,
    values: Vec<Vec<JsonValue>>,
}

#[derive(Clone)]
pub struct Influx<S>
where
    S: 'static + Service<ReqBody = Body, ResBody = Body, Error = HyperError> + Send,
{
    db_name: String,
    client: Client<S>,
}

impl<S> Influx<S>
where
    S: 'static + Service<ReqBody = Body, ResBody = Body, Error = HyperError> + Send,
    <S as Service>::Future: Send,
{
    pub fn new(db_name: String, client: Client<S>) -> Influx<S> {
        Influx { db_name, client }
    }

    pub fn query(&self, sql: &str) -> impl Future<Item = Option<QueryResults>, Error = Error> {
        let mut query = HashMap::new();
        query.insert("db", self.db_name.as_str());
        query.insert("q", &sql);

        self.client
            .request::<(), QueryResults>(Method::GET, "query", Some(query), None, false)
    }
}
