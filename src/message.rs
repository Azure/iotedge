use crate::util::*;

use serde::{Deserialize, Serialize};

mod pull_secrets {
    const METHOD_BINDING: &str = "Secret.Pull";

    #[derive(Debug, super::Deserialize)]
    pub struct Request<'a> {
        keys: Option<Vec<&'a str>>,
        basename: &'a str
    }
}

mod get_secret {
    const METHOD_BINDING: &str = "Secret.Get";

    #[derive(Debug, super::Deserialize)]
    pub struct Request<'a> {
        key: &'a str
    }
}

mod set_secret {
    const METHOD_BINDING: &str = "Secret.Set";

    #[derive(Debug, super::Deserialize)]
    pub struct Request<'a> {
        key: &'a str,
        value: &'a str
    }
}

fn pull_secrets(req: pull_secrets::Request<'_>) -> BoxedResult<()> {
    Ok(())
}

fn get_secret(req: get_secret::Request<'_>) -> BoxedResult<&str> {
    Ok("Foo")
}

fn set_secret(req: set_secret::Request<'_>) -> BoxedResult<()> {
    Ok(())
}
