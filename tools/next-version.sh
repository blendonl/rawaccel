#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$0")/.."

header_version() {
    local part value parts=()
    for part in MAJOR MINOR PATCH; do
        value=$(sed -nE "s/^#define[[:space:]]+RA_VER_${part}[[:space:]]+([0-9]+).*/\1/p" common/rawaccel-version.h)
        if [[ -z $value ]]; then
            echo "RA_VER_$part is missing from common/rawaccel-version.h" >&2
            exit 1
        fi
        parts+=("$value")
    done
    local IFS=.
    echo "${parts[*]}"
}

version_from_commits() {
    local tag major minor patch subjects bodies
    tag=$(git describe --tags --abbrev=0 --match 'v[0-9]*' 2>/dev/null) || return 0
    IFS=. read -r major minor patch <<<"${tag#v}"
    subjects=$(git log --no-merges --format=%s "$tag..HEAD")
    bodies=$(git log --no-merges --format=%b "$tag..HEAD")

    if grep -qE '^[a-z]+(\([^)]*\))?!:' <<<"$subjects" || grep -qE '^BREAKING[ -]CHANGE:' <<<"$bodies"; then
        echo "$((major + 1)).0.0"
    elif grep -qE '^feat(\([^)]*\))?:' <<<"$subjects"; then
        echo "$major.$((minor + 1)).0"
    elif grep -qE '^(fix|perf)(\([^)]*\))?:' <<<"$subjects"; then
        echo "$major.$minor.$((patch + 1))"
    else
        echo "$major.$minor.$patch"
    fi
}

current=$(header_version)
bumped=$(version_from_commits)
printf '%s\n' "$current" ${bumped:+"$bumped"} | sort -V | tail -n 1
