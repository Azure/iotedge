use policy::{
    Decision, DefaultResourceMatcher, DefaultSubstituter, DefaultValidator, PolicyBuilder, Request,
    Result,
};

fn main() -> Result<()> {
    let json = r#"{
        "schemaVersion": "2020-10-30",
        "statements": [
            {
                "effect": "allow",
                "identities": [
                    "actor_a"
                ],
                "operations": [
                    "write"
                ],
                "resources": [
                    "resource_1"
                ]
            }
        ]
    }"#;

    let policy = PolicyBuilder::from_json(json)
        .with_validator(DefaultValidator)
        .with_matcher(DefaultResourceMatcher)
        .with_substituter(DefaultSubstituter)
        .with_default_decision(Decision::Denied)
        .build()?;

    let request = Request::new("actor_a", "write", "resource_1")?;

    let result = policy.evaluate(&request)?;
    println!("Result of policy evaluation: {:?}", result);

    Ok(())
}
