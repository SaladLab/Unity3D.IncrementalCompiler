@echo off

pushd %~dp0

tools\nuget\NuGet.exe update -self
tools\nuget\NuGet.exe install FAKE -ConfigFile tools\nuget\Nuget.Config -OutputDirectory packages -ExcludeVersion -Version 4.22.8
tools\nuget\NuGet.exe install ILRepack -ConfigFile tools\nuget\Nuget.Config -OutputDirectory packages -ExcludeVersion -Version 2.0.10

set encoding=utf-8
packages\FAKE\tools\FAKE.exe build.fsx %*

popd
