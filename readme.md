# Marshalling with State

A repository to explore marshaller state options with the `LibraryImport` source generator in dotnet/runtime.

## Requirements

- .NET 6+
- BenchmarkDotNet 0.13.1

## Run

Validating the marshallers is done by running the application with no arguments.

`dotnet run`

The BenchmarkDotNet tests can be run by passing arguments to the application. Passing flags that tell BenchmarkDotNet to run all tests is the easiest way to run the tests.

`dotnet run -c Release -- -f *`
