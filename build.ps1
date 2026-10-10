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
    'publish' {
        $uncommitted = & git status --porcelain
        if ($LASTEXITCODE -ne 0) { throw '无法读取发布源版本。' }
        if ($uncommitted) { throw '请先提交或处理工作区修改再发布；发布清单必须对应已提交的源版本。' }
        & $sdkExecutable publish 'src\FocusOn\FocusOn.csproj' -c Release -r win-x64 --self-contained true -o 'artifacts\FocusOn-win-x64'
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        $publishDirectory = Join-Path $PSScriptRoot 'artifacts\FocusOn-win-x64'
        $requiredFiles = @('FocusOn.exe', 'FocusOn.dll', 'FocusOn.deps.json', 'FocusOn.runtimeconfig.json',
            'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'System.Private.CoreLib.dll',
            'PresentationFramework.dll', 'PresentationNative_cor3.dll', 'wpfgfx_cor3.dll',
            'WindowsBase.dll', 'System.Windows.Forms.dll', 'Microsoft.Data.Sqlite.dll', 'e_sqlite3.dll')
        foreach ($requiredFile in $requiredFiles) {
            if (-not (Test-Path -LiteralPath (Join-Path $publishDirectory $requiredFile) -PathType Leaf)) {
                throw "发布包缺少运行文件：$requiredFile"
            }
        }
        $runtimeConfig = Get-Content -LiteralPath (Join-Path $publishDirectory 'FocusOn.runtimeconfig.json') -Raw | ConvertFrom-Json
        foreach ($framework in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
            if ($framework -notin $runtimeConfig.runtimeOptions.includedFrameworks.name) {
                throw "发布包未包含自包含运行时：$framework"
            }
        }
        $dependencies = Get-Content -LiteralPath (Join-Path $publishDirectory 'FocusOn.deps.json') -Raw | ConvertFrom-Json
        if ($dependencies.runtimeTarget.name -notlike '*/win-x64') { throw '发布包目标不是 Windows x64。' }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\usage.md') -Destination (Join-Path $publishDirectory '使用说明.md') -Force
        $sourceCommit = & git rev-parse HEAD
        $fileRecords = @(Get-ChildItem -LiteralPath $publishDirectory -File -Recurse |
            Where-Object { $_.Name -ne 'manifest.json' } | Sort-Object FullName | ForEach-Object {
                [ordered]@{
                    path = [IO.Path]::GetRelativePath($publishDirectory, $_.FullName)
                    bytes = $_.Length
                    sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
                }
            })
        [ordered]@{ sourceCommit = $sourceCommit; runtime = 'win-x64'; selfContained = $true; files = $fileRecords } |
            ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $publishDirectory 'manifest.json') -Encoding utf8
        $zipPath = Join-Path $PSScriptRoot 'artifacts\FocusOn-win-x64.zip'
        Compress-Archive -LiteralPath $publishDirectory -DestinationPath $zipPath -Force
        $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
        "$zipHash  FocusOn-win-x64.zip" | Set-Content -LiteralPath ($zipPath + '.sha256') -Encoding ascii
        Write-Output "已检查自包含文件并打包：$zipPath"
        Write-Output "SHA256：$zipHash"
    }
}
exit $LASTEXITCODE
