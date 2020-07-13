use std::{env, fs, path::Path};

use anyhow::{bail, Result};
use chrono::{Duration, Utc};

use mqtt_broker::{Broker, BrokerBuilder, BrokerConfig, BrokerSnapshot, Server};
use mqtt_broker_core::auth::Authorizer;
use mqtt_edgehub::{
    auth::{EdgeHubAuthenticator, EdgeHubAuthorizer, LocalAuthenticator, LocalAuthorizer},
    edgelet,
    tls::Identity,
};

pub async fn broker(
    config: &BrokerConfig,
    state: Option<BrokerSnapshot>,
) -> Result<Broker<LocalAuthorizer<EdgeHubAuthorizer>>> {
    let broker = BrokerBuilder::default()
        .with_authorizer(LocalAuthorizer::new(EdgeHubAuthorizer::default()))
        .with_state(state.unwrap_or_default())
        .with_config(config.clone())
        .build();

    Ok(broker)
}

pub async fn server<Z>(config: &BrokerConfig, broker: Broker<Z>) -> Result<Server<Z>>
where
    Z: Authorizer + Send + 'static,
{
    // TODO read from config
    let url = "http://localhost:7120/authenticate/".into();
    let authenticator = EdgeHubAuthenticator::new(url);

    let mut server = Server::from_broker(broker);

    if let Some(tcp) = config.transports().tcp() {
        server.tcp(tcp.addr(), authenticator.clone());
    }

    if let Some(tls) = config.transports().tls() {
        dowload_server_certificate(tls.cert_path()).await?;
        server.tls(tls.addr(), tls.cert_path(), authenticator.clone())?;
    }

    // TODO read from config
    server.tcp("localhost:1882", LocalAuthenticator::new());

    Ok(server)
}

pub const WORKLOAD_URI: &str = "IOTEDGE_WORKLOADURI";
pub const EDGE_DEVICE_HOST_NAME: &str = "EDGEDEVICEHOSTNAME";
pub const MODULE_ID: &str = "IOTEDGE_MODULEID";
pub const MODULE_GENERATION_ID: &str = "IOTEDGE_MODULEGENERATIONID";

pub const CERTIFICATE_VALIDITY_DAYS: i64 = 90;

async fn dowload_server_certificate(path: impl AsRef<Path>) -> Result<()> {
    let uri = env::var(WORKLOAD_URI)?;
    let hostname = env::var(EDGE_DEVICE_HOST_NAME)?;
    let module_id = env::var(MODULE_ID)?;
    let generation_id = env::var(MODULE_GENERATION_ID)?;
    let expiration = Utc::now() + Duration::days(CERTIFICATE_VALIDITY_DAYS);

    let client = edgelet::workload(&uri)?;
    let cert = client
        .create_server_cert(&module_id, &generation_id, &hostname, expiration)
        .await?;

    if cert.private_key().type_() != "key" {
        bail!(
            "unknown type of private key: {}",
            cert.private_key().type_()
        );
    }

    if let Some(private_key) = cert.private_key().bytes() {
        let identity = Identity::try_from(cert.certificate(), private_key)?;
        fs::write(path, identity)?;
    } else {
        bail!("missing private key");
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use std::env;

    use mockito::mock;
    use serde_json::json;

    use super::*;

    #[tokio::test]
    async fn it_downloads_server_cert() {
        let expiration = Utc::now() + Duration::days(90);
        let res = json!(
            {
                "privateKey": { "type": "key", "bytes": "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDBPIj4l3QxdX0t\nz6audxzg9NQUKpA5Yb+fvC8cK8/B8/ivXTO6uMZV48sGHmP3+lu7y+L48VGwUsN3\ntC+/l/lEocHKPL8G4kKo2zX4wM/ea5UcHK3338ZdOPhhHMu1Goa79teN7N9giD7W\nChIFB9B1XUcQYeiq8FowkLlH7MrKMR7DNSqjyOaeBqafO2OLkZF9uPCK4ic92qdv\ni1rgIFeeFc7swDMdEh/TG4L9P4NpNfD36KiTOZgpLVPPG66I6Dsekhjrv5F6U9yp\nll5nJFTCmpI6hREWvRl03vo4IBVEu7lSCmNnc3Fdn0p1/QojEb167VCh6yINah9k\nj1LEWLYDAgMBAAECggEAQKYmFTVmlF2AYeFFHL2RFdoTUiPjWK9RUvm8sSofOf6L\nxu/hrKjBAl9Rv1xPidli83OFHlBuShWGe/f2uZz4snODyEuGhaERODkO14h6gylv\nG5akxXdCgFHdF3Bw3shfJ2ewOjVzjnJGw4le+fvhTELG0b5P/1Cme/UPZ/ba6cXs\nTrGyWJk4ef/IevrObf0DPh5XvqDYM8NiRCL19o5ZXK2w9Z1QJva7o2A0OrQ6Dkfa\navPXl242xd6c06f59akUi4CFNVq2dPdRRmadQkb64Sg2gJ+oxrnj+nZ3BRA9uZ1q\n96cK1/QjFjjB++wALYcCUB9uEWpZgstFuhNAHZIkEQKBgQDo/Y/A/Zq7T/LHM1fU\nVCcmMwtDAVEa69mnIy1rTM1WrZF3lJdj/LR3Ie7cYVa7YZu6CwZZCCHMpJiTOEa5\nA+8ruOFZzk7dRIHbyUEKFJ5cOXW8U7fDjCrAQGULNwJPiwFWN2WaasWb6yUT3lNb\nNtxYVV3QlgXzfKwPRZkN8ksuJQKBgQDUUerM73A3pMEoLNNQWHbZ2MaYZKQvvLNh\nldbNBXAxl3FiWdZnuUhy76oNQCgl2WQr6t9uoeLU+WQsbBb0STpq/1pKtdrcbMS8\nsaHPC9YTV2SWqBQcZ90oX5PcDHNGiGWXg+NEbv7Twv6Q3EraRKY69gswjIJZNv6N\nmE6guLa3BwKBgAfOl0aMOOcV4riyC7tdpoItK69vF9yjEoP7NcZmqGrDHiC3per/\nyLxFMs/HozRcLO+q9ud80kUdLs+gDx6b9yOr6kEsHJBlf0RyFe/UiQnmEv/gfkPb\nKoOOpNQfX8Byk/Tjnk/yS/TRiEiGJpzj1bZQBfi7Ti++ebV5S4ugFszRAoGAT9RR\nBSbNuY4vtexMs3vfi/8OhIPqm6xGgd11uvZdfbfkQMdobu1iSRzFgl/p+HkpSb3w\nrkcd41e1i2JEqyrRVSOXjlmb5Z7bxdq/7PoVToZgYqjljtyacbCmBmmD+jQUhohn\nLfbRx0scrdi2KCUBn0+dW26pH+Qmh5SJk7J/uIcCgYEAjdQB2lyda3keZb+bB/WL\n22SuwmcXKDCRw25d8n3lXEqB9C/01qvz1+AwQwSCBzW+JVRAnk1QzqxXqGnncTqI\ntuMSRV6rq/R8OGd0NH+QPMSLPemgvF1bCFd8+a36dt/e84Q4PBh1vOzZaSGPoE58\nIWF5NoDovkqU7R2qIDFs/ZI=\n-----END PRIVATE KEY-----\n" },
                "certificate": "-----BEGIN CERTIFICATE-----\nMIIEbjCCAlagAwIBAgIEdLDcUTANBgkqhkiG9w0BAQsFADAfMR0wGwYDVQQDDBRp\nb3RlZGdlZCB3b3JrbG9hZCBjYTAeFw0yMDA3MDkyMTI5NTNaFw0yMDEwMDcyMDEw\nMDJaMBQxEjAQBgNVBAMMCWxvY2FsaG9zdDCCASIwDQYJKoZIhvcNAQEBBQADggEP\nADCCAQoCggEBAME8iPiXdDF1fS3Ppq53HOD01BQqkDlhv5+8Lxwrz8Hz+K9dM7q4\nxlXjywYeY/f6W7vL4vjxUbBSw3e0L7+X+UShwco8vwbiQqjbNfjAz95rlRwcrfff\nxl04+GEcy7Uahrv2143s32CIPtYKEgUH0HVdRxBh6KrwWjCQuUfsysoxHsM1KqPI\n5p4Gpp87Y4uRkX248IriJz3ap2+LWuAgV54VzuzAMx0SH9Mbgv0/g2k18PfoqJM5\nmCktU88brojoOx6SGOu/kXpT3KmWXmckVMKakjqFERa9GXTe+jggFUS7uVIKY2dz\ncV2fSnX9CiMRvXrtUKHrIg1qH2SPUsRYtgMCAwEAAaOBvDCBuTAJBgNVHRMEAjAA\nMA4GA1UdDwEB/wQEAwID+DATBgNVHSUEDDAKBggrBgEFBQcDATAdBgNVHREEFjAU\ngglsb2NhbGhvc3SCB2VkZ2VodWIwHQYDVR0OBBYEFNiOqy/sZzR6MHKk6pYU0SlR\nz7TGMEkGA1UdIwRCMECAFAbOyQy9xAl46d39FhI0dQXohbRPoSKkIDAeMRwwGgYD\nVQQDDBNUZXN0IEVkZ2UgRGV2aWNlIENBggRri0VnMA0GCSqGSIb3DQEBCwUAA4IC\nAQBGZvInLdwQ9mKMcwM2E6kjIWuCcBOb1HXEswyDc8IKmtJS504s6Ybir/+Pb30m\nrfeWfoMWMP8GS4UeIm9T0zzYFBuDcwqmsQxLZLSigUMmXryEwt6zp1ksZSIEIkFi\nmKNFLuJSzPmLFFACsNQwsgl3qG2qqaMhOrRDEl/OH57tCFbLFVnSLWwB3XX4CsF3\nvN/3Ys+Bf4Y1gtY6gctByI6NCimQQYEaC1BygSUh/nwyjlAy1H8Vu+8+TymJ0KHK\neee+y/9OkCxUqPHDHmE6JKVefkNwqbb6w+Sl9MQZXRVepNfuTzVF3iTyKu4SARPE\nw19SRlNEfKM+W9U/T0shv3ay0W+3dry/5eY5nX6nuKx2Tt56iC5bjhCpUmsKuWoU\nXGE7z48ZhG2qwPIlNIbTzKFvXL4AXGEhoCot7xPwohTwPUxuDAYGibAB9BKjm3/0\nNgAPXqT82xpwX//mtRAoLFpSGct3E62KiLZD+RJnoC5A2X7KnQKnQndmEHwKGotS\nGJ1GZwU99C+kuG9MD+aNZJZBozcdoRZKT56438J25pOemjTy4MjFs+t3nWe4jtK8\n/gKeDoonQcvGbHR6+ukI+BDgyQwe+jvulA5ESanERONm42bnmZUuXxp2pZYKiB6q\nov2gTgQyaRE8rbX4SSPZghE5km7p6FAIjm/uqU9kGMUk3A==\n-----END CERTIFICATE-----\n-----BEGIN CERTIFICATE-----\nMIIFTDCCAzSgAwIBAgIEa4tFZzANBgkqhkiG9w0BAQsFADAeMRwwGgYDVQQDDBNU\nZXN0IEVkZ2UgRGV2aWNlIENBMB4XDTIwMDcwOTIwMTI1N1oXDTIwMTAwNzIwMTAw\nMlowHzEdMBsGA1UEAwwUaW90ZWRnZWQgd29ya2xvYWQgY2EwggIiMA0GCSqGSIb3\nDQEBAQUAA4ICDwAwggIKAoICAQDTJsX5hWqhnHS9M8jDeXglmWYyJEWjniUdSMVM\nFZvA1at8BQQv2KN7XtTbgDQCaPNv+2jJ1FisI2PfBzZvl4hJ0n/UBaDyR2cDiS+x\n4mEawGkIlHYYrX23iv9s1ODJw1JoTv9F69doZ3yRiI8xTDUBOx+XldxeGS7ROhX/\ncAzrt48fZLzmezzMafOCkiSQiVfmg5jn5YLZD52yLyHp5QW2Zqb9RSqTa+8EoK+f\nWUMzBnBiSHFcgZamAhqFGrir78FyVAyt/1J90N/DyEIoUAJ6aP1sbm9NZ9yCgJGQ\nZh3jnPOb+gT1N6WO3/VGC8PfYy5Hro74DFpmjASzYW77YzHb6LBJX3mwTEMb3/Rs\nGOx7KgnTmVimkNHaZy/BDTwn7muNZ69S7/NVfhG3t699o5p7jeXUMCs0UpnNqMfl\n2MWXnylUoV3FRC4GjuTvujlI9nUN5Jo86AyXusNktg9DprLemgREK4yYzaq6y9hW\noHzMugIcz3DaAyE7yIdiUxakXjqmheq2KvfZyBoqDkW5kSIo+jq/wlnsDpNghP2G\n+uF3YXoChXX5KSrK+vx5OOdBKBJ55C5eQULbUovJVm0hImWgcPSt+xaLjHMQw5zZ\n6KaWW9oOxAn68rzQvx/2BWqSgsHRpGMlv5OesXNgQWmOMyZkav6X6J7Hv8hjrKwx\nUJLKpwIDAQABo4GQMIGNMBIGA1UdEwEB/wQIMAYBAf8CAQAwDgYDVR0PAQH/BAQD\nAgKEMB0GA1UdDgQWBBQGzskMvcQJeOnd/RYSNHUF6IW0TzBIBgNVHSMEQTA/gBRG\nMGkqedeUoE469SYvm2pOHTgg0KEhpB8wHTEbMBkGA1UEAwwSVGVzdCBFZGdlIE93\nbmVyIENBggQyeyPGMA0GCSqGSIb3DQEBCwUAA4ICAQCduJxumU/BaJQ46m+vQarX\nDowO9pzXCxHFSG1ka7fCHphJYcw8bkcRM8sNMfZwctMYYnlIL9Ghod2FfSSoHmZx\nXOq6Tk+zgpqDZDp7ZDD9qFAI0/sI58QuAMx7NoGB8iqk4k5mYZirRlR24iy1iuky\najRsDCjgQiL/9xualdeJBvq4yooMbSBu4mIthoD+BxDwec4xh7BbYsy8+3qgculB\nE4YfAEhEYJGLSGaeU+iEHTgY7MO1GBvfMPN0BphkkR6995RSygzgbbN6lGPI3QKa\nnMWjLS5B1+7rtztJtC6DyeDA8eKQV8ZQ7roz5081UNaIV5sZUj1pHt48Z/WnO77b\niL1zPLpG7nMtOTtg/7m7ZZ74Sf0CeqGdx3XynfV5zzsvAaNDlKdPntNQrQxepPoc\nZDtl8hJwU5aZ7/fZiecvl+EtnnR906gdR1TIgG0a+To8zHCB16YaoP06BNDV1XDn\no09rP4jgOk4UbdGVX41tDmxOw6GRiHRHnIGtEVW+pn53ZUAQ+JBMlPLNYH8US26M\nfHzZ9X23ICqpGeG+Zd1VidH+TiD4PQwV4HUVL9xAlIkj/kD60XA5Haz6Vj14UlLa\nbMxabz+v0e4wPPJGGY3G3P8wCo0e8bFiL/RyoyrRWZCNDrC6N7GHrS4IfoFd3Bm5\nym6+PbkEtiBLlE8wRR1yJQ==\n-----END CERTIFICATE-----\n-----BEGIN CERTIFICATE-----\nMIIFSjCCAzKgAwIBAgIEMnsjxjANBgkqhkiG9w0BAQsFADAdMRswGQYDVQQDDBJU\nZXN0IEVkZ2UgT3duZXIgQ0EwHhcNMjAwNzA5MjAxMDAyWhcNMjAxMDA3MjAxMDAy\nWjAeMRwwGgYDVQQDDBNUZXN0IEVkZ2UgRGV2aWNlIENBMIICIjANBgkqhkiG9w0B\nAQEFAAOCAg8AMIICCgKCAgEAqV8ZfbUv5CeA6CEQzF0TCzLyr+6Qkz+TmtQ+m4dB\n9HRzcFUgLpo7Ag9JbkxhKk+z/9TIv/E8iMS9rqAawsq80/mcIkzS62sMci6NIcYt\nx7eAe6I/g05Q4tMilRmXbkWE/zLckjOG6j6exONnDPLpLINVfbA6zglZGjmgRgf7\nsUImpfO2ZModuYV0vKPvC90XkfMi6bGn/gZi3xDONdF+i+7lePd7PyI2zndIE23n\nx0lxZuO1cRmrJl+6EZgVa5m9afumD1kkbaKxbBYcde52vGU/VTe8cjdJxZZLYC6I\nxcBs5YbSDaVKMPxdDHLysicESjt+r98CN42udmh1KMeX9VnWdLImvDG4rTZbLs9z\np1g8Ev0VplqmUVpQmVHOgK5RrxYwfYLVzZBZE8tVIDIblkA27ifSQEkPnGsB8SoE\nyai0baP8IMQ6gWNfgy5LrUaueTeTk0vLVzOG37fs8/ZN2hzEV7Ji0lJS1cXwb1aG\nTcbOGQsO0Ndq+xLFoxd733GGxNmDt+QEt8RTFFk5ebripUZxSmhzlBs+lNUhz9yE\nB6HDRI8U0bd6OEHdlnWbEDGLNMexByfXSAqmIqWESBJRiGrsICLndZ+tfn/jbeIt\n7/B2zG1HpvxnLD8S4wjPs892F4R0S7eF+KhJ4rhrc7bqCVAEHMnBR6ZLpKt5sSrQ\n17cCAwEAAaOBkDCBjTASBgNVHRMBAf8ECDAGAQH/AgECMA4GA1UdDwEB/wQEAwIC\nhDAdBgNVHQ4EFgQURjBpKnnXlKBOOvUmL5tqTh04INAwSAYDVR0jBEEwP4AUx+Rg\npMwDygrHWuoSZ+D80QxPlVehIaQfMB0xGzAZBgNVBAMMElRlc3QgRWRnZSBPd25l\nciBDQYIEa4tFZzANBgkqhkiG9w0BAQsFAAOCAgEAYTTkoJwZoByKlIrdWt9sn1O4\n9CO+r/DUItSk46cwbZZb/VV1W3IYM8YPFo2vKiQjWwStP6kLdXafX/MBH2g9Z7Xz\ncv/fDTGUH2PpGxp85MVw87qESvb8pz67YWeEUt1z1RoL3JgkbrJOZIa64OSYShX7\nF3TFknz5Vn7Dv8YXncZNfFM+VAZasIXaNqLuqyLnWX9yTgLA/ULh7GJPBKqvu3gB\nIgfvumzo+BE1NNfMtl4UCztd1gOSMlJ0nNY4TskE7W08gCbdsyPOw56zz7cFI3xV\nN61jY1VB8QOfejwhBgl3zih6xfHb9dyfYX7iemPRwyJrFKMTEagYrcfKSiH4nIWh\ncgNVg1oECMqBVG75SNF4iEigB1OnZbE0iBaGXMvQoKVsS7y1rdbs7MmYCFgecOdZ\nRoIH+E7AN2BE9hkVmY4o0McSoypyVyAwbEUNhEq3FRGL0tAk6m3RC/MKxeth3UK1\nUdV5K0Mi9j8VVvShHNKMNYkhaKERhl6pCDQvqV0nheZS5ZF3tSq5FLEqvISmimDf\nFKqAPXuI0Q4EbrEs7XNau+p7RhFsMh78U3UtUzjWR475Ti2Yeh7+8D3dLnwtl1MH\npGVpcmVzD9snPZcTHDlJH8ZAVzNcua4KhRHldFLbL1L3X0KG02xEZxBbaQyyL6mv\nAej6YZlBIq1eGVQFeug=\n-----END CERTIFICATE-----\n-----BEGIN CERTIFICATE-----\nMIIFSTCCAzGgAwIBAgIEa4tFZzANBgkqhkiG9w0BAQsFADAdMRswGQYDVQQDDBJU\nZXN0IEVkZ2UgT3duZXIgQ0EwHhcNMjAwNzA5MjAxMDAyWhcNMjAxMDA3MjAxMDAy\nWjAdMRswGQYDVQQDDBJUZXN0IEVkZ2UgT3duZXIgQ0EwggIiMA0GCSqGSIb3DQEB\nAQUAA4ICDwAwggIKAoICAQDcT8+1lYPjrASsODI7f06dei8SsSjpEEWGd2LjXXt5\nsOJb0Q6TR7sB8kXyjc0K8wCeWsG+qLKcxr5gqzNP6BHTn3oKWGn+r/L9jM8vm3MJ\n/IQlF8aWPnRgYItGIfRM+1TCCGif26OLj7JLfQ/gDJ10ZanQRdeHN+jGP3YhqFht\nSqlxk5MYOH5S49ZDxEJ3D2EMjWnGwJVJYN/w22inubSgZ1uSb9fXIPHyTTub/QhC\nz72gJoeIxqVw5esd2zFqEOaXI6li3FB+pyAEJopSXxpLLz/uYKWmwXCYVNPnKeO/\nH71DzHG/RKDMTGCqjc0NKPiy3wwFoIyVBPfOa9AJ/2DdACjHphO1nav0aeq5Rkgn\ncffb+SkpxRnJGAnBpnNc01789G3aVnV3ALHiafpOEvQme1tADLpUHplrh7Obmood\nglDTuTQagVaBkyNHdFUIKEk31GUgllkkwBwADUN+b+bKj9hIVbWR7QQ19KUzQ9Gk\nKb7A3frkHXvVJvaVYR6AkhM7aLAWBPrzCWiOEHNsFZN5AGiWFq5N7UumGpxjmS5P\nMvDf1JpuntbqzwB2baQMGFHiVkb/PbtR7RZfD7JxX5yXKzfKiUEI72Vs6nk/gwax\ne/tL7B2o23/m/XGZh+kMozuAdMvy2r87VyA3RJUNmXJtV0JtT7outk2rQW0JW9cs\n5wIDAQABo4GQMIGNMBIGA1UdEwEB/wQIMAYBAf8CAQMwDgYDVR0PAQH/BAQDAgKE\nMB0GA1UdDgQWBBTH5GCkzAPKCsda6hJn4PzRDE+VVzBIBgNVHSMEQTA/gBTH5GCk\nzAPKCsda6hJn4PzRDE+VV6EhpB8wHTEbMBkGA1UEAwwSVGVzdCBFZGdlIE93bmVy\nIENBggRri0VnMA0GCSqGSIb3DQEBCwUAA4ICAQAGI4WZms/QCyxYPL7MJk6CMVnb\nwkm2dGIAoTsD8fZNrOsQC62Nyy932jGKB6dbNXAk+Jcq+0JIM0Ni3j7dFDosNavF\naC1Vo3cKCZKdiM2ETb7mpATtj2tX+jYYekOo2jIXshsYLhlpcUosZaOhx66M8njj\n+B5oEpa9Sm7rmV2qrlqLbIX/fz2rP9V7kpV3bggfphdkvZwcTU8+LVMcnA4U3rAe\nKbXqYtKSj5KlXpGSOydYRZ0rRA+Op9677EWcqTNI8aY5RihH2zsyejR/NrZaNEJz\nQ/lJn4ufU84Vai7wXP39BtWHzmxcoF4rxwnF9KqFACgSCh8u8r/AqbrendAbEWiY\n7hpPoHD6QIiLNuU8s4l4mB2NG/TQZxz8nvytYh9tRsRVrCG5HefoNnXDrnjjyPo2\ncArSi2IdAHGzdlZIddoJtJNTSuTxmBjDyAhUgi6fHTzXrZttz25xsW1HxvlEalB6\nk/Dr2ZGGmuRB44bbCJeldFahK/rQxpLQmrJgXhZ1ZmDnK3ZhAvlYT5o4H8S/kf/y\nQi24c03QkhWUi2lOfjv2obV89wgqZBgEmWNP/ClebKGq0ayGFSlBvicCn/lNj6Fq\nsU++iKo/mM3P6MAySIe3HaoMlntXyIin7/HEyvrXI6dqaiGeIgmpykcHfd85uZDT\n6dXExloxXMEiritdew==\n-----END CERTIFICATE-----\n",
                "expiration": expiration.to_rfc3339()
            }
        );

        let _m = mock(
            "POST",
            "/modules/$edgeHub/genid/12345678/certificate/server?api-version=2019-01-30",
        )
        .with_status(200)
        .with_body(serde_json::to_string(&res).unwrap())
        .create();

        env::set_var(WORKLOAD_URI, mockito::server_url());
        env::set_var(EDGE_DEVICE_HOST_NAME, "localhost");
        env::set_var(MODULE_ID, "$edgeHub");
        env::set_var(MODULE_GENERATION_ID, "12345678");

        let dir = tempfile::tempdir().unwrap();
        let path = dir.path().join("identity.pem");

        assert!(dowload_server_certificate(&path).await.is_ok());
        assert!(path.exists());
    }
}
