#!/usr/bin/env bash
# SOT-1 text rules G-1, G-2, G-3 and G-3b (foundation spec, section 8.1).
# Sourced by check_sources_of_truth.sh, which provides list_files, run_awk, scan_tokens, REVIEWED
# and TMP. Patterns are extended regular expressions, one per line; [.] and [(] are literal.
# Scope: _Project, plus .uss/.uxml anywhere under Assets (G-1). Excluded folders are already
# left out of the file list.

ALL_RULES="$ALL_RULES G-1 G-2 G-3 G-3b"

# ---------------------------------------------------------------------------------------------
# G-1: gradient functions and a blurred text-shadow in .uss/.uxml
# ---------------------------------------------------------------------------------------------
G1_PATTERNS='linear-gradient[(]
radial-gradient[(]
conic-gradient[(]'

# text-shadow takes <x> <y> <blur> <color>, with the color first or last. The value can run over
# several lines and ends at ; or } (or at the quote that closes a style="..." attribute). The
# third length is the blur radius and must be zero. A value with two lengths has no blur.
IFS= read -r -d '' AWK_TEXT_SHADOW <<'EOF' || true
function check(v, at,    n, i, t, lens, third) {
  v = tolower(v)
  gsub(/rgba?\([^)]*\)/, " ", v)
  gsub(/hsla?\([^)]*\)/, " ", v)
  n = split(v, t, /[[:space:]]+/)
  lens = 0
  for (i = 1; i <= n; i++) {
    if (t[i] ~ /^[-+]?[.]?[0-9]/) {
      lens++
      if (lens == 3) { third = t[i]; break }
    }
  }
  if (lens == 3 && third + 0 != 0)
    print rule " Assets/" file ":" at " text-shadow blur radius must be 0, found " third
}
FNR == 1 {
  if (pending) check(buf, start)
  pending = 0
  file = FILENAME
}
{
  rest = $0
  if (pending) {
    if (match(rest, /[;}"']/)) {
      check(buf " " substr(rest, 1, RSTART - 1), start)
      pending = 0
      rest = substr(rest, RSTART + 1)
    } else {
      buf = buf " " rest
      next
    }
  }
  while (match(rest, /text-shadow[[:space:]]*:/)) {
    rest = substr(rest, RSTART + RLENGTH)
    if (match(rest, /[;}"']/)) {
      check(substr(rest, 1, RSTART - 1), FNR)
      rest = substr(rest, RSTART + 1)
    } else {
      buf = rest
      start = FNR
      pending = 1
      break
    }
  }
}
END { if (pending) check(buf, start) }
EOF

rule_G_1() {
  scan_tokens G-1 assets "uss uxml" 0 "banned gradient function" "$G1_PATTERNS" || return 1
  run_awk assets "uss uxml" -v rule=G-1 "$AWK_TEXT_SHADOW"
}

# ---------------------------------------------------------------------------------------------
# G-2: gradient, vignette and ambient APIs in .cs. Lines that start with // are ignored.
# ---------------------------------------------------------------------------------------------
G2_PATTERNS='new Gradient[(]
GradientColorKey
GradientAlphaKey
colorGradient
enableVertexGradient
VertexGradient
TMP_ColorGradient
AmbientMode[.]Trilight
AmbientMode[.]Skybox
ambientSkyColor
ambientEquatorColor
ambientGroundColor
RenderSettings[.]fog[[:space:]]*=[[:space:]]*true
Vignette
ScreenSpaceLensFlare'

rule_G_2() {
  scan_tokens G-2 project "cs" 1 "banned gradient or vignette API" "$G2_PATTERNS"
}

# ---------------------------------------------------------------------------------------------
# G-3: gradient node types in .shadergraph/.shadersubgraph
# ---------------------------------------------------------------------------------------------
G3_PATTERNS='UnityEditor[.]ShaderGraph[.]GradientNode
UnityEditor[.]ShaderGraph[.]SampleGradient
UnityEditor[.]ShaderGraph[.]BlackbodyNode'

rule_G_3() {
  scan_tokens G-3 project "shadergraph shadersubgraph" 0 "banned gradient shader node" "$G3_PATTERNS"
}

# ---------------------------------------------------------------------------------------------
# G-3b: a .shader/.hlsl/.shadergraph under _Project must be listed in reviewed-shaders.txt.
# An entry is "<path> # reviewed by <name> on <date>: <why gradient-free>", with the path starting
# at Assets/. Blank lines and lines that start with # are ignored.
# ---------------------------------------------------------------------------------------------
rule_G_3b() {
  : > "$TMP/reviewed.txt"
  if [ -f "$REVIEWED" ]; then
    sed -e 's/[[:space:]]*#.*$//' -e 's/^[[:space:]]*//' -e 's/[[:space:]]*$//' "$REVIEWED" \
      | grep -v '^$' > "$TMP/reviewed.txt"
  fi
  list_files project "shader hlsl shadergraph" > "$TMP/shaders.txt"
  awk -v rule=G-3b '
    FILENAME == ARGV[1] { listed[$0] = 1; next }
    {
      p = "Assets/" $0
      if (!(p in listed))
        print rule " " p " shader not reviewed: after checking it has no gradient, add \"" p " # reviewed by <name> on <date>: <why gradient-free>\" to tools/checks/reviewed-shaders.txt"
    }
  ' "$TMP/reviewed.txt" "$TMP/shaders.txt"
}
