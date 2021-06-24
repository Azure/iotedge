/*!
 This crate provides a binary `obsagent-client` which  has two modes of operation:
    1. Prometheus mode
    2. OpenTelemetry mode

 The modes are set by enabling the 'prom' and 'otel' cargo feature flags.

 In OTel mode, the program sends example metrics to a configurable OTLP endpoint. The program
 loops, updating and exporting metrics for one example of each OTel instrument type (i.e,
 counter, up-down-counter, value-recorder, sum-observer, up-down-sum-observer,
 value-observer).

 In Prometheus mode, the program hosts a prometheus endpoint from which metrics can be pulled
 by a Prometheus scraper. As in the OTel mode, the program loops, updating metrics for one
 example of each Prometheus instrument type (counter, gauge, and histogram).

 The program is configurable via command line arguments as well as environment variables,
 with the environment variables taking precendence. The following is a list of the environment
 variables (and corresponding command line args in brackets):
    1. UPDATE_RATE [--update-rate|-u] - Rate at which each instrument is
            updated with a new metric measurement (updates/sec).
    2. PUSH_RATE [--push-rate|-p] - Rate at which measurements are pushed
            out of the OTel client (pushes/sec). Only used in OTel mode.
    3. OTLP_ENDPOINT [--otlp-endpoint|-e] - Endpoint to which OTLP messages
             will be sent.
    4. PROMETHEUS_ENDPOINT [--prometheus-endpoint] - Endpoint address from which Prometheus
             metrics can be pulled.
*/

pub mod config;
#[cfg(feature = "otel")]
pub mod otel_client;
#[cfg(feature = "prom")]
pub mod prometheus_server;
