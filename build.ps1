param(
    [ValidateSet('build', 'test', 'publish', 'package-check', 'smoke')][string]$Action = 'build',
    [ValidateSet('', 'win-x64', 'win-arm64', 'linux-x64', 'linux-arm64', 'osx-x64', 'osx-arm64')][string]$Runtime = '',
    [ValidatePattern('^FocusOn-[A-Za-z0-9-]+$')][string]$PackageName = 'FocusOn-win-x64'
)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$sdkExecutable = Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $sdkExecutable)) {
    $sdkExecutable = Join-Path $PSScriptRoot '.tools/dotnet/dotnet'
}
if (-not (Test-Path -LiteralPath $sdkExecutable)) { $sdkExecutable = (Get-Command dotnet -ErrorAction Stop).Source }
if ($sdkExecutable.StartsWith((Join-Path $PSScriptRoot '.tools'))) { $env:DOTNET_ROOT = Split-Path -Parent $sdkExecutable }
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools/cli-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
$env:FOCUSON_DOTNET = $sdkExecutable
# Preserve explicit legacy package names; otherwise derive the name from the selected runtime.
$packageArgument = if ($PSBoundParameters.ContainsKey('PackageName')) { $PackageName } else { '-' }
$runtimeArgument = if ($Runtime) { $Runtime } else { '-' }
& $sdkExecutable run --project 'tools/FocusOn.Build/FocusOn.Build.csproj' -c Release --property:RestoreLockedMode=true -- $PSScriptRoot $Action $runtimeArgument $packageArgument
exit $LASTEXITCODE
