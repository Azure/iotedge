// Copyright (c) Microsoft. All rights reserved.

pub struct TestServer {
    // The temp path object needs to be stored so that it's not dropped before TestServer.
    _path: mktemp::Temp,

    socket: url::Url,
}

impl TestServer {
    pub async fn start<TServer>(service: TServer) -> TestServer
    where
        TServer: hyper::service::Service<
                hyper::Request<hyper::Body>,
                Response = hyper::Response<hyper::Body>,
                Error = std::convert::Infallible,
            > + Clone
            + Send
            + 'static,
        <TServer as hyper::service::Service<hyper::Request<hyper::Body>>>::Future: Send,
    {
        let path = mktemp::Temp::new_path();
        let socket = path.to_str().expect("failed to determine socket path");
        let socket =
            url::Url::parse(&format!("unix://{}", socket)).expect("failed to parse socket path");
        let connector = http_common::Connector::new(&socket).expect("failed to create connector");
        let mut incoming = connector
            .incoming()
            .await
            .expect("failed to listen on socket");

        tokio::spawn(async move {
            incoming
                .serve(service)
                .await
                .expect("failed to serve socket");
        });

        TestServer {
            _path: path,
            socket,
        }
    }

    pub async fn process_request<TRequest, TResponse>(
        &self,
        method: http::Method,
        uri: url::Url,
        body: Option<&TRequest>,
    ) -> std::io::Result<TResponse>
    where
        TRequest: serde::Serialize,
        TResponse: serde::de::DeserializeOwned,
    {
        let connector =
            http_common::Connector::new(&self.socket).expect("failed to create connector");
        let client: hyper::Client<_, hyper::Body> = hyper::Client::builder().build(connector);

        http_common::request(&client, method, uri.as_str(), body).await
    }

    pub fn make_uri(path: &str, api_version: edgelet_http::ApiVersion) -> url::Url {
        let uri = url::Url::parse("http://test.sock").expect("failed to parse hardcoded url");
        let mut uri = uri.join(path).expect("failed to add url path");

        uri.set_query(Some(&format!("api-version={}", api_version)));

        uri
    }
}
