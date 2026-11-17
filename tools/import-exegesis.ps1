param([string]$OutputDirectory = "")
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'biblioteca/exegese' }
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.tools/dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$sdk = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $sdk)) { $sdk = (Get-Command dotnet -ErrorAction Stop).Source }
$hebrewRevision = '47db250bd55d0d8577f2a94fba114ef16c35b23c'
$greekRevision = '8423afe47b9e8f24b7772e808af45c7159a6fe7e'
$ubsRevision = 'a15a96cfaa4ca18a6c0339016645ba1e534db3c8'
$archives = @()
foreach ($entry in @(@('macula-hebrew', $hebrewRevision), @('macula-greek', $greekRevision))) {
    $archive = Join-Path ([IO.Path]::GetTempPath()) ('verbum-' + $entry[0] + '-' + $entry[1] + '.zip')
    if (-not (Test-Path -LiteralPath $archive)) {
        Invoke-WebRequest -Uri ('https://codeload.github.com/Clear-Bible/' + $entry[0] + '/zip/' + $entry[1]) -OutFile ($archive + '.part')
        Move-Item -LiteralPath ($archive + '.part') -Destination $archive
    }
    $archives += $archive
}
foreach ($source in @(@('MACULA-Hebrew', 'Clear-Bible/macula-hebrew', $hebrewRevision, 'LICENSE.md'), @('MACULA-Greek', 'Clear-Bible/macula-greek', $greekRevision, 'LICENSE.md'), @('UBS-HOTTP', 'ubsicap/ubs-open-license', $ubsRevision, 'HOTTP/LICENSE.md'), @('UBS-HOTTP', 'ubsicap/ubs-open-license', $ubsRevision, 'HOTTP/HOTTP.XML'), @('UBS-HOTTP', 'ubsicap/ubs-open-license', $ubsRevision, 'HOTTP/HOTTP-RATINGS-EN.HTML'), @('UBS-HOTTP', 'ubsicap/ubs-open-license', $ubsRevision, 'HOTTP/HOTTP-FACTORS-EN.HTML'))) {
    $folder = Join-Path $projectRoot ('biblioteca/fontes/' + $source[0])
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $target = Join-Path $folder ([IO.Path]::GetFileName($source[3]))
    if (-not (Test-Path -LiteralPath $target)) { Invoke-WebRequest -Uri ('https://raw.githubusercontent.com/' + $source[1] + '/' + $source[2] + '/' + $source[3]) -OutFile $target }
}
& $sdk run --project (Join-Path $PSScriptRoot 'ImportExegesis') -- $archives[0] $archives[1] (Join-Path $projectRoot 'biblioteca/fontes/UBS-HOTTP/HOTTP.XML') $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'A importação exegética falhou.' }
& $sdk run --project (Join-Path $PSScriptRoot 'ImportExegesis') -- --counts $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'A contagem dos lemas falhou.' }
Write-Output 'Dados exegéticos importados; fontes e licenças preservadas.'
