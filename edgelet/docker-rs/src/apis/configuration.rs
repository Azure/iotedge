// Currently the oldest dockerd we care about is the one in Debian 12, which is v20.10 and
// shipped with API version v1.41. So that is the version we use in request URLs.
//
// Note that the reason we had to switch at all is that newer versions of Docker raised the minimum supported API version
// to v1.40 and fail the request if it requests v1.34. See the API version mapping page linked below.
//
// Another concern is that, since we want to work with Docker and Moby engine on a variety of old and new distros,
// it is possible that the newest version of Docker might have a minimum API version that is newer than
// the maximum API version of the oldest version of Docker. Right now this is not a problem;
// the newest Docker release v29.5 has a minimum API version of v1.40,
// while the oldest Docker release v20.10 has a maximum API version of v1.41.
// There's also the fact that we officially only support moby-engine, which provides new releases
// even for old distributions like Debian 12.
//
// Ref:
//
// - Docker version <-> API version mapping: https://docs.docker.com/reference/api/engine/#api-version-matrix
// - API version changelog: https://docs.docker.com/reference/api/engine/version-history

pub struct Configuration {
    pub base_path: String,
    pub user_agent: Option<String>,
    pub uri_composer: Box<dyn Fn(&str, &str) -> anyhow::Result<hyper::Uri> + Send + Sync>,
}

impl Default for Configuration {
    fn default() -> Self {
        Configuration {
            base_path: "http://localhost/v1.41".to_owned(),
            user_agent: Some("edgelet/0.1.0".to_owned()),
            uri_composer: Box::new(|base_path, path| Ok(format!("{base_path}{path}").parse()?)),
        }
    }
}
