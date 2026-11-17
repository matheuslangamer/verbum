param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$localSdk = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools\dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
& $sdk publish (Join-Path $projectRoot 'src\Verbum\Verbum.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o (Join-Path $projectRoot 'dist') --nologo
if ($LASTEXITCODE -ne 0) { throw 'A publicação falhou.' }
if ($Test) {
    $exe = Join-Path $projectRoot 'dist\VERBUM.exe'
    $library = Join-Path $projectRoot 'biblioteca\biblias'
    $report = Join-Path $projectRoot 'docs\verbum-test-results.json'
    $argsList = @('--self-test', '--library', ('"' + $library + '"'), '--report', ('"' + $report + '"'))
    $testProcess = Start-Process -FilePath $exe -ArgumentList $argsList -WindowStyle Hidden -Wait -PassThru
    if ($testProcess.ExitCode -ne 0) { throw "Testes falharam. Veja $report" }
    Get-Content -LiteralPath $report -Raw
}
$publishedExe = Join-Path $projectRoot 'dist\VERBUM.exe'
$rootExe = Join-Path $projectRoot 'VERBUM.exe'
Copy-Item -LiteralPath $publishedExe -Destination $rootExe -Force
Write-Output $rootExe
