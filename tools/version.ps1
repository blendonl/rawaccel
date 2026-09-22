$header = Get-Content -Raw (Join-Path $PSScriptRoot '..\common\rawaccel-version.h')
$parts = foreach ($part in 'MAJOR', 'MINOR', 'PATCH') {
    if ($header -notmatch "#define\s+RA_VER_$part\s+(\d+)") {
        throw "RA_VER_$part is missing from common\rawaccel-version.h"
    }
    $Matches[1]
}
$parts -join '.'
