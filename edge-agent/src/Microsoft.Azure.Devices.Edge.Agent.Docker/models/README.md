This namespace defines a subset of the Docker API model types. These are used to parse the module `createOptions` in Edge Agent's twin.

Previously Edge Agent used to use `Docker.DotNet` types for this. However the project has not been updated in a long time and is missing several new properties that have been added since then. Since Edge Agent deserializes and reserializes the `createOptions` (since it needs to modify it to add env vars, etc), it would lose these properties even if the user added them to the `createOptions`.

So this namespace's approach is to leave the `createOptions` as an untyped JSON object. However we do want to have some type-safety for the modifications that Edge Agent needs to make, so those properties are promoted to real fields. The other properties are automatically added to a weakly-typed `IDictionary<string, JToken>` field annotated with `Newtonsoft.Json.JsonExtensionData`

Ref: https://www.newtonsoft.com/json/help/html/SerializationAttributes.htm#JsonExtensionDataAttribute

If you need to access a property that isn't already defined as a field, promote it to a field.

If you need to add a type, make sure it has the `OtherProperties` field.
