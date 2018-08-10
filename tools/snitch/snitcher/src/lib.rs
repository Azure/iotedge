// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]
extern crate azure_sdk_for_rust;
extern crate backtrace;
extern crate byteorder;
extern crate bytes;
extern crate chrono;
extern crate futures;
extern crate hex;
extern crate http;
extern crate humantime;
extern crate hyper;
extern crate hyper_tls;
extern crate libflate;
#[macro_use]
extern crate log;
extern crate serde;
#[macro_use]
extern crate serde_derive;
#[cfg(not(test))]
extern crate serde_json;
#[cfg(test)]
#[macro_use]
extern crate serde_json;
extern crate serde_yaml;
extern crate tar;
extern crate tokio;
extern crate tokio_uds;
extern crate url;
extern crate url_serde;

pub mod client;
pub mod connect;
pub mod docker;
pub mod error;
pub mod influx;
pub mod report;
pub mod settings;
pub mod uds;

use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::Instant;

use azure_sdk_for_rust::core::lease::{LeaseState, LeaseStatus};
use azure_sdk_for_rust::core::ContainerNameSupport;
use azure_sdk_for_rust::storage::blob::{Blob, BlobType, PUT_OPTIONS_DEFAULT};
use azure_sdk_for_rust::storage::client::Client as AzureStorageClient;
use azure_sdk_for_rust::storage::client::Container as BlobContainer;
use azure_sdk_for_rust::storage::container::PublicAccess;
use azure_sdk_for_rust::storage::container::PublicAccessSupport;
use bytes::{BufMut, Bytes};
use chrono::Utc;
use futures::future::{self, loop_fn, Either, Loop};
use futures::{Future, IntoFuture, Stream};
use humantime::format_duration;
use hyper::{Client as HyperClient, Method};
use hyper_tls::HttpsConnector;
use serde_json::Value as JsonValue;
use tokio::timer::{Delay, Interval};

use connect::HyperClientService;
use docker::{Container, DockerClient};
use error::Error;
use influx::QueryResults;
use report::{MessageAnalysis, Report};
use settings::Settings;
use uds::{UnixConnector, Uri};

const LOGS_FILE_NAME: &str = "logs.tar.gz";

pub fn schedule_reports(settings: &Settings) -> impl Future<Item = (), Error = Error> + Send {
    // we schedule one report at the end of the test run
    let run_at = Instant::now() + *settings.test_duration();
    info!(
        "Scheduling end of test run to kick off in {}",
        format_duration(*settings.test_duration())
    );

    let settings_copy = settings.clone();
    let last_report = Delay::new(run_at)
        .map_err(Error::from)
        .and_then(|_| do_report(settings_copy));

    // and we schedule another periodic one for the specified reporting interval
    let periodic_report = if let Some(reporting_interval) = settings.reporting_interval() {
        let settings_copy = settings.clone();
        let run_at = Instant::now() + *reporting_interval;
        info!(
            "Scheduling periodic runs to run every {}",
            format_duration(*reporting_interval),
        );
        Either::A(
            Interval::new(run_at, *reporting_interval)
                .map_err(Error::from)
                .and_then(move |_| do_report(settings_copy.clone()))
                .collect()
                .map(|_| ()),
        )
    } else {
        Either::B(future::ok::<(), Error>(()))
    };

    last_report.join(periodic_report).map(|_| ())
}

pub fn do_report(settings: Settings) -> impl Future<Item = (), Error = Error> + Send {
    info!("Beginning report run");

    let report = Arc::new(Mutex::new(Report::new(format!("{}", settings.build_id()))));
    let timestamp = Utc::now().timestamp();

    // collect docker logs from all running containers
    let add_log_files = {
        let report = report.clone();
        let report_copy = report.clone();
        let settings_copy = settings.clone();
        get_module_logs(&settings)
            .and_then(move |module_logs| {
                info!("Fetched module logs.");

                // add each log as a file into the report
                for (container, logs) in &module_logs {
                    report
                        .lock()
                        .unwrap()
                        .add_file(&format!("./{}.log", container.name()), logs.as_bytes());
                }

                // write all the files in the report into blob storage
                info!("Compressing module logs");
                let buffer = Vec::new();
                let report = report.lock().unwrap();
                report.write_files(buffer.writer()).into_future()
            })
            .and_then(move |writer| {
                info!("Uploading module logs to blob storage");
                let buffer = writer.into_inner();
                let report_copy = report_copy.lock().unwrap();
                upload_file(
                    report_copy.id(),
                    &settings_copy,
                    &format!("{}/{}", timestamp, LOGS_FILE_NAME),
                    &buffer,
                )
            })
            .map(|_| info!("Module logs uploaded to blob storage"))
    };

    // collect metrics from influx
    let get_metrics = {
        let report = report.clone();
        get_metrics(&settings).map(move |metrics| {
            info!("Acquired Edge Hub metrics");
            for (name, results) in metrics {
                // add the metrics into the report
                report.lock().unwrap().add_metric(&name, results);
            }
        })
    };

    // collect report from analyzer module
    let get_analysis = {
        let report = report.clone();
        fetch_message_analysis(&settings).map(move |analysis| {
            info!("Got message analysis from analyzer");

            if let Some(analysis) = analysis {
                report.lock().unwrap().set_message_analysis(analysis);
            }
        })
    };

    // wait for all the bits to get done and then build report and alert
    let all_futures: Vec<Box<Future<Item = (), Error = Error> + Send>> = vec![
        Box::new(add_log_files),
        Box::new(get_metrics),
        Box::new(get_analysis),
    ];
    let report_copy = report.clone();
    future::join_all(all_futures)
        .and_then(move |_| {
            info!("Preparing report");

            let report = &mut *report_copy.lock().unwrap();
            let report_id = report.id().to_string();
            report.add_attachment(
                LOGS_FILE_NAME,
                &format!(
                    "https://{}.blob.core.windows.net/{}/{}/{}",
                    settings.blob_storage_account(),
                    report_id,
                    timestamp,
                    LOGS_FILE_NAME
                ),
            );
            report.add_notes(format!(
                "Test report generated at: {}",
                Utc::now().to_rfc3339()
            ));
            serde_json::to_value(report)
                .map_err(Error::from)
                .map(|report_json| Either::A(raise_alert(&settings, report_json)))
                .unwrap_or_else(|err| Either::B(future::err(err)))
        })
        .map(|_| info!("Report run complete"))
}

pub fn upload_file(
    report_id: &str,
    settings: &Settings,
    name: &str,
    data: &[u8],
) -> impl Future<Item = (), Error = Error> + Send {
    debug!(
        "Creating blob with name {} in container {}",
        name, report_id
    );

    let report_id = report_id.to_owned();
    let name = name.to_owned();
    let data = Bytes::from(data);
    AzureStorageClient::new(
        settings.blob_storage_account(),
        settings.blob_storage_master_key(),
    ).map(|client| {
        let fut = client
            .list()
            .finalize()
            .and_then(move |containers| {
                if containers.iter().find(|c| &c.name == &report_id).is_none() {
                    debug!(
                        "Blob container {} not found. Creating container.",
                        report_id
                    );
                    Either::A(
                        client
                            .create()
                            .with_container_name(&report_id)
                            .with_public_access(PublicAccess::Container)
                            .finalize()
                            .map(|_| (client, report_id)),
                    )
                } else {
                    debug!("Blob container {} already exists.", report_id);
                    Either::B(future::ok((client, report_id)))
                }
            })
            .and_then(move |(client, report_id)| {
                let blob = Blob {
                    name,
                    container_name: report_id.to_owned(),
                    snapshot_time: None,
                    last_modified: Utc::now(),
                    etag: String::new(),
                    content_length: data.len() as u64,
                    content_type: Some("application/gzip".to_owned()),
                    content_encoding: None,
                    content_language: None,
                    content_md5: None,
                    cache_control: None,
                    x_ms_blob_sequence_number: None,
                    blob_type: BlobType::BlockBlob,
                    lease_status: LeaseStatus::Unlocked,
                    lease_state: LeaseState::Available,
                    lease_duration: None,
                    copy_id: None,
                    copy_status: None,
                    copy_source: None,
                    copy_progress: None,
                    copy_completion: None,
                    copy_status_description: None,
                };

                blob.put(&client, &PUT_OPTIONS_DEFAULT, Some(data.as_ref()))
                    .map(|_| ())
            })
            .map_err(Error::from);

        Either::A(fut)
    })
        .map_err(Error::from)
        .unwrap_or_else(|err| Either::B(future::err(err)))
}

pub fn get_metrics(
    settings: &Settings,
) -> impl Future<Item = HashMap<String, QueryResults>, Error = Error> + Send {
    info!("Fetching Edge Hub metrics");

    match settings.influx_queries() {
        Some(queries) => {
            let influx_client = client::Client::new(
                HyperClientService::new(HyperClient::new()),
                settings.influx_url().clone(),
            );
            let influx = influx::Influx::new(settings.influx_db_name().to_string(), influx_client);

            let fut = future::join_all(queries.clone().into_iter().map(move |(name, query)| {
                influx.clone().query(&query).map(|results| (name, results))
            })).map(|results| {
                results
                    .into_iter()
                    .filter(|(_, v)| v.is_some())
                    .map(|(name, v)| (name, v.expect("Unwrap of an option with a value failed.")))
                    .collect()
            });

            Either::A(fut)
        }
        None => Either::B(future::ok(HashMap::new())),
    }
}

pub fn get_module_logs(
    settings: &Settings,
) -> impl Future<Item = Vec<(Container, String)>, Error = Error> + Send {
    info!("Fetching module logs");
    Uri::from_url(settings.docker_url())
        .map(|uri| {
            let docker_client = client::Client::new(
                HyperClientService::new(
                    HyperClient::builder()
                        // Setting keep_alive to false causes hyper to not pool connections.
                        // When using UDS this is the only mode in which things work. Not pooling
                        // connections for UDS is probably OK (?).
                        .keep_alive(false)
                        .build(UnixConnector),
                ),
                uri.into(),
            );
            let docker = DockerClient::new(docker_client);
            let docker_copy = docker.clone();

            debug!("Listing docker containers");
            let fut = docker.list_containers().and_then(|containers| {
                containers
                    .map(|containers| {
                        // exclude containers whose state is "created"
                        let containers = containers
                            .into_iter()
                            .filter(|c| c.state().map(|s| s != "created").unwrap_or(true));

                        let logs_futures = containers.map(move |container| {
                            debug!("Getting logs for container {}", container.name());
                            docker_copy.logs(container.id()).map(|logs| {
                                debug!("Got logs for container {}", container.name());
                                (container, logs.unwrap_or_else(|| "<no logs>".to_string()))
                            })
                        });

                        Either::A(future::join_all(logs_futures))
                    })
                    .unwrap_or_else(|| Either::B(future::ok(vec![])))
            });

            Either::A(fut)
        })
        .unwrap_or_else(|err| Either::B(future::err(err)))
}

pub fn fetch_message_analysis(
    settings: &Settings,
) -> impl Future<Item = Option<Vec<MessageAnalysis>>, Error = Error> + Send {
    info!("Fetching analysis from analyzer module");

    client::Client::new(
        HyperClientService::new(HyperClient::new()),
        settings.analyzer_url().clone(),
    ).request::<(), Vec<MessageAnalysis>>(
        Method::GET,
        settings.analyzer_url().path(),
        None,
        None,
        false,
    )
}

pub fn raise_alert(
    settings: &Settings,
    report_json: JsonValue,
) -> impl Future<Item = (), Error = Error> + Send {
    info!("Report ready. Posting to alert URL.");
    debug!(
        "Report: \n{}",
        serde_json::to_string_pretty(&report_json).unwrap()
    );

    settings
        .alert()
        .to_url()
        .and_then(|alert_url| {
            HttpsConnector::new(4)
                .map(|connector| (alert_url, connector))
                .map_err(Error::from)
        })
        .map(|(alert_url, connector)| {
            let client = client::Client::new(
                HyperClientService::new(HyperClient::builder().build(connector)),
                alert_url,
            );

            Either::A(
                client
                    .request::<JsonValue, ()>(
                        Method::POST,
                        settings.alert().path(),
                        Some(
                            settings
                                .alert()
                                .query()
                                .iter()
                                .map(|(k, v)| (k.as_str(), v.as_str()))
                                .collect(),
                        ),
                        Some(report_json),
                        false,
                    )
                    .map(|_| ()),
            )
        })
        .unwrap_or_else(|err| Either::B(future::err(err)))
}

pub fn seq<I>(
    i: I,
) -> impl Future<Item = Vec<<I::Item as IntoFuture>::Item>, Error = <I::Item as IntoFuture>::Error>
where
    I: IntoIterator,
    I::Item: IntoFuture,
{
    let iter = i.into_iter();
    loop_fn((vec![], iter), |(mut output, mut iter)| {
        let fut = if let Some(next) = iter.next() {
            Either::A(next.into_future().map(|v| Some(v)))
        } else {
            Either::B(future::ok(None))
        };

        fut.and_then(move |val| {
            if let Some(val) = val {
                output.push(val);
                Ok(Loop::Continue((output, iter)))
            } else {
                Ok(Loop::Break(output))
            }
        })
    })
}

#[cfg(test)]
mod tests {
    extern crate env_logger;

    use super::*;

    #[test]
    fn module_logs() {
        env_logger::init();

        let settings = Settings::default().merge_env().unwrap();
        let tasks = get_module_logs(&settings)
            .map(|_| ())
            .map_err(|err| panic!("ERROR: {:?}", err));
        tokio::run(tasks);
    }

    #[test]
    fn upload_blob() {
        env_logger::init();

        let settings = Settings::default().merge_env().unwrap();
        let tasks = upload_file("424242", &settings, "booyah/logs.tar.gz", &[1, 2, 3, 4, 5])
            .map(|_| ())
            .map_err(|err| panic!("ERROR: {:?}", err));
        tokio::run(tasks);
    }

    #[test]
    fn analysis() {
        env_logger::init();

        let settings = Settings::default().merge_env().unwrap();
        let tasks = fetch_message_analysis(&settings)
            .map(|_| ())
            .map_err(|err| panic!("ERROR: {:?}", err));
        tokio::run(tasks);
    }

    #[test]
    fn alert_report() {
        let json = json!({
            "attachments": {
                "logs.tar.gz": "https://blob/logs.tar.gz"
            },
            "id": "123456",
            "messageAnalysis": [],
            "metrics": {},
            "notes": [
                "Test report generated at: 2018-08-07T17:45:55.364690739+00:00"
            ]
        });
        env_logger::init();
        let settings = Settings::default().merge_env().unwrap();

        debug!("Alert URL: {}", settings.alert().to_url().unwrap());

        let tasks = raise_alert(&settings, json)
            .map(|_| ())
            .map_err(|err| panic!("ERROR: {:?}", err));
        tokio::run(tasks);
    }
}
