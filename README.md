# Unity3D.IncrementalCompiler

Unity3D Incremental C# Compiler using [Roslyn](https://github.com/dotnet/roslyn).
- You can get faster compilation speed. Because it works as an
  [incremental compiler](https://en.wikipedia.org/wiki/Incremental_compiler)
- You can use C# 5 and 6 features.

This project is at an early development stage.
And still now it can support only windows platform.

### Setup

Unzip a release zip file to your Unity project top-most directory.
- When Unity 4.x, use IncrementalCompiler.Unity4.zip
- When Unity 5.x, use IncrementalCompiler.Unity5.zip

### Benchmark

Brief [Benchmark](./docs/Benchmark.md)

### Related works

An integration part between unity3d and incremental compiler is based on
[alexzzzz](https://bitbucket.org/alexzzzz/unity-c-5.0-and-6.0-integration/src)'s works.
