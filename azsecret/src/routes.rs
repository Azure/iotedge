use crate::config::Configuration;
use crate::error::{Error, ErrorKind};
use crate::store::{Store, StoreBackend};

use std::error::{Error as StdError};
use std::convert::Infallible;
use std::future::Future;
use std::sync::Arc;

use hyper::{Body, Request, Response};
use hyper::service::Service;
use tokio::net::unix::UCred;
use warp::http::StatusCode;
use warp::{reject, Filter, Rejection, Reply};
use warp::filters::ext;
use warp::reply::with_status;

pub(crate) fn connect<T: StoreBackend>(backend: T, config: Configuration) -> impl Service<Request<Body>, Response = Response<Body>, Error = impl StdError, Future = impl Future<Output = Result<Response<Body>, impl StdError>>> + Clone + Send {
    let store = Arc::new(Store::new(backend, config));
    let ucred = ext::get::<UCred>()
        .or_else(|_| async { Err(reject::custom(Error::from(ErrorKind::Unauthorized))) });

    let copy = store.clone();
    let get_secret = ucred
        .and(warp::get())
        .and(warp::path::param())
        .and_then(move |cred, id| {
                let store = copy.clone();
                async move {
                    store.get_secret(cred, id)
                        .await
                        .map_err(reject::custom)
                }
            })
        .map(|val| warp::reply::json(&val));

    let copy = store.clone();
    let set_secret = ucred
        .and(warp::put())
        .and(warp::path::param())
        .and(warp::body::json::<String>())
        .and_then(move |cred, id, val| {
                let store = copy.clone();
                async move {
                    store.set_secret(cred, id, val)
                        .await
                        .map_err(reject::custom)
                }
            })
        .map(|_| StatusCode::NO_CONTENT);

    let copy = store.clone();
    let delete_secret = ucred
        .and(warp::delete())
        .and(warp::path::param())
        .and_then(move |cred, id| {
                let store = copy.clone();
                async move {
                    store.delete_secret(cred, id)
                        .await
                        .map_err(reject::custom)
                }
            })
        .map(|_| StatusCode::NO_CONTENT);

    let copy = store.clone();
    let pull_secret = ucred
        .and(warp::post())
        .and(warp::path::param())
        .and(warp::body::json::<String>())
        .and_then(move |cred, id, akv| {
                let store = copy.clone();
                async move {
                    store.pull_secret(cred, id, akv)
                        .await
                        .map_err(reject::custom)
                }
            })
        .map(|_| StatusCode::NO_CONTENT);

    warp::service(
            get_secret
                .or(set_secret)
                .or(delete_secret)
                .or(pull_secret)
                .recover(handle_error)
        )
}

async fn handle_error(err: Rejection) -> Result<impl Reply, Infallible> {
    println!("{:?}", err);
    Ok(
        err.find::<Error>()
            .map(|e|
                with_status(format!("{}", e), match e.kind() {
                        ErrorKind::CorruptData => StatusCode::BAD_REQUEST,
                        ErrorKind::Unauthorized => StatusCode::UNAUTHORIZED,
                        ErrorKind::Forbidden => StatusCode::FORBIDDEN,
                        ErrorKind::NotFound => StatusCode::NOT_FOUND,
                        _ => StatusCode::INTERNAL_SERVER_ERROR
                    })
                    .into_response()
            )
            .unwrap_or(StatusCode::NOT_FOUND.into_response())
    )
}