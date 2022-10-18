param(
    [string]$dotnet="dotnet",
    [ValidateSet("Debug","Release")]
    [string]$configuration="Release",
    [switch]$local,
    [switch]$nuget
)

Import-Module $PSScriptRoot/../../Scripts/common.psm1 -Force
CheckPSVersion

Write-Comment -prefix "." -text "Building the Coyote samples" -color "yellow"

if ($local.IsPresent -and $nuget.IsPresent) {
    # Restore the local coyote tool.
    &dotnet tool restore
}

# Check that the expected .NET SDK is installed.
$dotnet = "dotnet"
$dotnet_sdk_path = FindDotNetSdkPath -dotnet $dotnet
$sdk_version = FindDotNetSdkVersion -dotnet_sdk_path $dotnet_sdk_path

if ($null -eq $sdk_version) {
    Write-Error "The global.json file is pointing to version '$sdk_version' but no matching version was found."
    Write-Error "Please install .NET SDK version '$sdk_version' from https://dotnet.microsoft.com/download/dotnet."
    exit 1
}

# Build the task-based samples.
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../AccountManager/AccountManager.sln" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../BoundedBuffer/BoundedBuffer.csproj" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../CoffeeMachineTasks/CoffeeMachineTasks.csproj" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent

# Build the actor samples.
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../HelloWorldActors/HelloWorldActors.csproj" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../CoffeeMachineActors/CoffeeMachineActors.csproj" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../DrinksServingRobotActors/DrinksServingRobotActors.csproj" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../CloudMessaging/CloudMessaging.sln" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../Timers/Timers.csproj" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../Monitors/Monitors.csproj" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent

# Build the web app samples.
# Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../WebApps/ImageGalleryAspNet/ImageGallery.sln" `
    # -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent
Invoke-DotnetBuild -dotnet $dotnet -solution "$PSScriptRoot/../WebApps/PetImagesAspNet/PetImages.sln" `
    -config $configuration -local $local.IsPresent -nuget $nuget.IsPresent

Write-Comment -prefix "." -text "Successfully built the Coyote samples" -color "green"
