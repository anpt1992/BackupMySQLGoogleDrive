# Stop hook: enforce the Definition of Done — `dotnet test` must pass.
# Exit 2 blocks turn completion and feeds stderr back to Claude so it keeps fixing.
$ErrorActionPreference = 'Stop'

# Drain stdin (Claude Code sends hook JSON there); we don't need its contents.
try { [Console]::In.ReadToEnd() | Out-Null } catch { }

$output = dotnet test --nologo -v q 2>&1 | Out-String

if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine("Definition of Done NOT met: `dotnet test` failed. Fix the failing tests before finishing.")
    [Console]::Error.WriteLine($output)
    exit 2
}

exit 0
