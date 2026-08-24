#!/bin/bash
# Copy OpenCvSharpExtern sources to a staging dir and remove the modules we
# do not build in OpenCV (so the wrapper compiles against our slim OpenCV).
# Usage: prune-extern.sh <src_dir> <dst_dir>
set -u

SRC="${1:?source dir required}"
DST="${2:?destination dir required}"

EXCLUDED=(cuda.cpp)

mkdir -p "$DST"
# copy everything except the excluded module sources
for f in "$SRC"/*; do
    b=$(basename "$f")
    skip=0
    for e in "${EXCLUDED[@]}"; do
        [[ "$b" == "$e" ]] && skip=1 && break
    done
    [[ $skip -eq 0 ]] && cp -R "$f" "$DST/"
done

# Drop #include lines for the excluded modules from all copied sources.
# (include_opencv.h pulls in cuda.hpp ...)
# NOTE: keep bash 3.2-compatible (macOS /bin/bash) — no associative arrays.
for mod in cuda; do
  pat="opencv2/${mod}"
  # '#' delimiter avoids escaping the '/' in the path
  grep -rl "$pat" "$DST" 2>/dev/null | while read -r f; do
    sed -i '' "\\#${pat}#d" "$f"
  done || true
done
echo "Pruned extern sources -> $DST (removed: ${EXCLUDED[*]})"
