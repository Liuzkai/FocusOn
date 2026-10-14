param([Parameter(Mandatory)][string]$Runtime)
$ErrorActionPreference = 'Stop'
$focusonRoot = Split-Path $PSScriptRoot
$focusonQa = Join-Path ([System.IO.Path]::GetTempPath()) ('FocusOn-installer-' + [guid]::NewGuid().ToString('N'))
$focusonInstall = Join-Path $focusonQa 'Application'
$focusonData = Join-Path $focusonQa 'data'
New-Item -ItemType Directory -Force $focusonData | Out-Null
$focusonInstaller = Join-Path $focusonRoot "artifacts/release/FocusOn-$Runtime-Setup.exe"
$focusonEvidence = Join-Path $focusonRoot "artifacts/evidence/$Runtime/installer.json"
$focusonRecord = '{"language":"en","theme":"light","sentinel":"preserve-on-upgrade"}'
[System.IO.File]::WriteAllText((Join-Path $focusonData 'settings.json'), $focusonRecord)
# A real synthetic SQLite record stays outside the application installation, as production data does.
$env:FOCUSON_INSTALLER_QA_DATA = $focusonData
python -c 'import os,sqlite3; c=sqlite3.connect(os.path.join(os.environ["FOCUSON_INSTALLER_QA_DATA"],"focuson.db")); c.execute("create table preservation_test (value text)"); c.execute("insert into preservation_test values (?)", ("original-record",)); c.commit(); c.close()'
if ($LASTEXITCODE -ne 0) { throw 'SQLite fixture failed' }
$focusonBefore = (Get-FileHash (Join-Path $focusonData 'focuson.db')).Hash
try {
  foreach ($focusonPass in 1..2) {
    $focusonProcess = Start-Process $focusonInstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/DIR="' + $focusonInstall + '"'),('/LOG="' + (Join-Path $focusonQa "install-$focusonPass.log") + '"')) -Wait -PassThru
    if ($focusonProcess.ExitCode -ne 0) { throw "Installer exit $($focusonProcess.ExitCode)" }
    $focusonManifest = Get-Content (Join-Path $focusonInstall 'manifest.json') -Raw | ConvertFrom-Json
    foreach ($focusonFile in $focusonManifest.files) {
      $focusonPath = Join-Path $focusonInstall $focusonFile.path
      if ((Get-Item $focusonPath).Length -ne $focusonFile.bytes -or (Get-FileHash $focusonPath).Hash -ne $focusonFile.sha256) { throw "Installed file mismatch: $($focusonFile.path)" }
    }
    if ((Get-FileHash (Join-Path $focusonData 'focuson.db')).Hash -ne $focusonBefore) { throw 'Upgrade modified records' }
    if ([System.IO.File]::ReadAllText((Join-Path $focusonData 'settings.json')) -ne $focusonRecord) { throw 'Upgrade modified settings' }
  }
  # Run the installed executable from its actual installation and verify it can open/record/exit.
  $focusonSmokeData = Join-Path $focusonQa 'native-app-data'
  New-Item -ItemType Directory -Force $focusonSmokeData | Out-Null
  [System.IO.File]::WriteAllText((Join-Path $focusonSmokeData 'settings.json'), '{"language":"en","theme":"light"}')
  $focusonApp = Start-Process (Join-Path $focusonInstall 'FocusOn.exe') -ArgumentList @('--data-dir', ('"' + $focusonSmokeData + '"')) -PassThru
  Add-Type -AssemblyName UIAutomationClient
  Add-Type -AssemblyName UIAutomationTypes
  $focusonWindow = $null
  for ($focusonTry = 0; $focusonTry -lt 40; $focusonTry++) {
    Start-Sleep -Milliseconds 250
    $focusonApp.Refresh()
    if ($focusonApp.HasExited) { throw 'Installed app exited before displaying its window' }
    if ($focusonApp.MainWindowHandle -ne 0) {
      $focusonWindow = [System.Windows.Automation.AutomationElement]::FromHandle($focusonApp.MainWindowHandle)
      if ($null -ne $focusonWindow) { break }
    }
  }
  if ($null -eq $focusonWindow) { throw 'Installed app window did not open' }
  Start-Sleep -Seconds 2
  $focusonCondition = [System.Windows.Automation.AndCondition]::new(
    [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, 'Exit'),
    [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::ControlTypeProperty, [System.Windows.Automation.ControlType]::Button))
  $focusonExit = $focusonWindow.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $focusonCondition)
  if ($null -eq $focusonExit) { throw 'Installed app Exit control is inaccessible' }
  $focusonExit.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
  if (-not $focusonApp.WaitForExit(15000) -or $focusonApp.ExitCode -ne 0) { throw 'Installed app did not save and exit normally' }
  $env:FOCUSON_INSTALLER_QA_APP_DATA = $focusonSmokeData
  python -c 'import os,sqlite3; c=sqlite3.connect(os.path.join(os.environ["FOCUSON_INSTALLER_QA_APP_DATA"],"focuson.db")); assert c.execute("pragma integrity_check").fetchone()[0]=="ok"; assert c.execute("select count(*) from intervals").fetchone()[0]>0; c.close()'
  if ($LASTEXITCODE -ne 0) { throw 'Installed app failed to persist valid records' }
  $focusonUninstall = Start-Process (Join-Path $focusonInstall 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
  if ($focusonUninstall.ExitCode -ne 0 -or (Test-Path (Join-Path $focusonInstall 'FocusOn.exe'))) { throw 'Uninstall failed' }
  if ((Get-FileHash (Join-Path $focusonData 'focuson.db')).Hash -ne $focusonBefore) { throw 'Uninstall modified records' }
  if ([System.IO.File]::ReadAllText((Join-Path $focusonData 'settings.json')) -ne $focusonRecord) { throw 'Uninstall modified settings' }
  $focusonReport = Get-Content $focusonEvidence -Raw | ConvertFrom-Json
  $focusonReport.nativeInstallation = 'PASS: install, installed desktop, same-version upgrade, uninstall; records and preferences preserved'
  $focusonReport | ConvertTo-Json -Depth 10 | Set-Content $focusonEvidence
  Write-Output 'PASS native per-user installer, installed desktop, upgrade, uninstall and data preservation'
} finally {
  Remove-Item Env:FOCUSON_INSTALLER_QA_APP_DATA -ErrorAction SilentlyContinue
  Remove-Item Env:FOCUSON_INSTALLER_QA_DATA -ErrorAction SilentlyContinue
  Remove-Item $focusonQa -Recurse -Force -ErrorAction SilentlyContinue
}
