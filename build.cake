// Usings
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

// Arguments
var target = Argument<string>("target", "Default");
var source = Argument<string>("source", null);
var apiKey = Argument<string>("apikey", null);
var releaseNote = ParseReleaseNotes("./RELEASE_NOTES.md");
var buildNumber = EnvironmentVariable("BUILD_NUMBER") ?? "0";
var version = Argument<string>("targetversion", $"{releaseNote.Version}.{buildNumber}-beta");
var skipClean = Argument<bool>("skipclean", false);
var skipTests = Argument<bool>("skiptests", false);
var nogit = Argument<bool>("nogit", false);

// Variables
var configuration = IsRunningOnWindows() ? "Release" : "MonoRelease";
var csProjectFiles = GetFiles("./src/**/*.csproj");

// Directories
var nuget = Directory(".nuget");
var output = Directory("build");
var outputBinaries = output + Directory("binaries");
var outputBinariesNet45 = outputBinaries + Directory("net45");
var outputBinariesNetstandard = outputBinaries + Directory("netstandard1.3");
var outputPackages = output + Directory("packages");
var outputNuGet = output + Directory("nuget");
var outputPerfResults = Directory("perfResults");

///////////////////////////////////////////////////////////////

Task("Clean")
  .Does(() =>
{
  // Clean artifact directories.
  CleanDirectories(new DirectoryPath[] {
    output, outputBinaries, outputPackages, outputNuGet,
    outputBinariesNet45, outputBinariesNetstandard
  });

  if(!skipClean) {
    // Clean output directories.
    CleanDirectories("./**/bin/" + configuration);
    CleanDirectories("./**/obj/" + configuration);
  }
});

Task("Restore-NuGet-Packages")
  .Description("Restores dependencies")
  .Does(() =>
{
  DotNetCoreRestore();
  
  int result = StartProcess("dotnet", new ProcessSettings { Arguments = "restore -r win-x64" } );
  if (result != 0)
  {
    throw new CakeException($"Restore failed.");
  }
});

Task("Compile")
  .Description("Builds the solution")
  .IsDependentOn("Clean")
  .IsDependentOn("Restore-NuGet-Packages")
  .Does(() =>
{

  int result = StartProcess("dotnet", new ProcessSettings { Arguments = "msbuild dotnetty.sln /p:Configuration=" + configuration } );
  if (result != 0)
  {
    throw new CakeException($"Compilation failed.");
  }
});

Task("Test")
  .Description("Executes xUnit tests")
  .WithCriteria(!skipTests)
  .IsDependentOn("Compile")
  .Does(() =>
{
  var projects = GetFiles("./test/**/*.csproj")
    - GetFiles("./test/**/*.Performance.csproj");

  foreach(var project in projects)
  {
      DotNetCoreTest(project.FullPath, new DotNetCoreTestSettings
        {
          Configuration = configuration//,
          //Verbose = false
        });
      
    // if (IsRunningOnWindows())
    // {
    //   DotNetCoreTest(project.GetDirectory().FullPath, new DotNetCoreTestSettings {
    //     Configuration = configuration
    //   });
    // }
    // else
    // {
    //   // For when test projects are set to run against netstandard

    //   // DotNetCoreTest(project.GetDirectory().FullPath, new DotNetCoreTestSettings {
    //   //   Configuration = configuration,
    //   //   Framework = "netstandard1.3",
    //   //   Runtime = "unix-64"
    //   // });

    //   var dirPath = project.GetDirectory().FullPath;
    //   var testFile = project.GetFilenameWithoutExtension();

    //   using(var process = StartAndReturnProcess("mono", new ProcessSettings{Arguments =
    //     dirPath + "/bin/" + configuration + "/net451/unix-x64/dotnet-test-xunit.exe" + " " +
    //     dirPath + "/bin/" + configuration + "/net451/unix-x64/" + testFile + ".dll"}))
    //   {
    //     process.WaitForExit();
    //     if (process.GetExitCode() != 0)
    //     {
    //       throw new Exception("Mono tests failed");
    //     }
    //   }
    // }
  }
});

Task("Package-NuGet")
  .Description("Generates NuGet packages for each project that contains a nuspec")
  .Does(() =>
{
  var settings = new DotNetCorePackSettings {
    Configuration = configuration,
    OutputDirectory = outputNuGet,
    ArgumentCustomization = args => args.Append("--include-symbols").Append("-s").Append("--no-build")
  };

  foreach(var project in csProjectFiles)
  {
    DotNetCorePack(project.GetDirectory().FullPath, settings);
  }

});

Task("Publish-NuGet")
  .Description("Pushes the nuget packages in the nuget folder to a NuGet source. Also publishes the packages into the feeds.")
  .Does(() =>
{
  // Make sure we have an API key.
  if(string.IsNullOrWhiteSpace(apiKey)){
    throw new CakeException("No NuGet API key provided.");
  }

  // Upload every package to the provided NuGet source (defaults to nuget.org).
  var packages = GetFiles(outputNuGet.Path.FullPath + "/*" + version + ".nupkg");
  foreach(var package in packages)
  {
    NuGetPush(package, new NuGetPushSettings {
      ToolPath = ".nuget/nuget.exe",
      Source = source,
      ApiKey = apiKey,
      Verbosity = NuGetVerbosity.Detailed
    });
  }
});

///////////////////////////////////////////////////////////////

Task("Benchmark")
  .Description("Runs benchmarks")
  .IsDependentOn("Compile")
  .Does(() =>
{
  StartProcess(nuget.ToString() + "/nuget.exe", "install NBench.Runner -OutputDirectory tools -ExcludeVersion -Version 1.0.0");

  var libraries = GetFiles("./test/**/bin/" + configuration + "/net452/*.Performance.dll");
  CreateDirectory(outputPerfResults);

  foreach (var lib in libraries)
  {
    Information("Using NBench.Runner: {0}", lib);

    CopyFiles("./tools/NBench.Runner*/**/NBench.Runner.exe", lib.GetDirectory(), false);
    
    var nbenchArgs = new StringBuilder()
      .Append(" " + lib)
      .Append($" output-directory=\"{outputPerfResults}\"")
      .Append(" concurrent=\"true\"");
    
    int result = StartProcess(lib.GetDirectory().FullPath + "\\NBench.Runner.exe", new ProcessSettings { Arguments = nbenchArgs.ToString(), WorkingDirectory = lib.GetDirectory() });
    if (result != 0)
    {
      throw new CakeException($"NBench.Runner failed. {nbenchArgs}");
    }
  }
});

///////////////////////////////////////////////////////////////

Task("Tag")
  .Description("Tags the current release.")
  .Does(() =>
{
  StartProcess("git", new ProcessSettings {
    Arguments = string.Format("tag \"v{0}\"", version)
  });
});

Task("Prepare-Release")
  .Does(() =>
{
  // Update version.
  UpdateCsProjectVersion(version, csProjectFiles);

    // Add
    foreach (var file in csProjectFiles)
    {
      if (nogit)
      {
        Information("git " + string.Format("add {0}", file.FullPath));
      }
      else
      {
        StartProcess("git", new ProcessSettings {
          Arguments = string.Format("add {0}", file.FullPath)
        });
      }
    }

    // Commit
    if (nogit)
    {
      Information("git " + string.Format("commit -m \"Updated version to {0}\"", version));
    }
    else
    {
      StartProcess("git", new ProcessSettings {
        Arguments = string.Format("commit -m \"Updated version to {0}\"", version)
      });
    }

    // Tag
    if (nogit)
    {
      Information("git " + string.Format("tag \"v{0}\"", version));
    }
    else
    {
      StartProcess("git", new ProcessSettings {
        Arguments = string.Format("tag \"v{0}\"", version)
      });
    }

    //Push
    if (nogit)
    {
      Information("git push origin master");
      Information("git push --tags");
    }
    else
    {
      StartProcess("git", new ProcessSettings {
        Arguments = "push origin master"
      });

      StartProcess("git", new ProcessSettings {
        Arguments = "push --tags"
      });
    }
});

Task("Update-Version")
  .Does(() =>
{
  if(string.IsNullOrWhiteSpace(version)) {
    throw new CakeException("No version specified!");
  }

  CreateAssemblyInfo("src/shared/SharedAssemblyInfo.cs", new AssemblyInfoSettings {
      Product = "DotNetty",
      Company = "Microsoft",
      Version = version,
      FileVersion = version,
      Copyright = string.Format("(c) Microsoft 2015 - {0}", DateTime.Now.Year)
  });
  UpdateCsProjectVersion(version, csProjectFiles);
});

///////////////////////////////////////////////////////////////

public void UpdateCsProjectVersion(string version, FilePathCollection filePaths)
{
  Verbose(logAction => logAction("Setting version to {0}", version));
  foreach (var file in filePaths)
  {
    XmlPoke(file, "//PropertyGroup/VersionPrefix", version);
  }
}


Task("Default")
  .IsDependentOn("Test")
  //.IsDependentOn("Update-Version")
  .IsDependentOn("Package-NuGet");

Task("Mono")
  .IsDependentOn("Test");

Task("PR")
  //.IsDependentOn("Update-Version")
  .IsDependentOn("Test")
  .IsDependentOn("Benchmark")
  .IsDependentOn("Package-NuGet");

Task("Nightly")
  .IsDependentOn("Update-Version")
  .IsDependentOn("Compile")
  .IsDependentOn("Package-NuGet");
  

///////////////////////////////////////////////////////////////

RunTarget(target);