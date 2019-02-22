Code Style
==========

C# Code Style Guidelines
------------------------

C# is a very broad, multi-paradigm language. It's done a good job over the years of evolving with the changing best practices in language design. In order to build a system with such a broad language, we need to pare down the set of features and style that we will use. These "rules" have helped in the past to write "correct" code, that is easier to test, extend, and debug.

### Baseline

Our baseline C# Coding Style is defined [here](https://github.com/Azure/DotNetty/wiki/C#-Coding-Style). These guidelines are used by the broader IoT Hub team.  In order to enforce coding style, StyleCop (NuGet version) is used.  StyleCop will be run automatically during build except Debug configuration.  Some StyleCop rules are not applicable to IoT Edge, you can find out from [here](https://github.com/Azure/iotedge/blob/master/stylecop.ruleset). 

### Resharper

These style guidelines have been defined in a set of Resharper rules. Resharper is a static analysis tool for C# from JetBrains. We have a license server at `http://resharper/`. Use of Resharper is highly recommended, as it improves the C# development process and helps find a large set of code defects. If you've never used it before, you will soon realize you can't live without it.

#### Tips
- With Resharper default settings:
  - To format code, use "Ctrl + Alt + Enter".
  - To slient cleanup code, use "Ctrl + E, Ctrl + F", which will run default cleanup profile "Azure.IoT.Edge".
  - Cleanup code will run "format code" plus some optimization e.g. remove unused using statements.
- NOTE: remember Resharper code formatting doesn't resolve all StyleCop issues; therefore please make sure to build solution in Release configuration to ensure no styling issue found.

### Immutable Types

We should strive to use immutable types/classes wherever possible, which implies the following:

- Members should be marked as readonly (or only define a getter) as much as possible. This sometimes requires reevaluating the design to make this happen. 
- Use of property setters (even private setters) is discouraged. Properties should be bound in the constructor or derived from other property data. This is made much cleaner with [C# 6.0 expression body function members](http://www.c-sharpcorner.com/UploadFile/66489a/expression-bodied-members-in-C-Sharp-6-0/).

Favoring immutability has many advantages. Because variables can only be bound once and in one spot in the code, it is much easier to debug and diagnose issues, especially in a multi-threaded environment. This vastly reduces the possible things that could have gone wrong.

The main disadvantage is that immutable classes can generate a lot of garbage. If there is a performance issue found, the use of mutability is an acceptable solution. 

> ***NOTE:** this style of programming was supposed to gain much more support in C#7 with [Record Types](https://github.com/dotnet/roslyn/blob/features/records/docs/features/records.md). Alas, it was pushed to C#8.*

### Use of `null`

Simply put, don't use it. The use of null is highly discouraged, particularly in a public API. The reason is simple, null is a fundamentally broken concept in object oriented languages. Take it from the inventor, [Tony Hoare, as he discusses the null pointer](https://en.wikipedia.org/wiki/Null_pointer#History).

#### `NullReferenceException` (NRE)
In native code, there is great care taken to not use a null reference, because this results in a segmentation fault and process crash. In C# a null reference results in an NRE, which does not crash the process. However, it is unacceptable to see an NRE in C# code, and should be treated as a crash.

The easiest way to not get NREs is to not use `null`. This works well together with immutable types. Since, a variable can only be bound once, it doesn't make sense to use `null` in most cases.

#### `Option` type

In many cases, `null` is used to represent an optional value, particularly in public APIs. The problem with this approach is that there is no way to determine, or specify, via the type signature of a function or method that the return value is optional. In these cases we should use the [`Option`](https://en.wikipedia.org/wiki/Option_type) type to handle cases where a value may be optional. This makes optional return values explicit and aides the consumer of the API in dealing with the fact that the result of a function may not have a value.

> ***NOTE:** We have brought over an implementation of the `Option` type from the IoT Hub project, which is modeled after Scala's option type.*

### Pure Functions

Use of pure functions are encouraged. Meaning, all state required for a function should be passed in as arguments and functions should be idempotent. This makes functions easier to reason about and test. Obviously, this is not possible everywhere, because the software needs to get work done and interact with the outside world. However, having a clear separation between the "pure" business logic and interaction with the environment makes code generally easier to understand and test.

### Result

The sections above all work together and lead to the following results:

- Most null checking can be removed. If all types are immutable, and null is not used, then we can confidently access the value of property or result of a function. This removes a lot of cognitive load.
- This implies that `null` must not get into the system. In order to achieve this, all constructors should check arguments for `null` and throw `ArgumentNullException`. Due to the fact all types are immutable and we aren't using `null` in the public API, we don't need any further `null` checks. A set of `Preconditions` helpers are included in the core utilities to make these checks easy.
- Truly optional return values are represented using the `Option` type, which forces the caller to handle the case where there is no value returned.

### Summary

The goal is to allow for a simpler mental model of the software, reducing cognitive load while extending or debugging. It also helps to write multithreaded code (immutable types can't be modified by two threads by definition).
