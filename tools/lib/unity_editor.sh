#!/usr/bin/env bash
# Finds an installed Unity editor under Unity Hub's editors directory. Source it; don't run it.
# Shared by tools/ci/unity.sh and tools/dev/check-env.sh, so both apply the same rule:
#
#   unity_find_editor <editors dir> <version>
#
# Unity Hub installs an Apple Silicon editor into <editors dir>/<version>-arm64 (or <version>),
# so the candidates are tried in this order:
#
#   1. <editors dir>/<version>-arm64
#   2. <editors dir>/<version>
#
# A candidate is valid only if Unity.app/Contents/Info.plist exists and
# Unity.app/Contents/MacOS/Unity is executable. A half-installed folder fails this: macOS kills
# its editor binary at launch, so "the file exists" is not enough.
#
# Returns 0 if a candidate is valid, otherwise 1. Sets these globals either way:
#   unity_editor_bin           editor binary of the first valid candidate ("" if none)
#   unity_editor_app           its Unity.app directory ("" if none)
#   unity_editor_tried         "<binary 1> or <binary 2>": the paths that were looked at
#   unity_editor_broken_dir    first candidate directory that exists but is not valid ("" if none)
#   unity_editor_broken_why    why it isn't valid: "no Info.plist", or
#                              "no executable Contents/MacOS/Unity"
#
# Written for bash 3.2 (macOS system bash).

unity_find_editor() {
  local editors_dir="$1" version="$2"
  local candidate contents

  unity_editor_bin=""
  unity_editor_app=""
  unity_editor_tried=""
  unity_editor_broken_dir=""
  unity_editor_broken_why=""

  for candidate in "$editors_dir/$version-arm64" "$editors_dir/$version"; do
    contents="$candidate/Unity.app/Contents"
    if [ -n "$unity_editor_tried" ]; then
      unity_editor_tried="$unity_editor_tried or $contents/MacOS/Unity"
    else
      unity_editor_tried="$contents/MacOS/Unity"
    fi

    if [ ! -f "$contents/Info.plist" ]; then
      if [ -e "$candidate" ] && [ -z "$unity_editor_broken_dir" ]; then
        unity_editor_broken_dir="$candidate"
        unity_editor_broken_why="no Info.plist"
      fi
      continue
    fi
    if [ ! -f "$contents/MacOS/Unity" ] || [ ! -x "$contents/MacOS/Unity" ]; then
      if [ -z "$unity_editor_broken_dir" ]; then
        unity_editor_broken_dir="$candidate"
        unity_editor_broken_why="no executable Contents/MacOS/Unity"
      fi
      continue
    fi

    unity_editor_bin="$contents/MacOS/Unity"
    unity_editor_app="$candidate/Unity.app"
    return 0
  done
  return 1
}
