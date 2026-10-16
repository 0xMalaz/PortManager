[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^v\d+\.\d+\.\d+$')][string] $Tag,
    [Parameter(Mandatory)][string] $Repository,
    [string] $OutputDir = 'artifacts/releases'
)
$ErrorActionPreference = 'Stop'

function Get-Releases {
    $json = gh api "repos/$Repository/releases?per_page=100" --paginate --slurp
    if ($LASTEXITCODE -ne 0) { throw 'Cannot list releases; refusing to mutate anything.' }
    return @($json | ConvertFrom-Json | ForEach-Object { $_ } | ForEach-Object { $_ })
}
function Assert-Draft([long] $Id) {
    $json = gh api "repos/$Repository/releases/$Id"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify release state.' }
    $release = $json | ConvertFrom-Json
    if (!$release.draft -or $release.tag_name -cne $Tag) { throw 'Refusing to modify a published or mismatched release.' }
}

$version = $Tag.Substring(1)
foreach ($required in @("0xMalaz.PortManager-$version-full.nupkg", 'releases.win.json', 'assets.win.json', 'PortManager-Setup.exe')) {
    if (!(Test-Path (Join-Path $OutputDir $required))) { throw "Missing release asset: $required" }
}

# Only drafts may be discarded. Never use --clobber or delete the tag.
# The workflow serializes all releases; maintainers must not publish a draft while it is being built.
$existing = @(Get-Releases | Where-Object tag_name -CEQ $Tag)
if ($existing.Count -gt 1) { throw 'Ambiguous release state.' }
if ($existing.Count -eq 1) {
    Assert-Draft $existing[0].id
    gh api --method DELETE "repos/$Repository/releases/$($existing[0].id)"
    if ($LASTEXITCODE -ne 0) { throw 'Could not remove failed draft.' }
}

gh release create $Tag --repo $Repository --verify-tag --draft --title "Port Manager $version" --generate-notes
if ($LASTEXITCODE -ne 0) { throw 'Draft creation failed.' }
$created = @(Get-Releases | Where-Object tag_name -CEQ $Tag)
if ($created.Count -ne 1) { throw 'Cannot identify created draft.' }
$id = $created[0].id

# Upload the whole feed (including the previous full package referenced by its index), installer,
# and build manifest. Each upload must succeed before the release becomes visible to clients.
foreach ($asset in Get-ChildItem $OutputDir -File) {
    Assert-Draft $id
    gh release upload $Tag $asset.FullName --repo $Repository
    if ($LASTEXITCODE -ne 0) { throw "Upload failed: $($asset.Name). Draft remains unpublished; rerun the workflow." }
}
Assert-Draft $id
gh api --method PATCH "repos/$Repository/releases/$id" -F draft=false -f make_latest=true
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed. Inspect release state before retrying.' }
