$CakeVersion = "0.17.0"
$DotNetVersion = "1.0.1";
$DotNetInstallerUri = "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.1/scripts/obtain/dotnet-install.ps1";

# Make sure tools folder exists
$PSScriptRoot = $pwd

$ToolPath = Join-Path $PSScriptRoot "tools"
if (!(Test-Path $ToolPath)) {
    Write-Verbose "Creating tools directory..."
    New-Item -Path $ToolPath -Type directory | out-null
}

###########################################################################
# INSTALL .NET CORE CLI
###########################################################################

# Get .NET Core CLI path if installed.
$FoundDotNetCliVersion = $null;
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $FoundDotNetCliVersion = dotnet --version;
}

if($FoundDotNetCliVersion -ne $DotNetVersion) {
    $InstallPath = Join-Path $PSScriptRoot ".dotnet"
    if (!(Test-Path $InstallPath)) {
        mkdir -Force $InstallPath | Out-Null;
    }
    (New-Object System.Net.WebClient).DownloadFile($DotNetInstallerUri, "$InstallPath\dotnet-install.ps1");
    & $InstallPath\dotnet-install.ps1 -Channel preview -Version $DotNetVersion -InstallDir $InstallPath;

    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    $env:DOTNET_CLI_TELEMETRY_OPTOUT=1

    & dotnet --info
}

###########################################################################
# INSTALL CAKE
###########################################################################

Add-Type -AssemblyName System.IO.Compression.FileSystem
Function Unzip {
    param([string]$zipfile, [string]$outpath)

    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipfile, $outpath)
}


# Make sure Cake has been installed.
$CakePath = Join-Path $ToolPath "Cake.CoreCLR.$CakeVersion/Cake.dll"
if (!(Test-Path $CakePath)) {
    Write-Host "Installing Cake..."
    (New-Object System.Net.WebClient).DownloadFile("https://www.nuget.org/api/v2/package/Cake.CoreCLR/$CakeVersion", "$ToolPath\Cake.CoreCLR.zip")
    Unzip "$ToolPath\Cake.CoreCLR.zip" "$ToolPath/Cake.CoreCLR.$CakeVersion"
    Remove-Item "$ToolPath\Cake.CoreCLR.zip"
}

###########################################################################
# INSTALL NUGET
###########################################################################

# Make sure NuGet has been installed.
$NugetPath = Join-Path $PSScriptRoot ".nuget/nuget.exe"
if (!(Test-Path $NugetPath)) {
    Write-Host "Installing Nuget..."
    (New-Object System.Net.WebClient).DownloadFile("https://www.nuget.org/nuget.exe", $NugetPath)
    & "$NugetPath" update -self
}

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

& dotnet "$CakePath" $args
exit $LASTEXITCODE