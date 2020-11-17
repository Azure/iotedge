use policy::{Decision, Effect, PolicyBuilder, PolicyDefinition, Request, Statement};
use proptest::{collection::vec, num, prelude::*};

proptest! {
    #[test]
    fn policy_builder_does_not_crash(definition in arb_policy_definition()){
        let statement = &definition.statements()[0];
        let request = Request::new(
            &statement.identities()[0],
            &statement.operations()[0],
            &statement.resources()[0],
        ).unwrap();
        let expected = match statement.effect() {
            Effect::Allow => Decision::Allowed,
            Effect::Deny => Decision::Denied
        };

        let policy = PolicyBuilder::from_definition(definition)
            .build()
            .unwrap();

        assert_eq!(policy.evaluate(&request).unwrap(), expected);
    }
}

prop_compose! {
    pub fn arb_policy_definition()(
        statements in vec(arb_statement(), 1..5)
    ) -> PolicyDefinition {
        PolicyDefinition {
            statements
        }
    }
}

prop_compose! {
    pub fn arb_statement()(
        order in  num::usize::ANY,
        description in arb_description(),
        effect in arb_effect(),
        identities in vec(arb_identity(), 1..5),
        operations in vec(arb_operation(), 1..5),
        resources in vec(arb_resource(), 1..5),
    ) -> Statement {
        Statement{
            order,
            description,
            effect,
            identities,
            operations,
            resources,
        }
    }
}

pub fn arb_effect() -> impl Strategy<Value = Effect> {
    prop_oneof![Just(Effect::Allow), Just(Effect::Deny)]
}

pub fn arb_description() -> impl Strategy<Value = String> {
    "\\PC+"
}

pub fn arb_identity() -> impl Strategy<Value = String> {
    "[a-zA-Z0-9_()!@%,'=\\*\\$\\?\\-\\{\\}]{1,23}"
}

pub fn arb_operation() -> impl Strategy<Value = String> {
    "\\PC+"
}

pub fn arb_resource() -> impl Strategy<Value = String> {
    "\\PC+(/\\PC+)*"
}
