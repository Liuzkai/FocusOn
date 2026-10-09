param([ValidateSet('build', 'test', 'publish')][string]$Action = 'build')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$sdkExecutable = Join-Path $PSScriptRoot '.tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $sdkExecutable)) { $sdkExecutable = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.tools\cli-home'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.tools\nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
switch ($Action) {
    'build' { & $sdkExecutable build 'src\FocusOn\FocusOn.csproj' -c Release }
    'test' { & $sdkExecutable test 'tests\FocusOn.Tests\FocusOn.Tests.csproj' -c Release }
    'publish' { & $sdkExecutable publish 'src\FocusOn\FocusOn.csproj' -c Release -r win-x64 --self-contained true -o 'artifacts\FocusOn-win-x64' }
}
exit $LASTEXITCODE
