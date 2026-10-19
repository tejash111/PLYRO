#!/usr/bin/env bash
# SOT-1 text rules G-1, G-2, G-3 and G-3b (foundation spec, section 8.1).
# Sourced by check_sources_of_truth.sh, which provides list_files, run_awk, scan_tokens, REVIEWED
# and TMP. Patterns are extended regular expressions, one per line; [.] and [(] are literal.
# Scope: G-1 (.uss/.uxml) and G-2 (.cs) read all of Assets, G-3 and G-3b only _Project. Every G rule
# skips Tooling (the g- scopes of list_files); FMOD is already left out of the file list.

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
# The value is read as tokens: pieces between spaces, where a space inside ( ) does not split, so
# rgba(0, 0, 0, 0.5) and var(--c, 1px 2px) are one token each. A literal length is a token that
# starts with a digit, a sign or a dot. A value that has var( anywhere and fewer than three
# literal lengths fails, because the blur could be the var(). With three literal lengths a var(
# can only be the color (USS takes at most three lengths), so the third one is judged as usual
# (ruling R29).
IFS= read -r -d '' AWK_TEXT_SHADOW <<'EOF' || true
function tokens(v, t,    n, i, c, depth, cur) {
  n = 0
  depth = 0
  cur = ""
  for (i = 1; i <= length(v); i++) {
    c = substr(v, i, 1)
    if (c == "(") depth++
    else if (c == ")" && depth > 0) depth--
    if (depth == 0 && c ~ /[[:space:]]/) {
      if (cur != "") t[++n] = cur
      cur = ""
    } else {
      cur = cur c
    }
  }
  if (cur != "") t[++n] = cur
  return n
}
function check(v, at,    n, i, t, lens, third) {
  v = tolower(v)
  n = tokens(v, t)
  lens = 0
  for (i = 1; i <= n; i++) {
    if (t[i] ~ /^[-+]?[.]?[0-9]/) {
      lens++
      if (lens == 3) { third = t[i]; break }
    }
  }
  if (lens < 3 && index(v, "var(") > 0) {
    print rule " Assets/" file ":" at " text-shadow blur can't be checked through var(): write the x, y and blur lengths as literals (blur 0)"
    return
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
  scan_tokens G-1 g-assets "uss uxml" 0 "banned gradient function" "$G1_PATTERNS" || return 1
  run_awk g-assets "uss uxml" -v rule=G-1 "$AWK_TEXT_SHADOW"
}

# ---------------------------------------------------------------------------------------------
# G-2: gradient, vignette and ambient APIs in .cs, anywhere under Assets. Lines that start with //
# are ignored. The constructor patterns cover new Gradient( and new UnityEngine.Gradient( with
# optional spaces, new Gradient { ... }, a new Gradient that ends its line or runs on into a //
# comment (its { is on the next line), and the target-typed form "Gradient name = new(". FilmGrain
# and Bloom are whole words (the awk here has no \b): they are the URP types G-4 judges. A line or
# trail renderer's end color is a gradient end, so it has its own message; .startColor is not
# banned, because ParticleSystem.MainModule.startColor is a flat color.
# ---------------------------------------------------------------------------------------------
G2_PATTERNS='(^|[^A-Za-z0-9_])new[[:space:]]+(UnityEngine[.])?Gradient[[:space:]]*([({]|//|$)
(^|[^A-Za-z0-9_])Gradient[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*=[[:space:]]*new[[:space:]]*[({]
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
ScreenSpaceLensFlare
(^|[^A-Za-z0-9_])FilmGrain([^A-Za-z0-9_]|$)
(^|[^A-Za-z0-9_])Bloom([^A-Za-z0-9_]|$)'

G2_END_COLOR='[.]endColor[[:space:]]*=([^=]|$)'

rule_G_2() {
  scan_tokens G-2 g-assets "cs" 1 "banned gradient or vignette API" "$G2_PATTERNS" || return 1
  scan_tokens G-2 g-assets "cs" 1 "a line or trail end color makes a gradient: use one flat color through the material, or author a one-key colorGradient in a prefab (G-5 checks it)" "$G2_END_COLOR"
}

# ---------------------------------------------------------------------------------------------
# G-3: gradient node types in .shadergraph/.shadersubgraph
# ---------------------------------------------------------------------------------------------
G3_PATTERNS='UnityEditor[.]ShaderGraph[.]GradientNode
UnityEditor[.]ShaderGraph[.]SampleGradient
UnityEditor[.]ShaderGraph[.]BlackbodyNode'

rule_G_3() {
  scan_tokens G-3 g-project "shadergraph shadersubgraph" 0 "banned gradient shader node" "$G3_PATTERNS"
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
  list_files g-project "shader hlsl shadergraph" > "$TMP/shaders.txt"
  awk -v rule=G-3b '
    FILENAME == ARGV[1] { listed[$0] = 1; next }
    {
      p = "Assets/" $0
      if (!(p in listed))
        print rule " " p " shader not reviewed: after checking it has no gradient, add \"" p " # reviewed by <name> on <date>: <why gradient-free>\" to tools/checks/reviewed-shaders.txt"
    }
  ' "$TMP/reviewed.txt" "$TMP/shaders.txt"
}
