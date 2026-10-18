param(
    [string]$WorkspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')),
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$WorkspaceRoot = [System.IO.Path]::GetFullPath($WorkspaceRoot)
$workspacePrefix = $WorkspaceRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw | ConvertFrom-Json
$patch = Join-Path $PSScriptRoot 'typography.patch'

function Resolve-TypographyPath([string]$relativePath) {
    $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $WorkspaceRoot $relativePath))
    if (-not $resolvedPath.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Typography path escapes the workspace: $relativePath"
    }
    return $resolvedPath
}

# Check everything before modifying anything. Later edits to newly added files need
# manual merging rather than deletion. Existing files use an inverse patch, not copies.
foreach ($addedFile in $manifest.Added) {
    $target = Resolve-TypographyPath $addedFile.Path
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Missing typography file: $($addedFile.Path)" }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $addedFile.Sha256) {
        throw "Later changes detected in $($addedFile.Path). Preserve those changes before rolling back."
    }
}
$exactFiles = @()
foreach ($existingFile in $manifest.Existing) {
    $target = Resolve-TypographyPath $existingFile.Path
    $before = Join-Path $PSScriptRoot ('before/' + $existingFile.Path + '.before')
    if ((Get-FileHash -LiteralPath $before).Hash -ne $existingFile.BeforeSha256) { throw "Invalid rollback snapshot: $($existingFile.Path)" }
    if ((Get-FileHash -LiteralPath $target).Hash -eq $existingFile.AfterSha256) { $exactFiles += $existingFile }
}
Push-Location -LiteralPath $WorkspaceRoot
try {
    & git -c core.autocrlf=true apply --reverse --check -- $patch
    if ($LASTEXITCODE -ne 0) { throw 'Typography patch conflicts with later edits. No files were changed; merge only the typography hunks manually.' }
    if ($CheckOnly) { Write-Output 'Typography-only rollback checks passed. No files changed.'; return }
    & git -c core.autocrlf=true apply --reverse -- $patch
    if ($LASTEXITCODE -ne 0) { throw 'Unable to reverse the typography patch.' }
    # Preserve the exact original encoding and mixed line endings when there are no
    # later edits; files with later edits retain the inverse-patch result instead.
    foreach ($existingFile in $exactFiles) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('before/' + $existingFile.Path + '.before')) -Destination (Resolve-TypographyPath $existingFile.Path)
    }
    foreach ($addedFile in $manifest.Added) {
        Remove-Item -LiteralPath (Resolve-TypographyPath $addedFile.Path)
    }
    Write-Output 'Typography implementation removed. Earlier SQL, ETL, reference-number and unrelated changes were preserved.'
}
finally { Pop-Location }
