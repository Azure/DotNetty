#I @"packages/FAKE/tools"
#r "FakeLib.dll"

open System
open System.IO
open Fake
open Fake.FileUtils
open Fake.TaskRunnerHelper
open Fake.StrongNamingHelper
open Fake.Testing.XUnit2

//--------------------------------------------------------------------------------
// Information about the project for Nuget and Assembly info files
//-------------------------------------------------------------------------------

let product = "DotNetty"
let authors = [ "Microsoft Azure" ]
let copyright = "Copyright © 2016"
let company = "DotNetty"
let description = "High performance, reactive TCP / UDP socket middleware for .NET"
let tags = ["socket";"sockets";"UDP";"TCP";"Netty";"DotNetty"]
let configuration = "Release"

// Read release notes and version
let parsedRelease =
    File.ReadLines "RELEASE_NOTES.md"
    |> ReleaseNotesHelper.parseReleaseNotes

let envBuildNumber = System.Environment.GetEnvironmentVariable("BUILD_NUMBER") //populated by TeamCity build agent
let buildNumber = if String.IsNullOrWhiteSpace(envBuildNumber) then "0" else envBuildNumber

let version = parsedRelease.AssemblyVersion + "." + buildNumber
let preReleaseVersion = version + "-beta" //suffixes the assembly for pre-releases

let isUnstableDocs = hasBuildParam "unstable"
let isPreRelease = hasBuildParam "nugetprerelease"
let release = if isPreRelease then ReleaseNotesHelper.ReleaseNotes.New(version, version + "-beta", parsedRelease.Notes) else parsedRelease


//--------------------------------------------------------------------------------
// Directories

let binDir = "bin"
let testOutput = "TestResults"

let nugetDir = binDir @@ "nuget"
let workingDir = binDir @@ "build"
let nugetExe = FullName @".nuget\NuGet.exe"

open Fake.RestorePackageHelper
Target "RestorePackages" (fun _ -> 
     "./DotNetty.sln"
     |> RestoreMSSolutionPackages (fun p ->
         { p with
             OutputPath = "./packages"
             Retries = 4 })
 )

//--------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    DeleteDir binDir
)

//--------------------------------------------------------------------------------
// Generate AssemblyInfo files with the version for release notes 


open AssemblyInfoFile

Target "AssemblyInfo" (fun _ ->
    let version = release.AssemblyVersion

    let signKey = getBuildParamOrDefault "signKey" ""
    let delaySign =
        match signKey with
            | s as string when s.Length > 0 -> Some(true)
            | _ -> None
    
    CreateCSharpAssemblyInfoWithConfig "src/SharedAssemblyInfo.cs" [
        Attribute.Company company
        Attribute.Copyright copyright
        Attribute.KeyFile signKey
        Attribute.DelaySign delaySign
        Attribute.Version version
        Attribute.FileVersion version ] <| AssemblyInfoFileConfig(false)
)

//--------------------------------------------------------------------------------
// Build the solution

Target "Build" (fun _ ->
    !!"DotNetty.sln"
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

//--------------------------------------------------------------------------------
// Build the solution

Target "BuildSignedConfig" (fun _ ->
    !!"DotNetty.sln"
    |> MSBuild "" "Rebuild" ["Configuration", "Signed"]
    |> ignore
)

//--------------------------------------------------------------------------------
// Copy the build output to bin directory
//--------------------------------------------------------------------------------

Target "CopyOutput" (fun _ ->    
    let copyOutput project =
        let src = "src" @@ project @@ "bin" @@ "Release" 
        let dst = binDir @@ project
        CopyDir dst src allFiles
    [ "DotNetty.Buffers"
      "DotNetty.Common"
      "DotNetty.Transport"
      "DotNetty.Codecs"
      "DotNetty.Handlers"
      "DotNetty.Codecs.Mqtt"
    ]
    |> List.iter copyOutput
)

//--------------------------------------------------------------------------------
// Copy the build output to bin directory
//--------------------------------------------------------------------------------

Target "ResignAssemblies" (fun _ ->    
    let reSignKey = getBuildParamOrDefault "reSignKey" ""
    let copyOutput project =
        let src = "src" @@ project @@ "bin" @@ "Release" @@ project + ".dll"
        StrongName (fun x -> x) ("-Ra " + src + " " + reSignKey)
    [ "DotNetty.Buffers"
      "DotNetty.Common"
      "DotNetty.Transport"
      "DotNetty.Codecs"
      "DotNetty.Handlers"
      "DotNetty.Codecs.Mqtt"
    ]
    |> List.iter copyOutput
)

Target "BuildRelease" DoNothing
Target "BuildReleaseMono" DoNothing
Target "BuildSigned" DoNothing

//--------------------------------------------------------------------------------
// Tests targets
//--------------------------------------------------------------------------------

Target "RunTests" <| fun _ ->
    let xunitTestAssemblies = !! "test/**/bin/Release/*.Tests.dll" ++ "test/**/bin/Release/*.Tests.End2End.dll"

    mkdir testOutput
    let xunitToolPath = findToolInSubPath "xunit.console.exe" "packages/xunit.runner.console*/tools"
    printfn "Using XUnit runner: %s" xunitToolPath
    xUnit2
        (fun p -> { p with XmlOutputPath = Some (testOutput + "/report.xml"); ToolPath = xunitToolPath })
        xunitTestAssemblies

//--------------------------------------------------------------------------------
// Clean test output

Target "CleanTests" <| fun _ ->
    DeleteDir testOutput

//--------------------------------------------------------------------------------
// Nuget targets 
//--------------------------------------------------------------------------------
module Nuget = 
     // add DotNetty dependency for other projects
    let getDependencies project =
        match project with
        | "DotNetty.Common" -> []
        | "DotNetty.Transport" -> ["DotNetty.Common", release.NugetVersion] @ ["DotNetty.Buffers", release.NugetVersion]
        | "DotNetty.Codecs" -> ["DotNetty.Common", release.NugetVersion] @ ["DotNetty.Buffers", release.NugetVersion] @ ["DotNetty.Transport", release.NugetVersion]
        | "DotNetty.Handlers" -> ["DotNetty.Common", release.NugetVersion] @ ["DotNetty.Buffers", release.NugetVersion] @ ["DotNetty.Transport", release.NugetVersion] @ ["DotNetty.Codecs", release.NugetVersion]
        | codecs when (codecs.StartsWith("DotNetty.Codecs.")) -> ["DotNetty.Common", release.NugetVersion] @ ["DotNetty.Buffers", release.NugetVersion] @ ["DotNetty.Transport", release.NugetVersion] @ ["DotNetty.Codecs", release.NugetVersion]
        | _ -> ["DotNetty.Common", release.NugetVersion]

     // used to add -pre suffix to pre-release packages
    let getProjectVersion project =
      match project with
      | _ -> release.NugetVersion

open Nuget

//--------------------------------------------------------------------------------
// Clean nuget directory
Target "CleanNuget" (fun _ ->
    CleanDir nugetDir
)

//--------------------------------------------------------------------------------
// Pack nuget for all projects
// Publish to nuget.org if nugetkey is specified

let createNugetPackages _ =
    let nugetSuffix = getBuildParamOrDefault "nugetSuffix" ""
    let mutable dirName = 1
    let removeDir dir = 
        let del _ = 
            DeleteDir dir
            not (directoryExists dir)
        runWithRetries del 3 |> ignore

    let getDirName workingDir dirCount =
        workingDir + dirCount.ToString()

    CleanDir workingDir

    ensureDirectory nugetDir
    for nuspec in !! "src/**/*.nuspec" do
        printfn "Creating nuget packages for %s" nuspec
        
        let project = Path.GetFileNameWithoutExtension nuspec 
        let projectDir = Path.GetDirectoryName nuspec
        let projectFile = (!! (projectDir @@ project + ".*sproj")) |> Seq.head
        let releaseDir = projectDir @@ @"bin\Release"
        let packages = projectDir @@ "packages.config"
        let packageDependencies = if (fileExists packages) then (getDependencies packages) else []
        let dependencies = packageDependencies @ (getDependencies project |> List.map (fun x -> fst x + nugetSuffix, snd x))
        let releaseVersion = getProjectVersion project

        let pack outputDir symbolPackage =
            NuGetHelper.NuGet
                (fun p ->
                    { p with
                        Description = description
                        Authors = authors
                        Copyright = copyright
                        Project =  project + nugetSuffix
                        Properties = ["Configuration", "Release"]
                        ReleaseNotes = release.Notes |> String.concat "\n"
                        Version = releaseVersion
                        Tags = tags |> String.concat " "
                        OutputPath = outputDir
                        WorkingDir = workingDir
                        SymbolPackage = symbolPackage
                        Dependencies = dependencies })
                nuspec

        // Copy dll, pdb and xml to libdir = workingDir/lib/net45/
        let libDir = workingDir @@ @"lib\net45"
        printfn "Creating output directory %s" libDir
        ensureDirectory libDir
        CleanDir libDir
        !! (releaseDir @@ project + ".dll")
        ++ (releaseDir @@ project + ".pdb")
        ++ (releaseDir @@ project + ".xml")
        |> CopyFiles libDir

        // Copy all src-files (.cs and .fs files) to workingDir/src
        let nugetSrcDir = workingDir @@ @"src/"
        CleanDir nugetSrcDir

        let isCs = hasExt ".cs"
        let isFs = hasExt ".fs"
        let isAssemblyInfo f = (filename f).Contains("AssemblyInfo")
        let isSrc f = (isCs f || isFs f) && not (isAssemblyInfo f) 
        CopyDir nugetSrcDir projectDir isSrc
        
        //Remove workingDir/src/obj and workingDir/src/bin
        removeDir (nugetSrcDir @@ "obj")
        removeDir (nugetSrcDir @@ "bin")

        // Create both normal nuget package and symbols nuget package. 
        // Uses the files we copied to workingDir and outputs to nugetdir
        pack nugetDir NugetSymbolPackage.Nuspec


let publishNugetPackages _ = 
    let rec publishPackage url accessKey trialsLeft packageFile =
        let tracing = enableProcessTracing
        enableProcessTracing <- false
        let args p =
            match p with
            | (pack, key, "") -> sprintf "push \"%s\" %s" pack key
            | (pack, key, url) -> sprintf "push \"%s\" %s -source %s" pack key url

        tracefn "Pushing %s Attempts left: %d" (FullName packageFile) trialsLeft
        try 
            let result = ExecProcess (fun info -> 
                    info.FileName <- nugetExe
                    info.WorkingDirectory <- (Path.GetDirectoryName (FullName packageFile))
                    info.Arguments <- args (packageFile, accessKey,url)) (System.TimeSpan.FromMinutes 1.0)
            enableProcessTracing <- tracing
            if result <> 0 then failwithf "Error during NuGet symbol push. %s %s" nugetExe (args (packageFile, "key omitted",url))
        with exn -> 
            if (trialsLeft > 0) then (publishPackage url accessKey (trialsLeft-1) packageFile)
            else raise exn
    let shouldPushNugetPackages = hasBuildParam "nugetkey"
    let shouldPushSymbolsPackages = (hasBuildParam "symbolspublishurl") && (hasBuildParam "symbolskey")
    
    if (shouldPushNugetPackages || shouldPushSymbolsPackages) then
        printfn "Pushing nuget packages"
        if shouldPushNugetPackages then
            let normalPackages= 
                !! (nugetDir @@ "*.nupkg") 
                -- (nugetDir @@ "*.symbols.nupkg") |> Seq.sortBy(fun x -> x.ToLower())
            for package in normalPackages do
                publishPackage (getBuildParamOrDefault "nugetpublishurl" "") (getBuildParam "nugetkey") 3 package

        if shouldPushSymbolsPackages then
            let symbolPackages= !! (nugetDir @@ "*.symbols.nupkg") |> Seq.sortBy(fun x -> x.ToLower())
            for package in symbolPackages do
                publishPackage (getBuildParam "symbolspublishurl") (getBuildParam "symbolskey") 3 package

Target "Nuget" <| fun _ -> 
    createNugetPackages()
    publishNugetPackages()

Target "CreateNuget" <| fun _ -> 
    createNugetPackages()

Target "PublishNuget" <| fun _ -> 
    publishNugetPackages()

Target "NugetSigned" <| fun _ -> 
    createNugetPackages()
    publishNugetPackages()

//--------------------------------------------------------------------------------
// Help 
//--------------------------------------------------------------------------------

Target "Help" <| fun _ ->
    List.iter printfn [
      "usage:"
      "build [target]"
      ""
      " Targets for building:"
      " * Build      Builds"
      " * Nuget      Create and optionally publish nugets packages"
      " * RunTests   Runs tests"
      " * All        Builds, run tests, creates and optionally publish nuget packages"
      ""
      " Other Targets"
      " * Help       Display this help" 
      " * HelpNuget  Display help about creating and pushing nuget packages" 
      ""]

Target "HelpNuget" <| fun _ ->
    List.iter printfn [
      "usage: "
      "build Nuget [nugetkey=<key> [nugetpublishurl=<url>]] "
      "            [symbolskey=<key> symbolspublishurl=<url>] "
      "            [nugetprerelease=<prefix>]"
      ""
      "Arguments for Nuget target:"
      "   nugetprerelease=<prefix>   Creates a pre-release package."
      "                              The version will be version-prefix<date>"
      "                              Example: nugetprerelease=dev =>"
      "                                       0.6.3-dev1408191917"
      ""
      "In order to publish a nuget package, keys must be specified."
      "If a key is not specified the nuget packages will only be created on disk"
      "After a build you can find them in bin/nuget"
      ""
      "For pushing nuget packages to nuget.org and symbols to symbolsource.org"
      "you need to specify nugetkey=<key>"
      "   build Nuget nugetKey=<key for nuget.org>"
      ""
      "For pushing the ordinary nuget packages to another place than nuget.org specify the url"
      "  nugetkey=<key>  nugetpublishurl=<url>  "
      ""
      "For pushing symbols packages specify:"
      "  symbolskey=<key>  symbolspublishurl=<url> "
      ""
      "Examples:"
      "  build Nuget                      Build nuget packages to the bin/nuget folder"
      ""
      "  build Nuget nugetprerelease=dev  Build pre-release nuget packages"
      ""
      "  build Nuget nugetkey=123         Build and publish to nuget.org and symbolsource.org"
      ""
      "  build Nuget nugetprerelease=dev nugetkey=123 nugetpublishurl=http://abc"
      "              symbolskey=456 symbolspublishurl=http://xyz"
      "                                   Build and publish pre-release nuget packages to http://abc"
      "                                   and symbols packages to http://xyz"
      ""]


//--------------------------------------------------------------------------------
//  Target dependencies
//--------------------------------------------------------------------------------

Target "Mono" DoNothing
Target "All" DoNothing

// build dependencies
"Clean" ==> "AssemblyInfo" ==> "RestorePackages" ==> "Build" ==> "CopyOutput" ==> "BuildRelease"

// build dependencies
"Clean" ==> "AssemblyInfo" ==> "RestorePackages" ==> "BuildSignedConfig" ==> "ResignAssemblies" ==> "BuildSigned"

// tests dependencies
"CleanTests" ==> "RunTests"

// nuget dependencies
"CleanNuget" ==> "BuildRelease" ==> "Nuget"
"CleanNuget" ==> "BuildSigned" ==> "NugetSigned"

"BuildRelease" ==> "All"
"RunTests" ==> "All"
"Nuget" ==> "All"

Target "AllTests" DoNothing //used for Mono builds, due to Mono 4.0 bug with FAKE / NuGet https://github.com/fsharp/fsharp/issues/427
"BuildRelease" ==> "AllTests"
"RunTests" ==> "AllTests"

RunTargetOrDefault "Help"