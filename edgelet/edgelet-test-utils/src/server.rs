// Copyright (c) Microsoft. All rights reserved.

pub struct TestServer {
    socket: url::Url,
    //server_handle: tokio::task::JoinHandle<()>,
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
        let socket = mktemp::Temp::new_path();
        let socket = socket.to_str().expect("failed to determine socket path");
        let socket =
            url::Url::parse(&format!("unix://{}", socket)).expect("failed to parse socket path");

        //let server_handle = tokio::spawn(async move {
            let connector = http_common::Connector::new(&socket).expect("failed to create connector");

            let mut incoming = connector.incoming().await.unwrap();
            incoming.serve(service).await.unwrap();
        //});

        TestServer {
            socket,
            //server_handle,
        }
    }

    pub async fn stop(self) {
        /*self.server_handle.abort();

        if let Err(err) = self.server_handle.await {
            assert!(err.is_cancelled());
        }*/
    }

    pub async fn process_request<TRequest, TResponse>(
        &self,
        method: http::Method,
        uri: &str,
        body: Option<&TRequest>,
    ) -> TResponse
    where
        TRequest: serde::Serialize,
        TResponse: serde::de::DeserializeOwned,
    {
        let connector =
            http_common::Connector::new(&self.socket).expect("failed to create connector");
        let client: hyper::Client<_, hyper::Body> = hyper::Client::builder().build(connector);

        http_common::request(&client, method, uri, body)
            .await
            .expect("server request failed")
    }
}
