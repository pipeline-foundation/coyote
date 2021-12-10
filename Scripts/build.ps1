# Copyright (c) Microsoft Corporation.
# Licensed under the MIT License.

param(
    [ValidateSet("Debug", "Release")]
    [string]$configuration = "Release",
    [bool]$ci = $false
)

$ScriptDir = $PSScriptRoot

Import-Module $ScriptDir/powershell/common.psm1 -Force

Write-Comment -prefix "." -text "Building Coyote" -color "yellow"

if ($host.Version.Major -lt 7)
{
    Write-Error "Please use PowerShell v7.x or later (see https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell-core-on-windows?view=powershell-7)."
    exit 1
}

# Check that the expected .NET SDK is installed.
$dotnet = "dotnet"
$dotnet_path = FindDotNet($dotnet)
$version_net4 = $IsWindows -and (Get-ItemProperty "HKLM:SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full").Release -ge 528040
$version_netcore31 = FindInstalledDotNetSdk -dotnet_path $dotnet_path -version [version] "3.1.0"
$version_net5 = FindInstalledDotNetSdk -dotnet_path $dotnet_path -version [version] "5.0.0"
$sdk_version = FindDotNetSdk -dotnet_path $dotnet_path

if ($null -eq $sdk_version) {
    Write-Error "The global.json file is pointing to version '$sdk_version' but no matching version was found."
    Write-Error "Please install .NET SDK version '$sdk_version' from https://dotnet.microsoft.com/download/dotnet-core."
    exit 1
}

Write-Comment -prefix "..." -text "Using .NET SDK version $sdk_version" -color "white"
Write-Comment -prefix "..." -text "Configuration: $configuration" -color "white"
$solution = Join-Path -Path $ScriptDir -ChildPath "\.." -AdditionalChildPath "Coyote.sln"
$command = "build -c $configuration $solution /p:Platform=""Any CPU"""

if ($ci) {
    # Build any supported .NET versions that are installed on this machine.
    if ($version_net4) {
        # Build .NET Framework 4.x as well as the latest version.
        $command = $command + " /p:BUILD_NET462=yes"
    }

    if ($null -ne $version_netcore31 -and $version_netcore31 -ne $sdk_version) {
        # Build .NET Core 3.1 as well as the latest version.
        $command = $command + " /p:BUILD_NETCORE31=yes"
    }

    if ($null -ne $version_net5 -and $version_net5 -ne $sdk_version) {
        # Build .NET 5.0 as well as the latest version.
        $command = $command + " /p:BUILD_NET5=yes"
    }
}

$error_msg = "Failed to build Coyote"
Invoke-ToolCommand -tool $dotnet -cmd $command -error_msg $error_msg

Write-Comment -prefix "." -text "Successfully built Coyote" -color "green"
