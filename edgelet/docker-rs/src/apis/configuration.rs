use failure::{format_err, Error};
use hyper::Uri;

pub struct Configuration {
    pub base_path: String,
    pub user_agent: Option<String>,
    pub uri_composer: Box<dyn Fn(&str, &str) -> Result<Uri, Error> + Send + Sync>,
}

impl Configuration {
    pub fn new() -> Self {
        Configuration {
            base_path: "http://localhost/v1.34".to_owned(),
            user_agent: Some("edgelet/0.1.0".to_owned()),
            uri_composer: Box::new(|base_path, path| {
                format!("{}{}", base_path, path)
                    .parse()
                    .map_err(|e| format_err!("Url parse error: {}", e))
            }),
        }
    }
}
