# PostToolUse/Write|Edit hook: run `dotnet format whitespace` on the changed .cs file.
# Uses --folder mode so MSBuild doesn't load the whole solution (much faster).
$ErrorActionPreference = 'SilentlyContinue'
try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    exit 0
}
$file = $payload.tool_input.file_path
if (-not $file) { $file = $payload.tool_response.filePath }
if (-not $file -or -not ($file -like '*.cs') -or -not (Test-Path $file)) { exit 0 }

$dir = Split-Path $file -Parent
$name = Split-Path $file -Leaf

# --folder + --include scopes the operation to one file, avoiding solution load.
& dotnet format whitespace --folder --include $name $dir 2>&1 | Out-Null
exit 0
