# PreToolUse/Bash hook: deny `dotnet ef migrations add` and similar variants.
# Stdin: Claude Code tool-use JSON. Stdout: hookSpecificOutput JSON (deny) or nothing.
$ErrorActionPreference = 'Stop'
try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    exit 0
}
$cmd = $payload.tool_input.command
if (-not $cmd) { exit 0 }

# Match `dotnet ef migrations add ...` (with any flags or working-dir prefix).
if ($cmd -match '(^|[\s;&|])dotnet\s+ef\s+migrations\s+add\b') {
    $out = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = 'Blocked by .claude/hooks/block-ef-migrations-add.ps1. CLAUDE.md says never run `dotnet ef migrations add` without asking. Confirm with the user, then have them run it manually.'
        }
    } | ConvertTo-Json -Depth 5 -Compress
    Write-Output $out
}
