// Copyright (c) Microsoft. All rights reserved.

use std::fmt;
use std::fmt::Display;

use failure::{Backtrace, Context, Fail};
use url::Url;

pub type Result<T> = ::std::result::Result<T, Error>;

#[derive(Debug)]
pub struct Error {
    inner: Context<ErrorKind>,
}

#[derive(Debug, Fail, PartialEq)]
pub enum ErrorKind {
    #[fail(display = "Unable to load a kubernetes configuration file: {}", _0)]
    KubeConfig(KubeConfigErrorReason),

    #[fail(
        display = "Could not form well-formed URL by joining {:?} with {:?}",
        _0, _1
    )]
    UrlJoin(Url, String),

    #[fail(display = "Invalid URI to parse: {:?}", _0)]
    Uri(Url),

    #[fail(display = "Invalid HTTP header value {:?}", _0)]
    HeaderValue(String),

    #[fail(display = "A kubernetes client error occurred.")]
    KubeOpenApi,

    #[fail(display = "Hyper HTTP error")]
    Hyper,

    #[fail(display = "HTTP request error: {}", _0)]
    Request(RequestType),

    #[fail(display = "HTTP response error: {}", _0)]
    Response(RequestType),

    #[cfg(test)]
    #[fail(display = "HTTP test error")]
    HttpTest,
}

impl Fail for Error {
    fn cause(&self) -> Option<&dyn Fail> {
        self.inner.cause()
    }

    fn backtrace(&self) -> Option<&Backtrace> {
        self.inner.backtrace()
    }
}

impl Display for Error {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        Display::fmt(&self.inner, f)
    }
}

impl Error {
    pub fn new(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }

    pub fn kind(&self) -> &ErrorKind {
        self.inner.get_context()
    }
}

impl From<ErrorKind> for Error {
    fn from(kind: ErrorKind) -> Self {
        Error {
            inner: Context::new(kind),
        }
    }
}

impl From<Context<ErrorKind>> for Error {
    fn from(inner: Context<ErrorKind>) -> Self {
        Error { inner }
    }
}

#[derive(Clone, Debug, PartialEq)]
pub enum KubeConfigErrorReason {
    LoadConfig(String),
    LoadToken,
    LoadCertificate,
    MissingKubeConfig,
    MissingOrInvalidKubeContext,
    MissingUser,
    MissingData,
    Base64Decode,
    UrlParse(String),
    Tls,
    MissingEnvVar(String),
}

impl Display for KubeConfigErrorReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::LoadConfig(x) => {
                write!(f, "Could not load kubernetes configuration file: {}.", x)
            }
            Self::LoadToken => write!(f, "Could not load kubernetes authorization token."),
            Self::LoadCertificate => write!(f, "Could not load kubernetes root CA from."),
            Self::MissingKubeConfig => {
                write!(f, "Could not locate a kubernetes configuration file.")
            }
            Self::MissingOrInvalidKubeContext => write!(
                f,
                "Missing or invalid Kubernetes context in .kube/config file."
            ),
            Self::MissingUser => write!(f, "Missing user configuration in .kube/config file."),
            Self::MissingData => write!(f, "Both file and data missing."),
            Self::Base64Decode => write!(f, "Base64 decode error."),
            Self::UrlParse(x) => write!(f, "Unable to parse valid URL from: {}.", x),
            Self::Tls => write!(f, "Could not create TLS connector."),
            Self::MissingEnvVar(x) => write!(f, "Missing ENV: {}.", x),
        }
    }
}

#[derive(Clone, Debug, PartialEq)]
pub enum RequestType {
    ConfigMapList,
    ConfigMapCreate,
    ConfigMapReplace,
    ConfigMapDelete,
    DeploymentList,
    DeploymentCreate,
    DeploymentReplace,
    DeploymentDelete,
    PodList,
    NodeList,
    SecretList,
    SecretCreate,
    SecretReplace,
    TokenReview,
    SelfSubjectAccessReviewCreate,
    ServiceAccountList,
    ServiceAccountCreate,
    ServiceAccountReplace,
    ServiceAccountGet,
    ServiceAccountDelete,
    RoleBindingReplace,
    RoleBindingDelete,
}

impl Display for RequestType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:#}", self)
    }
}
