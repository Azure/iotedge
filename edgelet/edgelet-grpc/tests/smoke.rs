// Copyright (c) Microsoft. All rights reserved.

extern crate bytes;
extern crate edgelet_grpc;
extern crate env_logger;
extern crate failure;
#[macro_use]
extern crate failure_derive;
extern crate futures;
extern crate http;
extern crate prost;
#[macro_use]
extern crate prost_derive;
extern crate tempfile;
extern crate tokio_core;
extern crate tower_grpc;
extern crate url;

mod calculator {
    include!(concat!(env!("OUT_DIR"), "/calculator.rs"));
}

use futures::{oneshot, Future};
use futures::future::FutureResult;
use edgelet_grpc::{Connect, Connection, Listener, TcpConnector, TcpListener};
use tokio_core::reactor::{Core, Handle};
use tower_grpc::{Request, Response};
use url::Url;

use calculator::{AddReply, AddRequest};

#[derive(Debug, Fail)]
pub enum Error {
    #[fail(display = "general failure")] General,
}

trait Calculator {
    type AddFuture: Future<Item = i64, Error = Error>;

    fn add(&mut self, a: i32, b: i32) -> Self::AddFuture;
}

#[derive(Clone, Debug)]
struct CalculatorImpl;

impl Calculator for CalculatorImpl {
    type AddFuture = FutureResult<i64, Error>;

    fn add(&mut self, a: i32, b: i32) -> Self::AddFuture {
        Ok((a + b) as i64).into()
    }
}

// Server components
#[derive(Clone, Debug)]
struct Server<S: Calculator + Clone> {
    inner: S,
}

impl<S: Calculator + Clone> Server<S> {
    pub fn new(service: S) -> Server<S> {
        Server { inner: service }
    }
}

impl<S: Calculator + Clone + 'static> calculator::server::Calculator for Server<S> {
    type AddFuture = Box<Future<Item = Response<AddReply>, Error = tower_grpc::Error>>;

    fn add(&mut self, request: Request<AddRequest>) -> Self::AddFuture {
        let response = self.inner
            .add(request.get_ref().a, request.get_ref().b)
            .map(|sum| Response::new(AddReply { sum }))
            .map_err(|_| tower_grpc::Error::from(()));
        Box::new(response)
    }
}

// Client components
struct Client<C>
where
    C: Connect,
{
    inner: calculator::client::Calculator<Connection<C>>,
}

impl<C: Connect> Client<C> {
    pub fn new(service: calculator::client::Calculator<Connection<C>>) -> Client<C> {
        Client { inner: service }
    }
}

impl<C: Connect> Calculator for Client<C> {
    type AddFuture = Box<Future<Item = i64, Error = Error>>;

    fn add(&mut self, a: i32, b: i32) -> Self::AddFuture {
        let sum = self.inner
            .add(Request::new(AddRequest { a, b }))
            .map(|reply| reply.get_ref().sum)
            .map_err(|_| Error::General);
        Box::new(sum)
    }
}

// Helper functions
fn connect<C: Connect>(
    connector: C,
    address: Url,
    handle: Handle,
) -> Box<Future<Item = Client<C>, Error = Error>> {
    let client = edgelet_grpc::connect(connector, address, handle)
        .and_then(move |conn| {
            use calculator::client::Calculator;
            let client = Client::new(Calculator::new(conn));
            Ok(client)
        })
        .map_err(|_| Error::General);
    Box::new(client)
}

fn server<S: Calculator + Clone + 'static>(
    service: S,
) -> calculator::server::CalculatorServer<Server<S>> {
    calculator::server::CalculatorServer::new(Server::new(service))
}

// Tests

macro_rules! t {
     ($e:expr) => (match $e {
        Ok(e) => e,
        Err(e) => panic!("{} failed with {:?}", stringify!($e), e),
    })
}

#[test]
pub fn tcp() {
    drop(env_logger::init());

    // arrange
    let url = t!(Url::parse("tcp://127.0.0.1:50000"));
    let mut core = t!(Core::new());
    let connector = TcpConnector::new(&core.handle());
    let (complete, shutdown) = oneshot::<()>();

    let srv = t!(TcpListener::bind_handle(
        url.clone(),
        &core.handle(),
        server(CalculatorImpl)
    )).run_until(shutdown.map_err(|_| ()));

    let request = connect(connector, url.clone(), core.handle())
        .and_then(|mut client| client.add(1, 2))
        .and_then(|result| {
            // assert
            t!(complete.send(()));
            assert_eq!(3, result);
            Ok(())
        })
        .map_err(|e| panic!("panic: {:?}", e));

    // act
    if let Err(_) = core.run(srv.select(request)) {
        panic!("test run failed");
    }
}

#[cfg(unix)]
mod unix {
    use super::*;
    use std::fs::File;

    use edgelet_grpc::{Listener, UnixConnector, UnixListener};

    #[test]
    pub fn unix_first_run() {
        drop(env_logger::init());
        // This tests the case where the file has been created and we are rebinding

        // arrange
        let dir = t!(tempfile::TempDir::new());
        let path = dir.path().join("grpc.sock");
        let url = t!(Url::parse(&format!("unix://{}", path.to_str().unwrap())));

        t!(File::create(path));

        let mut core = t!(Core::new());
        let connector = UnixConnector::new(&core.handle());
        let (complete, shutdown) = oneshot::<()>();

        let srv = t!(UnixListener::bind_handle(
            url.clone(),
            &core.handle(),
            server(CalculatorImpl)
        )).run_until(shutdown.map_err(|_| ()));

        let request = connect(connector, url.clone(), core.handle())
            .and_then(|mut client| client.add(1, 2))
            .and_then(|result| {
                // assert
                t!(complete.send(()));
                assert_eq!(3, result);
                Ok(())
            })
            .map_err(|e| panic!("panic: {:?}", e));

        // act
        if let Err(_) = core.run(srv.select(request)) {
            panic!("test run failed");
        }
    }

    #[test]
    pub fn unix_rerun() {
        drop(env_logger::init());
        // This tests the case where the file hasn't been created yet (first run)

        // arrange
        let dir = t!(tempfile::TempDir::new());
        let url = t!(Url::parse(&format!(
            "unix://{}",
            dir.path().join("grpc.sock").to_str().unwrap()
        )));
        let mut core = t!(Core::new());
        let connector = UnixConnector::new(&core.handle());
        let (complete, shutdown) = oneshot::<()>();

        let srv = t!(UnixListener::bind_handle(
            url.clone(),
            &core.handle(),
            server(CalculatorImpl)
        )).run_until(shutdown.map_err(|_| ()));

        let request = connect(connector, url.clone(), core.handle())
            .and_then(|mut client| client.add(1, 2))
            .and_then(|result| {
                // assert
                t!(complete.send(()));
                assert_eq!(3, result);
                Ok(())
            })
            .map_err(|e| panic!("panic: {:?}", e));

        // act
        if let Err(_) = core.run(srv.select(request)) {
            panic!("test run failed");
        }
    }
}
