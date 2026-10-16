$ErrorActionPreference = 'Stop'
$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/PortManager.App/PortManager.App.csproj'
foreach ($mode in @('default', 'true', 'false')) {
    $arguments = @('msbuild', $project, '-nologo', '-p:Configuration=Release', '-getProperty:DefineConstants')
    if ($mode -ne 'default') { $arguments += "-p:EnableLocalUpdateFeed=$mode" }
    $symbols = dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Cannot evaluate build symbols for $mode." }
    $enabled = (($symbols -join '') -split ';') -contains 'LOCAL_UPDATE_FEED'
    if ($enabled -ne ($mode -eq 'true')) { throw "Unexpected local-feed compilation for mode '$mode': $symbols" }
    Write-Host "PASS: local-feed build mode $mode"
}
