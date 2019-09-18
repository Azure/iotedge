use www_authenticate::*;

mod common;

single_challenge_valid!(smoke, 
    "Bearer realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\",scope=\"repository:library/ubuntu:pull\"", 
    ChallengeScheme::Bearer,
    string_map!{
        "realm" => "https://auth.docker.io/token",
        "service" => "registry.docker.io",
        "scope" => "repository:library/ubuntu:pull"
    }
);

invalid!(close_but_invalid, "close Bearer realm=\"https://auth.docker.io/token\"", WWWAuthenticateError::Pest(_));

multi_challenge_valid!(complex_smoke,
    r#" Digest realm="htt\"p\"-auth@example.org", qop="auth, auth-int",
 algorithm=MD5,
 nonce="7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v",
 opaque="FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS",
 Basic realm="example.com""#,
    {
        ChallengeScheme::Digest,
        string_map!{
            "realm" => "htt\\\"p\\\"-auth@example.org",
            "qop" => "auth, auth-int",
            "algorithm" => "MD5",
            "nonce" => "7ypf/xlj9XXwfDPEoM4URrv/xwf94BcCAzFZH4GiTo0v",
            "opaque" => "FQhe/qaU925kfnzjCev0ciny7QMkPqMAFRtzCUYo5tdS"
        }
    },
    {
        ChallengeScheme::Basic,
        string_map! { "realm" => "example.com" }
    }
);
