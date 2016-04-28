@echo off

pushd %~dp0

SETLOCAL
SET CACHED_NUGET=%LocalAppData%\NuGet\NuGet.exe

IF EXIST %CACHED_NUGET% goto copynuget
echo Downloading latest version of NuGet.exe...
IF NOT EXIST %LocalAppData%\NuGet md %LocalAppData%\NuGet
@powershell -NoProfile -ExecutionPolicy unrestricted -Command "$ProgressPreference = 'SilentlyContinue'; Invoke-WebRequest 'https://www.nuget.org/nuget.exe' -OutFile '%CACHED_NUGET%'"

:copynuget
IF EXIST .nuget\nuget.exe goto restore
md .nuget
copy %CACHED_NUGET% .nuget\nuget.exe > nul

:restore

.nuget\NuGet.exe update -self

.nuget\NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion -Version 4.25.4

.nuget\NuGet.exe install xunit.runner.console -OutputDirectory packages\FAKE -ExcludeVersion -Version 2.1.0

if not exist packages\SourceLink.Fake\tools\SourceLink.fsx ( 
  .nuget\nuget.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
)
rem cls

set encoding=utf-8
packages\FAKE\tools\FAKE.exe build.fsx %*

popd


