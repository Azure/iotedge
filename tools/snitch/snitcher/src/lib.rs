// Copyright (c) Microsoft. All rights reserved.

#![deny(warnings)]

pub mod client;
pub mod connect;
pub mod error;
pub mod report;
pub mod settings;

use std::io;
use std::sync::{Arc, Mutex};
use std::time::Instant;

use azure_sdk_for_rust::prelude::{
    BlobNameSupport, BodySupport, ContainerNameSupport, ContentTypeSupport, PrefixSupport,
    PublicAccessSupport,
};
use azure_sdk_for_rust::storage::client::{
    Blob, Client as AzureStorageClient, Container as AzureStorageContainer,
};
use azure_sdk_for_rust::storage::container::PublicAccess;
use bytes::{BufMut, Bytes};
use chrono::Utc;
use connect::HyperClientService;
use edgelet_core::{Chunked, LogChunk, LogDecode, LogOptions, Module, ModuleRuntime, ModuleStatus};
use edgelet_http_mgmt::ModuleClient;
use error::{Error, ErrorKind};
use futures::future::{self, loop_fn, Either, FutureResult, Loop};
use futures::{Future, IntoFuture, Stream};
use http::Uri;
use humantime::format_duration;
use hyper::header::{CONTENT_LENGTH, CONTENT_TYPE};
use hyper::{Body, Client as HyperClient, Method, Request};
use hyper_tls::HttpsConnector;
use log::{debug, error, info};
use report::{MessageAnalysis, Report};
use serde_json::Value as JsonValue;
use settings::Settings;
use tokio::timer::{Delay, Interval};

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
                for (container_name, logs) in module_logs {
                    report
                        .lock()
                        .unwrap()
                        .add_file(&format!("./{}.log", container_name), logs.as_bytes());
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

    // collect report from analyzer module
    let get_analysis = {
        let report = report.clone();
        fetch_message_analysis(&settings).map(move |analysis| {
            info!("Got message analysis from analyzer");

            if let Some(analysis) = analysis {
                report.lock().unwrap().set_device_analysis(analysis);
            }
        })
    };

    // wait for all the bits to get done and then build report and alert
    let all_futures: Vec<Box<Future<Item = (), Error = Error> + Send>> =
        vec![Box::new(add_log_files), Box::new(get_analysis)];
    let report_copy = report.clone();
    future::join_all(all_futures)
        .and_then(move |_| {
            info!("Preparing report");
            let report = &mut *report_copy.lock().unwrap();
            debug!(
                "alert url: {:?}, report: {:?}",
                &settings.alert().url(),
                report
            );
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

            info!("Serialize report to json");
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
    )
    .map(|client| {
        let fut = client
            .list_containers()
            .with_prefix(&report_id)
            .finalize()
            .and_then(move |containers| {
                if containers
                    .incomplete_vector
                    .vector
                    .iter()
                    .find(|c| &c.name == &report_id)
                    .is_none()
                {
                    debug!(
                        "Blob container {} not found. Creating container.",
                        report_id
                    );
                    Either::A(
                        client
                            .create_container()
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
                client
                    .put_block_blob()
                    .with_container_name(&report_id)
                    .with_blob_name(&name)
                    .with_body(data.as_ref())
                    .with_content_type("application/gzip")
                    .finalize()
                    .map(|_| ())
            })
            .map_err(Error::from);

        Either::A(fut)
    })
    .map_err(Error::from)
    .unwrap_or_else(|err| Either::B(future::err(err)))
}

pub fn get_module_logs(
    settings: &Settings,
) -> impl Future<Item = Vec<(String, String)>, Error = Error> + Send {
    info!("Fetching module logs");

    let module_client = ModuleClient::new(settings.management_uri())
        .map_err(|err| Error::new(ErrorKind::ModuleRuntime(err.to_string())))
        .expect(&format!(
            "Failed to instantiate module client with {}",
            settings.management_uri()
        ));
    debug!("Listing docker containers");
    module_client
        .list_with_details()
        .map_err(|err| Error::new(ErrorKind::ModuleRuntime(err.to_string())))
        .filter(|(_module, state)| {
            state.status() == &ModuleStatus::Running || state.status() == &ModuleStatus::Stopped
        })
        .and_then(move |(module, _state)| {
            debug!("Got logs for container {}", module.name());
            module_client
                .logs(module.name(), &LogOptions::new())
                .map_err(|err| Error::new(ErrorKind::ModuleRuntime(err.to_string())))
                .and_then(move |logs| {
                    let chunked = Chunked::new(
                        logs.map_err(|_| io::Error::new(io::ErrorKind::Other, "unknown")),
                    );
                    LogDecode::new(chunked)
                        .map_err(|err| Error::new(ErrorKind::ModuleRuntime(err.to_string())))
                        .fold(String::new(), |mut acc, chunk| match chunk {
                            LogChunk::Stdin(b)
                            | LogChunk::Stdout(b)
                            | LogChunk::Stderr(b)
                            | LogChunk::Unknown(b) => {
                                let result = std::str::from_utf8(&b)
                                    .map(|s| {
                                        acc.push_str(s);
                                        acc
                                    })
                                    .map_err(Error::from);

                                let f: FutureResult<String, Error> = future::result(result);
                                f
                            }
                        })
                        .map_err(|err| Error::new(ErrorKind::ModuleRuntime(err.to_string())))
                })
                .map_err(|err| Error::new(ErrorKind::ModuleRuntime(err.to_string())))
                .map(move |logs| {
                    let logs = if logs.is_empty() {
                        "<no logs>".to_string()
                    } else {
                        logs
                    };
                    (module.name().to_string(), logs)
                })
        })
        .collect()
}

pub fn fetch_message_analysis(
    settings: &Settings,
) -> impl Future<Item = Option<DeviceAnalysis>, Error = Error> + Send {
    info!("Fetching analysis from analyzer module");

    client::Client::new(
        HyperClientService::new(HyperClient::new()),
        settings.analyzer_url().clone(),
    )
    .request::<(), DeviceAnalysis>(
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

    HttpsConnector::new(4)
        .map(|connector| (settings.alert().url().clone(), connector))
        .map_err(Error::from)
        .map(|(alert_url, connector)| {
            let mut builder = Request::builder();
            let uri = alert_url
                .as_str()
                .parse::<Uri>()
                .expect("Unexpected Url to Uri conversion failure");
            let req = builder.method(Method::POST).uri(uri);
            let serialized = serde_json::to_string(&report_json).unwrap();
            req.header(CONTENT_TYPE, "text/json");
            req.header(CONTENT_LENGTH, format!("{}", serialized.len()).as_str());

            let hyper_client = HyperClient::builder().build(connector);
            let request = req.body(Body::from(serialized)).unwrap();
            debug!("send request to {}", request.uri());
            let result = hyper_client
                .request(request)
                .map_err(move |err| {
                    error!("HTTP request to {:?} failed with {:?}", alert_url, err);
                    Error::from(err)
                })
                .and_then(|resp| {
                    let status = resp.status();
                    debug!("HTTP request succeeded with status {}", status);
                    resp.into_body()
                        .concat2()
                        .map(move |body| (status, body))
                        .map_err(|err| {
                            error!("Reading response body failed with {:?}", err);
                            Error::from(err)
                        })
                        .and_then(move |(status, body)| {
                            if status.is_success() {
                                Ok(())
                            } else {
                                Err(Error::from((status, &*body)))
                            }
                        })
                });

            Either::A(result)
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
