#!/usr/bin/env bash
set -euo pipefail

if [[ ! ${1:-} =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)$ ]]; then
    echo "usage: $0 MAJOR.MINOR.PATCH" >&2
    exit 1
fi

sed -i -E \
    -e "s/^(#define[[:space:]]+RA_VER_MAJOR[[:space:]]+)[0-9]+/\1${BASH_REMATCH[1]}/" \
    -e "s/^(#define[[:space:]]+RA_VER_MINOR[[:space:]]+)[0-9]+/\1${BASH_REMATCH[2]}/" \
    -e "s/^(#define[[:space:]]+RA_VER_PATCH[[:space:]]+)[0-9]+/\1${BASH_REMATCH[3]}/" \
    "$(dirname "$0")/../common/rawaccel-version.h"
