// Copyright (c) Microsoft. All rights reserved.

#[derive(Default)]
pub struct Settings {
    pub edge_ca_cert: Option<String>,
    pub edge_ca_key: Option<String>,
    pub edge_ca_auto_renew: Option<cert_renewal::AutoRenewConfig>,
    pub edge_ca_subject: Option<aziot_certd_config::CertSubject>,

    pub trust_bundle: Option<String>,
    pub manifest_trust_bundle: Option<String>,
}

impl edgelet_settings::RuntimeSettings for Settings {
    type ModuleConfig = crate::runtime::Config;

    fn edge_ca_cert(&self) -> Option<&str> {
        self.edge_ca_cert.as_deref()
    }

    fn edge_ca_key(&self) -> Option<&str> {
        self.edge_ca_key.as_deref()
    }

    fn edge_ca_auto_renew(&self) -> &Option<cert_renewal::AutoRenewConfig> {
        &self.edge_ca_auto_renew
    }

    fn edge_ca_subject(&self) -> &Option<aziot_certd_config::CertSubject> {
        &self.edge_ca_subject
    }

    fn trust_bundle_cert(&self) -> Option<&str> {
        self.trust_bundle.as_deref()
    }

    fn manifest_trust_bundle_cert(&self) -> Option<&str> {
        self.manifest_trust_bundle.as_deref()
    }

    // The functions below aren't used in tests.

    fn hostname(&self) -> &str {
        unimplemented!()
    }

    fn auto_reprovisioning_mode(&self) -> edgelet_settings::aziot::AutoReprovisioningMode {
        unimplemented!()
    }

    fn homedir(&self) -> &std::path::Path {
        unimplemented!()
    }

    fn allow_elevated_docker_permissions(&self) -> bool {
        unimplemented!()
    }

    fn iotedge_max_requests(&self) -> &edgelet_settings::IotedgeMaxRequests {
        unimplemented!()
    }

    fn agent(&self) -> &edgelet_settings::module::Settings<Self::ModuleConfig> {
        unimplemented!()
    }

    fn agent_mut(&mut self) -> &mut edgelet_settings::module::Settings<Self::ModuleConfig> {
        unimplemented!()
    }

    fn connect(&self) -> &edgelet_settings::uri::Connect {
        unimplemented!()
    }

    fn listen(&self) -> &edgelet_settings::uri::Listen {
        unimplemented!()
    }

    fn watchdog(&self) -> &edgelet_settings::watchdog::Settings {
        unimplemented!()
    }

    fn endpoints(&self) -> &edgelet_settings::aziot::Endpoints {
        unimplemented!()
    }

    fn additional_info(&self) -> &std::collections::BTreeMap<String, String> {
        unimplemented!()
    }

    fn image_garbage_collection(&self) -> &edgelet_settings::base::image::ImagePruneSettings {
        unimplemented!()
    }
}
