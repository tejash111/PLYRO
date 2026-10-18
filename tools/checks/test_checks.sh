#!/usr/bin/env bash
# Tests the source-of-truth text checks: check_sources_of_truth.sh against the fixture folders in
# tools/checks/fixtures/{pass,fail}/<rule-id>/, against generated files and against throw-away git
# repos (staged mode), plus install-hooks.sh and the pre-commit hook it writes.
# Run: bash tools/checks/test_checks.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$ROOT/tools/lib/assert.sh"

CHECKS="$ROOT/tools/checks"
CHECK="$CHECKS/check_sources_of_truth.sh"
FIXTURES="$CHECKS/fixtures"

# The throw-away repos must not see the developer's git setup (hooksPath, signing, identity) or
# the repo this script runs in (when it is started from a hook, git exports GIT_DIR and friends).
unset GIT_DIR GIT_INDEX_FILE GIT_WORK_TREE
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1
export GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.com
export GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.com

# Paths contain spaces on purpose: the scripts must quote them.
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
SPACED="$WORK/with space"
mkdir -p "$SPACED"

# in_dir <dir> <cmd...>: run a command from another working directory.
in_dir() {
  local dir="$1"
  shift
  (cd "$dir" && "$@")
}

# check_pass <fixtures dir> <rule>: the pass fixture exits 0 and reports nothing for the rule.
check_pass() {
  local out rc=0
  out="$("$CHECK" --root "$1/pass/$2/Assets" --rules "$2" 2>&1)" || rc=$?
  if [ "$rc" -ne 0 ] || printf '%s\n' "$out" | grep -q "^$2 "; then
    printf 'exit %s\n%s\n' "$rc" "$out"
    return 1
  fi
}

# check_fail <fixtures dir> <rule>: the fail fixture exits 1 and reports the rule.
check_fail() {
  local out rc=0
  out="$("$CHECK" --root "$1/fail/$2/Assets" --rules "$2" 2>&1)" || rc=$?
  if [ "$rc" -ne 1 ] || ! printf '%s\n' "$out" | grep -q "^$2 "; then
    printf 'exit %s\n%s\n' "$rc" "$out"
    return 1
  fi
}

# output_lacks <needle> <cmd...>: the command's output does not contain the needle.
output_lacks() {
  local needle="$1" out
  shift
  out="$("$@" 2>&1)" || true
  case "$out" in
    *"$needle"*)
      printf '%s\n' "$out"
      return 1
      ;;
  esac
}

# prints_nothing <cmd...>: the command's output is empty (its exit code is not looked at).
prints_nothing() {
  local out
  out="$("$@" 2>&1)" || true
  [ -z "$out" ] || { printf '%s\n' "$out"; return 1; }
}

# run_at <rule> <path below Assets> <file content>: run one rule on a one-file Assets folder.
run_at() {
  local dir
  dir="$(mktemp -d "$WORK/one.XXXXXX")"
  mkdir -p "$(dirname "$dir/Assets/$2")"
  printf '%s' "$3" > "$dir/Assets/$2"
  "$CHECK" --root "$dir/Assets" --rules "$1" < /dev/null
}

# run_one <rule> <path below Assets/_Project> <file content>: run_at for a file under _Project.
run_one() {
  run_at "$1" "_Project/$2" "$3"
}

# gen_lines <n>: n comment lines, each ending in a newline.
gen_lines() {
  awk -v n="$1" 'BEGIN { for (i = 1; i <= n; i++) print "// line " i }'
}

# gen_assets <dir> <path below Assets> <lines> [unterminated]: a .cs file of that many lines in
# <dir>/Assets. With "unterminated" the last line has no newline at its end.
gen_assets() {
  local f="$1/Assets/$2"
  mkdir -p "$(dirname "$f")"
  if [ "${4:-}" = unterminated ]; then
    printf '%s' "$(gen_lines "$3")" > "$f"
  else
    gen_lines "$3" > "$f"
  fi
}

# mk_files <dir> <path below Assets>...: empty-ish files in <dir>/Assets.
mk_files() {
  local dir="$1" f
  shift
  for f in "$@"; do
    mkdir -p "$(dirname "$dir/Assets/$f")"
    echo x > "$dir/Assets/$f"
  done
}

# put <repo> <path> <content>: write a file below a repo.
put() {
  mkdir -p "$(dirname "$1/$2")"
  printf '%s\n' "$3" > "$1/$2"
}

# new_repo <dir>: an empty git repo.
new_repo() {
  mkdir -p "$1"
  git -C "$1" -c init.defaultBranch=main init -q
}

# copy_checks <dir>: put the check scripts into <dir>/tools/checks, as in a checkout of the repo.
copy_checks() {
  mkdir -p "$1/tools/checks"
  cp "$CHECKS/check_sources_of_truth.sh" "$CHECKS/install-hooks.sh" "$1/tools/checks/"
  cp -R "$CHECKS/lib" "$1/tools/checks/lib"
}

BAD_USS='.a { background-image: linear-gradient(red, blue); }'
UNITY="unity/MotionBowling/Assets/_Project"

# ---------------------------------------------------------------------------------------------
# Fixtures, one pass and one fail case per rule. The second round runs from a path with a space.
# ---------------------------------------------------------------------------------------------
cp -R "$FIXTURES" "$SPACED/fixtures"
for fx in "$FIXTURES" "$SPACED/fixtures"; do
  for rule in G-1 G-2 G-3 G-3b M-3 M-5 M-6 M-8; do
    assert_exit 0 check_pass "$fx" "$rule"
    assert_exit 0 check_fail "$fx" "$rule"
  done
done

# Each violation line is "<rule> <path>[:<line>] <message>", with paths starting at Assets/.
G1="$FIXTURES/fail/G-1/Assets"
assert_output_contains "G-1 Assets/_Project/Styles/Gradient.uss:2 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/_Project/Styles/Shadow.uss:2 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/_Project/Styles/Extra.uss:2 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/_Project/Styles/Extra.uss:5 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/Menus/Outside.uxml:2 " "$CHECK" --root "$G1" --rules G-1
assert_exit 0 output_lacks "Extra.uss:8" "$CHECK" --root "$G1" --rules G-1
# A var() among the first three tokens of a text-shadow fails (ruling R25); one after them does not.
assert_output_contains "G-1 Assets/_Project/Styles/Var.uss:2 " "$CHECK" --root "$G1" --rules G-1
assert_output_contains "G-1 Assets/_Project/Styles/Var.uss:5 " "$CHECK" --root "$G1" --rules G-1
assert_exit 0 output_lacks "Var.uss:8" "$CHECK" --root "$G1" --rules G-1

# G-2 skips lines that start with //, but still flags the same token in code (Review Focus 4).
G2="$FIXTURES/fail/G-2/Assets"
assert_output_contains "G-2 Assets/_Project/Scripts/Bad.cs:4 " "$CHECK" --root "$G2" --rules G-2
assert_output_contains "G-2 Assets/_Project/Scripts/Bad.cs:5 " "$CHECK" --root "$G2" --rules G-2
assert_output_contains "G-2 Assets/_Project/Scripts/Bad.cs:6 " "$CHECK" --root "$G2" --rules G-2
assert_exit 0 output_lacks "Bad.cs:1 " "$CHECK" --root "$G2" --rules G-2
assert_exit 0 output_lacks "Bad.cs:2 " "$CHECK" --root "$G2" --rules G-2
# G-2 reads all of Assets (ruling R25), the constructor forms, .endColor =, FilmGrain and Bloom.
assert_output_contains "G-2 Assets/Other/Outside.cs:3 " "$CHECK" --root "$G2" --rules G-2
for ln in 3 4 5 6 8; do
  assert_output_contains "G-2 Assets/_Project/Scripts/Forms.cs:$ln " "$CHECK" --root "$G2" --rules G-2
done
assert_exit 0 output_lacks "Forms.cs:7 " "$CHECK" --root "$G2" --rules G-2

# G-3 covers .shadergraph and .shadersubgraph.
G3="$FIXTURES/fail/G-3/Assets"
assert_output_contains "G-3 Assets/_Project/Shaders/Ramp.shadergraph:7 " "$CHECK" --root "$G3" --rules G-3
assert_output_contains "G-3 Assets/_Project/Shaders/Sub.shadersubgraph:6 " "$CHECK" --root "$G3" --rules G-3

# G-3b: .shader, .hlsl and .shadergraph under _Project need an entry in the reviewed list.
G3B="$FIXTURES/fail/G-3b/Assets"
assert_output_contains "G-3b Assets/_Project/Shaders/Flat.hlsl " "$CHECK" --root "$G3B" --rules G-3b
assert_output_contains "G-3b Assets/_Project/Shaders/Unlit.shader " "$CHECK" --root "$G3B" --rules G-3b
assert_output_contains "G-3b Assets/_Project/Shaders/Graph.shadergraph " "$CHECK" --root "$G3B" --rules G-3b
assert_exit 0 "$CHECK" --root "$G3B" --rules G-3b --reviewed "$FIXTURES/pass/G-3b/reviewed-shaders.txt"
assert_exit 1 "$CHECK" --root "$FIXTURES/pass/G-3b/Assets" --rules G-3b --reviewed "$FIXTURES/fail/G-3b/reviewed-shaders.txt"

# ---------------------------------------------------------------------------------------------
# Every banned token on its own line (the spec's lists, spelled out here on purpose).
# ---------------------------------------------------------------------------------------------
while IFS= read -r token; do
  assert_exit 1 run_one G-1 T.uss "x: ${token}red, blue);"
done <<'EOF'
linear-gradient(
radial-gradient(
conic-gradient(
EOF
assert_exit 0 run_one G-1 T.uss "x: linear-gradient;"

while IFS= read -r token; do
  assert_exit 1 run_one G-2 T.cs "var x = $token;"
  assert_exit 1 run_one G-2 T.cs "    $token // not a full-line comment"
  assert_exit 0 run_one G-2 T.cs "    // $token"
  assert_exit 0 run_one G-2 T.cs "//$token"
done <<'EOF'
new Gradient(
GradientColorKey
GradientAlphaKey
colorGradient
enableVertexGradient
VertexGradient
TMP_ColorGradient
AmbientMode.Trilight
AmbientMode.Skybox
ambientSkyColor
ambientEquatorColor
ambientGroundColor
RenderSettings.fog = true
Vignette
ScreenSpaceLensFlare
EOF
assert_exit 0 run_one G-2 T.cs "var x = AmbientMode.Flat; RenderSettings.fog = false;"

# G-2 constructor forms, the renderer end colour, and the post-processing type names (ruling R25).
while IFS= read -r stmt; do
  assert_exit 1 run_one G-2 T.cs "$stmt"
  assert_exit 1 run_one G-2 T.cs "    $stmt // not a full-line comment"
  assert_exit 0 run_one G-2 T.cs "    // $stmt"
done <<'EOF'
var g = new UnityEngine.Gradient();
var g = new Gradient ();
var g = new  Gradient();
var g = new Gradient { mode = GradientMode.Fixed };
var g = new Gradient{};
var g = new Gradient
Gradient g = new();
Gradient g = new ();
private Gradient _g = new() { mode = GradientMode.Fixed };
private readonly UnityEngine.Gradient _g = new();
Gradient  g  =  new  (  );
line.endColor = Color.red;
line.endColor=Color.red;
trail.endColor   = c;
var t = typeof(FilmGrain);
volume.profile.TryGet(out Bloom bloom);
class Bloom : VolumeComponent { }
EOF
while IFS= read -r stmt; do
  assert_exit 0 run_one G-2 T.cs "$stmt"
done <<'EOF'
Gradient g;
public Gradient Colors { get; }
Gradient[] gs = new Gradient[2];
var h = new GradientHelper();
var h = new Gradient2();
line.startColor = Color.red;
var c = main.startColor;
if (line.endColor == c) { }
if (line.endColor != c) { }
var endColor = Color.red;
endColor = Color.red;
var BloomRadius = 1f;
var bloom = 1f;
var _bloom = 1f;
var Bloomberg = 1f;
var MyBloom = 1f;
var Bloom_Strength = 1f;
var FilmGrainy = 1f;
var xFilmGrain = 1f;
EOF
# A constructor whose { starts on the next line (Allman style) is found too.
assert_exit 1 run_one G-2 T.cs 'var g = new Gradient
{
    mode = GradientMode.Fixed,
};'
assert_output_contains "G-2 Assets/_Project/T.cs:1 " run_one G-2 T.cs 'var g = new Gradient
{
};'
assert_output_contains "G-2 Assets/_Project/T.cs:1 " run_one G-2 T.cs 'line.endColor = Color.red;'
assert_output_contains "one-key colorGradient" run_one G-2 T.cs 'line.endColor = Color.red;'
# Scope: .cs anywhere under Assets, except FMOD and Tooling.
G2_CODE='var g = new Gradient();'
while IFS= read -r p; do
  assert_exit 1 run_at G-2 "$p" "$G2_CODE"
done <<'EOF'
Scripts/T.cs
Other/Deep/Down/T.cs
T.cs
Plugins/Other/T.cs
Plugins/FMODX/T.cs
_Project/Scripts/T.cs
_Project/Modules/Gameplay/Runtime/T.cs
_Project/Modules/Gameplay/Tests/EditMode/T.cs
_Project/Modules/App/Editor/T.cs
EOF
while IFS= read -r p; do
  assert_exit 0 run_at G-2 "$p" "$G2_CODE"
done <<'EOF'
Plugins/FMOD/T.cs
Plugins/FMOD/Deep/T.cs
_Project/Modules/Tooling/Editor/T.cs
_Project/Modules/Tooling/Tests/EditMode/T.cs
Scripts/T.txt
Scripts/T.uss
EOF
# The Tooling exclusion is for the G rules only: they leave Tooling alone, the others don't (see M-3 to M-8 below).
assert_exit 0 run_at G-1 _Project/Modules/Tooling/Editor/T.uss "$BAD_USS"
assert_exit 1 run_at G-1 _Project/Modules/Toolbox/Editor/T.uss "$BAD_USS"
assert_exit 0 run_at G-3 _Project/Modules/Tooling/Editor/T.shadergraph '"m_Type": "UnityEditor.ShaderGraph.GradientNode"'
assert_exit 0 run_at G-3b _Project/Modules/Tooling/Editor/T.hlsl 'float4 F() { return 0; }'

while IFS= read -r token; do
  assert_exit 1 run_one G-3 T.shadergraph "\"m_Type\": \"UnityEditor.ShaderGraph.$token\""
  assert_exit 1 run_one G-3 T.shadersubgraph "\"m_Type\": \"UnityEditor.ShaderGraph.$token\""
done <<'EOF'
GradientNode
SampleGradient
BlackbodyNode
EOF

# ---------------------------------------------------------------------------------------------
# G-1 text-shadow: only a non-zero third length (the blur radius) fails.
# ---------------------------------------------------------------------------------------------
shadow() { run_one G-1 T.uss "$(printf '.a {\n    %s\n}\n' "$1")"; }

assert_exit 1 shadow 'text-shadow: 2px 2px 4px black;'
assert_exit 1 shadow 'text-shadow: 2px 2px 0.5px black;'
assert_exit 1 shadow 'text-shadow: 2px 2px .5px black;'
assert_exit 1 shadow 'text-shadow: -1px -1px 2px #000;'
assert_exit 1 shadow 'text-shadow: black 2px 2px 4px;'
assert_exit 1 shadow 'text-shadow: rgba(10, 20, 30, 0.5) 1px 1px 3px;'
assert_exit 1 shadow 'text-shadow:2px 2px 3px red;'
assert_exit 1 shadow 'text-shadow : 2px 2px 3px red;'
assert_exit 1 shadow 'text-shadow: 1px 1px 2px red'
assert_exit 0 shadow 'text-shadow: 2px 2px 0px black;'
assert_exit 0 shadow 'text-shadow: 2px 2px 0 black;'
assert_exit 0 shadow 'text-shadow: 2px 2px 0.0px black;'
assert_exit 0 shadow 'text-shadow: 2px 2px;'
assert_exit 0 shadow 'text-shadow: 2px 2px black'
assert_exit 0 shadow 'text-shadow: -1px -1px 0 #000;'
assert_exit 0 shadow 'text-shadow: rgba(10, 20, 30, 0.5) 1px 1px 0;'
assert_exit 0 shadow 'text-shadow: none;'
assert_exit 0 shadow 'transition-property: text-shadow;'
# A value that runs to the end of the file, and one that continues on the next lines.
assert_exit 1 run_one G-1 T.uss 'text-shadow: 1px 1px 2px red'
assert_exit 0 run_one G-1 T.uss 'text-shadow: 1px 1px 0px red'
assert_exit 1 shadow 'text-shadow: 1px
        1px
        2px red;'
assert_exit 1 run_one G-1 T.uxml '<ui:Label style="text-shadow: 1px 1px 2px black;" />'
assert_exit 0 run_one G-1 T.uxml '<ui:Label style="text-shadow: 1px 1px 0px black;" />'
assert_output_contains "found 4px" shadow 'text-shadow: 2px 2px 4px black;'

# A var() among the first three value tokens fails, because the blur could be a token (ruling R25).
assert_exit 1 shadow 'text-shadow: 1px 1px var(--blur) black;'
assert_exit 1 shadow 'text-shadow: 1px 1px var(--blur);'
assert_exit 1 shadow 'text-shadow: var(--shadow);'
assert_exit 1 shadow 'text-shadow: var(--x) var(--y) var(--blur) var(--c);'
assert_exit 1 shadow 'text-shadow: 1px var(--y) 0 black;'
assert_exit 1 shadow 'text-shadow: var(--x) 1px 0 black;'
assert_exit 1 shadow 'text-shadow: VAR(--shadow);'
assert_exit 1 shadow 'text-shadow:var(--shadow);'
assert_exit 1 shadow 'text-shadow: var(--blur, 0) 1px 1px black;'
assert_exit 1 shadow 'text-shadow: var(--offsets, 1px 1px) 0 black;'
assert_exit 1 shadow 'text-shadow: 1px 1px
        var(--blur) red;'
assert_exit 1 shadow 'text-shadow: var(--shadow)'
assert_exit 1 run_one G-1 T.uxml '<ui:Label style="text-shadow: 1px 1px var(--blur) black;" />'
assert_exit 1 run_one G-1 T.uxml '<ui:Label style="text-shadow: var(--shadow);" />'
# A var() after the first three tokens (the colour) is fine, and so is any var() that is not in a text-shadow.
assert_exit 0 shadow 'text-shadow: 2px 2px 0 var(--color-shadow);'
assert_exit 0 shadow 'text-shadow: 2px 2px 0px var(--color, rgba(0, 0, 0, 0.5));'
assert_exit 0 shadow 'text-shadow: 2px 2px 0 var(--c) ;'
assert_exit 0 shadow 'text-shadow: none; color: var(--color);'
assert_exit 0 run_one G-1 T.uss '.a { text-shadow: 0 0 0 black; color: var(--text); }
.b { margin-left: var(--gap); }'
assert_exit 0 run_one G-1 T.uxml '<ui:Label style="text-shadow: 1px 1px 0px var(--c);" />'
assert_output_contains "write the blur radius as a literal 0" shadow 'text-shadow: 1px 1px var(--blur) black;'
assert_output_contains "G-1 Assets/_Project/T.uss:2 " shadow 'text-shadow: var(--shadow);'
# A colour function is one token whatever is in it: its numbers are not lengths.
assert_exit 1 shadow 'text-shadow: rgba(10, 20, 30, 0.5) 1px 1px 3px;'
assert_exit 1 shadow 'text-shadow: rgba( 10 , 20 , 30 , 0.5 ) 1px 1px 3px;'
assert_exit 1 shadow 'text-shadow: 1px 1px 3px rgba(10, 20, 30, 0.5);'
assert_exit 0 shadow 'text-shadow: rgba( 10 , 20 , 30 , 0.5 ) 1px 1px 0;'
assert_exit 0 shadow 'text-shadow: 1px 1px 0 hsl(10 20% 30% / 50%);'

# ---------------------------------------------------------------------------------------------
# SOT-2 text rules: M-3, M-4, M-5, M-6 and M-8. The fixtures are checked in the loop at the top;
# these are the output lines and the edge cases.
# ---------------------------------------------------------------------------------------------
CORE="Assets/_Project/Modules/Gameplay/Runtime/Core"

# M-3: one public top-level type per file, named like the file.
M3F="$FIXTURES/fail/M-3/Assets"
assert_output_contains "M-3 $CORE/Two.cs:7 " "$CHECK" --root "$M3F" --rules M-3
assert_output_contains "'Other'" "$CHECK" --root "$M3F" --rules M-3
assert_exit 0 output_lacks "Two.cs:3 " "$CHECK" --root "$M3F" --rules M-3
# Tooling is judged like any other module (ruling R25).
assert_output_contains "M-3 Assets/_Project/Modules/Tooling/Editor/Dup.cs:7 " "$CHECK" --root "$M3F" --rules M-3
assert_exit 1 run_at M-3 _Project/Modules/Tooling/Editor/T.cs 'public class T { }
public class U { }'
assert_exit 1 run_at M-3 _Project/Modules/Tooling/Editor/T.cs 'public class U { }'
assert_exit 0 run_at M-3 _Project/Modules/Tooling/Editor/T.cs 'public class T { }'
assert_output_contains "M-3 $CORE/Mismatch.cs:3 " "$CHECK" --root "$M3F" --rules M-3
assert_output_contains "must be in a file named NotMismatch.cs" "$CHECK" --root "$M3F" --rules M-3

# m3 <source>: the source is the file T.cs, so its one public type has to be called T.
m3() { run_one M-3 T.cs "$1"; }

assert_exit 0 m3 ''
assert_exit 0 m3 'class U { }'
assert_exit 0 m3 'internal class U { }'
assert_exit 0 m3 'public class T { }'
# Every keyword and modifier, with the right name (passes) and with a wrong one (fails).
while IFS= read -r decl; do
  assert_exit 0 m3 "${decl//NAME/T} { }"
  assert_exit 1 m3 "${decl//NAME/U} { }"
done <<'EOF'
public class NAME
public struct NAME
public interface NAME
public enum NAME
public record NAME
public record struct NAME
public record class NAME
public sealed class NAME
public static class NAME
public abstract class NAME
public static partial class NAME
public sealed partial class NAME
public readonly struct NAME
public ref struct NAME
public readonly ref struct NAME
public unsafe struct NAME
public unsafe class NAME
static public class NAME
sealed public class NAME
public class NAME<TKey, TValue>
public class NAME<TKey> where TKey : class
public enum NAME : byte
public class NAME : Base<int>, IFoo
public interface NAME<out TOut>
EOF
assert_exit 0 m3 'public delegate void T(int x);'
assert_exit 0 m3 'public delegate Task<int> T<X>(X x);'
assert_exit 1 m3 'public delegate Task<int> U<X>(X x);'
assert_exit 1 m3 'public delegate Dictionary<string, List<int>> U(int x);'
assert_exit 0 m3 'public delegate Dictionary<string, List<int>> T(int x);'
assert_exit 1 m3 'public delegate void U(int x);'
assert_exit 0 m3 'public record T(int A, string B);'
assert_exit 1 m3 'public record U(int A);'
assert_exit 1 m3 'public class TT { }'
assert_exit 1 m3 'public class t { }'

# Namespace depth: block (1), file-scoped (0), nested, none. A type inside a type doesn't count.
assert_exit 0 m3 'namespace A { public class T { } }'
assert_exit 0 m3 'namespace A.B { namespace C { public class T { } } }'
assert_exit 0 m3 'namespace A;
public class T { }'
assert_exit 0 m3 'namespace A
{
    public class T
    {
        public enum Kind { X }
        public class Nested { }
        public delegate void Done();
    }
}'
assert_exit 0 m3 'internal class H { public class N { } }
public class T { }'
assert_exit 0 m3 'public class T { }
internal class U { }'
assert_exit 0 m3 '[System.Serializable] public class T { }'
assert_exit 1 m3 '[System.Serializable] public class U { }'
assert_exit 1 m3 '[A][B(1, 2)] public sealed class U { }'
assert_exit 1 m3 '[Foo("a;b{")]
public class U { }'
assert_exit 0 m3 '[Foo("a;b{")]
public class T { }'
assert_exit 1 m3 'public class T { }
public class U { }'
assert_exit 1 m3 'public class T { } public class U { }'
assert_exit 1 m3 'namespace A;
public class T { }
public class U { }'
assert_exit 1 m3 'namespace A; public class U { }'
assert_exit 1 m3 'namespace A { public class T { } }
namespace B { public class U { } }'
assert_exit 1 m3 'public class T { }
public enum E { }'
assert_exit 1 m3 'public class T { }
public delegate void D();'
assert_exit 1 m3 'public class T { }
public interface IT { }'
assert_exit 1 m3 'public delegate void T(); public delegate void U();'
assert_output_contains "M-3 Assets/_Project/T.cs:2 " m3 'public class T { }
public class U { }'

# Comments and strings: keywords there don't count, and neither do their braces (a stray brace would
# make the next type look nested, and the check would pass).
assert_exit 0 m3 '// public class U { }
public class T { }'
assert_exit 0 m3 '/* public class U { } */ public class T { }'
assert_exit 0 m3 '/*
public class U { }
*/
public class T { }'
assert_exit 1 m3 'public class T { string s = "{"; }
public class U { }'
assert_exit 1 m3 'public class T { } // {
public class U { }'
assert_exit 1 m3 "public class T { char c = '{'; }
public class U { }"
assert_exit 1 m3 "public class T { char c = '\"'; }
public class U { }"
assert_exit 1 m3 'public class T { string s = @"{"; }
public class U { }'
assert_exit 1 m3 'public class T { string s = @"a""b{"; }
public class U { }'
assert_exit 1 m3 'public class T { string s = "\"{"; }
public class U { }'
assert_exit 1 m3 'public class T { string s = "//"; }
public class U { }'
assert_exit 1 m3 'public class T { string s = @"
{
"; }
public class U { }'
assert_exit 1 m3 'public class T { string s = $"{1 + 2}"; }
public class U { }'
assert_exit 1 m3 'public class T { /* { */ }
public class U { }'
assert_exit 1 m3 'public class T { /*
{
*/ }
public class U { }'
IFS= read -r -d '' ESCAPED_QUOTE_CHAR <<'EOF' || true
public class T { char c = '\''; string s = "}"; }
public class U { }
EOF
assert_exit 1 m3 "$ESCAPED_QUOTE_CHAR"
# Windows line endings, a byte order mark, and an apostrophe in a #region line.
assert_exit 0 m3 $'public class T\r\n{\r\n}\r\n'
assert_exit 0 m3 $'\xef\xbb\xbfpublic class T { }'
assert_exit 1 m3 $'\xef\xbb\xbfpublic class U { }'
assert_exit 0 m3 $'#region It\'s here\npublic class T { }\n#endregion'
# Preprocessor lines are skipped, so a brace in a #region title doesn't make the next type nested.
assert_exit 1 m3 $'#region Braces {\npublic class U { }\n#endregion'

# M-4: line count. Over 400 fails, over 300 only warns. A last line without a newline counts.
M4="$SPACED/m4 files"
BIG="_Project/Modules/Gameplay/Runtime/Core/Big.cs"
gen_assets "$M4/l401" "$BIG" 401
assert_exit 1 "$CHECK" --root "$M4/l401/Assets" --rules M-4
assert_output_contains "M-4 Assets/$BIG 401 lines (max 400)" "$CHECK" --root "$M4/l401/Assets" --rules M-4
gen_assets "$M4/l400" "$BIG" 400
assert_exit 0 "$CHECK" --root "$M4/l400/Assets" --rules M-4
assert_output_contains "WARN M-4 Assets/$BIG 400 lines (soft limit 300)" "$CHECK" --root "$M4/l400/Assets" --rules M-4
gen_assets "$M4/l301" "$BIG" 301
assert_exit 0 "$CHECK" --root "$M4/l301/Assets" --rules M-4
assert_output_contains "WARN M-4 Assets/$BIG 301 lines (soft limit 300)" "$CHECK" --root "$M4/l301/Assets" --rules M-4
gen_assets "$M4/l300" "$BIG" 300
assert_exit 0 "$CHECK" --root "$M4/l300/Assets" --rules M-4
assert_exit 0 prints_nothing "$CHECK" --root "$M4/l300/Assets" --rules M-4
gen_assets "$M4/u401" "$BIG" 401 unterminated
assert_exit 1 "$CHECK" --root "$M4/u401/Assets" --rules M-4
assert_output_contains "M-4 Assets/$BIG 401 lines (max 400)" "$CHECK" --root "$M4/u401/Assets" --rules M-4
gen_assets "$M4/u400" "$BIG" 400 unterminated
assert_exit 0 "$CHECK" --root "$M4/u400/Assets" --rules M-4
gen_assets "$M4/u301" "$BIG" 301 unterminated
assert_output_contains "WARN M-4 Assets/$BIG 301 lines (soft limit 300)" "$CHECK" --root "$M4/u301/Assets" --rules M-4
gen_assets "$M4/u300" "$BIG" 300 unterminated
assert_exit 0 prints_nothing "$CHECK" --root "$M4/u300/Assets" --rules M-4
# A warning and a violation in one run, then a short file after them: each file is counted alone.
gen_assets "$M4/mixed" "_Project/Modules/Gameplay/Runtime/A.cs" 401
gen_assets "$M4/mixed" "_Project/Modules/Gameplay/Runtime/B.cs" 301
gen_assets "$M4/mixed" "_Project/Modules/Gameplay/Runtime/C.cs" 5
assert_exit 1 "$CHECK" --root "$M4/mixed/Assets" --rules M-4
assert_output_contains "M-4 Assets/_Project/Modules/Gameplay/Runtime/A.cs 401 lines" "$CHECK" --root "$M4/mixed/Assets" --rules M-4
assert_output_contains "WARN M-4 Assets/_Project/Modules/Gameplay/Runtime/B.cs 301 lines" "$CHECK" --root "$M4/mixed/Assets" --rules M-4
assert_exit 0 output_lacks "C.cs" "$CHECK" --root "$M4/mixed/Assets" --rules M-4
# Only .cs under _Project: not .uss, and not other folders of Assets (M-6 flags a .cs there).
gen_assets "$M4/out" "Other/Big.cs" 500
gen_assets "$M4/out" "_Project/Styles/Big.uss" 500
assert_exit 0 prints_nothing "$CHECK" --root "$M4/out/Assets" --rules M-4
# Tooling is judged like any other module (ruling R25): the exclusion is for the G rules only.
gen_assets "$M4/tool" "_Project/Modules/Tooling/Editor/Big.cs" 401
assert_exit 1 "$CHECK" --root "$M4/tool/Assets" --rules M-4
assert_output_contains "M-4 Assets/_Project/Modules/Tooling/Editor/Big.cs 401 lines (max 400)" "$CHECK" --root "$M4/tool/Assets" --rules M-4
gen_assets "$M4/toolwarn" "_Project/Modules/Tooling/Tests/EditMode/Big.cs" 301
assert_exit 0 "$CHECK" --root "$M4/toolwarn/Assets" --rules M-4
assert_output_contains "WARN M-4 Assets/_Project/Modules/Tooling/Tests/EditMode/Big.cs 301 lines" "$CHECK" --root "$M4/toolwarn/Assets" --rules M-4

# M-5: scene lookups and singletons in runtime code, not in App, tests or editor code.
M5F="$FIXTURES/fail/M-5/Assets"
assert_output_contains "M-5 $CORE/UsesFind.cs:7 " "$CHECK" --root "$M5F" --rules M-5
assert_output_contains "M-5 $CORE/Singleton.cs:5 " "$CHECK" --root "$M5F" --rules M-5

m5() { run_one M-5 "Modules/Gameplay/Runtime/Core/T.cs" "$1"; }
while IFS= read -r token; do
  assert_exit 1 m5 "var x = $token;"
  assert_exit 1 m5 "    var x = $token; // not a full-line comment"
  assert_exit 0 m5 "    // $token"
  assert_exit 0 m5 "    /// $token"
done <<'EOF'
FindObjectOfType<Foo>()
FindFirstObjectByType<Foo>()
FindAnyObjectByType<Foo>()
Object.FindObjectOfType(typeof(Foo))
GameObject.Find("Ui")
GameObject.FindWithTag("Ui")
Resources.Load<Foo>("x")
Resources.LoadAll("x")
EOF
while IFS= read -r decl; do
  assert_exit 1 m5 "$decl"
done <<'EOF'
public static T Instance { get; private set; }
public static T Instance;
static T Instance
internal static Foo<Bar> Instance { get; }
public static readonly T Instance = new T();
public static Foo? Instance { get; set; }
public static Bowling.Gameplay.Foo Instance { get; }
public static T Instance=null;
EOF
while IFS= read -r decl; do
  assert_exit 0 m5 "$decl"
done <<'EOF'
public T Instance { get; }
public static T Instances { get; }
public static T InstanceCount { get; }
public static T GetInstance() { return null; }
public static void Reset() { Instance = null; }
// public static T Instance { get; }
var t = typeof(GameObject);
var x = Find("x");
var y = GetComponentInChildren<Foo>();
EOF
# Where it applies: Modules/<X>/Runtime/ except Modules/App/.
FIND='var x = FindObjectOfType<Foo>();'
while IFS= read -r p; do
  assert_exit 1 run_one M-5 "$p" "$FIND"
done <<'EOF'
Modules/Gameplay/Runtime/T.cs
Modules/Gameplay/Runtime/Core/T.cs
Modules/UI/Runtime/Components/Badge/T.cs
Modules/Testing/Runtime/T.cs
Modules/AppX/Runtime/T.cs
Modules/Tooling/Runtime/T.cs
EOF
while IFS= read -r p; do
  assert_exit 0 run_one M-5 "$p" "$FIND"
done <<'EOF'
Modules/App/Runtime/T.cs
Modules/App/Runtime/Core/T.cs
Modules/App/Editor/T.cs
Modules/Gameplay/Editor/T.cs
Modules/Gameplay/Editor/Runtime/T.cs
Modules/Gameplay/Tests/EditMode/T.cs
Modules/Gameplay/Tests/PlayMode/T.cs
Scripts/T.cs
EOF
assert_exit 0 run_one M-5 Modules/Gameplay/Runtime/T.uss "$FIND"

# M-6: every .cs under Assets is inside a module asmdef folder: Modules/<M>/Runtime, Editor,
# Tests/EditMode or Tests/PlayMode (Tooling too). Only Plugins/FMOD (vendor) is exempt.
M6F="$FIXTURES/fail/M-6/Assets"
assert_output_contains "M-6 Assets/_Project/Scripts/Loose.cs " "$CHECK" --root "$M6F" --rules M-6
assert_output_contains "M-6 Assets/Scripts/Loose.cs " "$CHECK" --root "$M6F" --rules M-6
assert_output_contains "M-6 Assets/_Project/Modules/Gameplay/Foo/Bar.cs " "$CHECK" --root "$M6F" --rules M-6
assert_output_contains "M-6 Assets/_Project/Modules/Tooling/Loose.cs " "$CHECK" --root "$M6F" --rules M-6
assert_output_contains "module asmdef folder" "$CHECK" --root "$M6F" --rules M-6
M6="public class T { }"
while IFS= read -r p; do
  assert_exit 1 run_at M-6 "$p" "$M6"
done <<'EOF'
T.cs
Scripts/T.cs
Scripts/Deep/Down/T.cs
Other/T.cs
Plugins/T.cs
Plugins/FMODX/T.cs
Plugins/Other/T.cs
_TestTemp/T.cs
_Project/T.cs
_Project/Scripts/T.cs
_Project/Scripts/Deep/Down/T.cs
_Project/Modules/T.cs
_Project/Modules/Gameplay/T.cs
_Project/Modules/Gameplay/Foo/T.cs
_Project/Modules/Gameplay/Foo/Runtime/T.cs
_Project/Modules/Gameplay/Config/T.cs
_Project/Modules/Gameplay/Tests/T.cs
_Project/Modules/Gameplay/Tests/Other/T.cs
_Project/Modules/Gameplay/runtime/T.cs
_Project/Modules/Tooling/T.cs
_Project/Modules/Tooling/Config/T.cs
EOF
while IFS= read -r p; do
  assert_exit 0 run_at M-6 "$p" "$M6"
done <<'EOF'
_Project/Modules/Gameplay/Runtime/T.cs
_Project/Modules/Gameplay/Runtime/Core/Deep/T.cs
_Project/Modules/Gameplay/Editor/T.cs
_Project/Modules/Gameplay/Editor/Setup/T.cs
_Project/Modules/Gameplay/Tests/EditMode/T.cs
_Project/Modules/Gameplay/Tests/PlayMode/Sub/T.cs
_Project/Modules/Tooling/Editor/T.cs
_Project/Modules/Tooling/Editor/Validation/Rules/T.cs
_Project/Modules/Tooling/Tests/EditMode/T.cs
_Project/Modules/App/Runtime/T.cs
_Project/Modules/App/Editor/T.cs
Plugins/FMOD/T.cs
Plugins/FMOD/Deep/T.cs
Scripts/T.txt
Scripts/T.uss
_Project/Scripts/T.txt
_Project/Modules/Gameplay/Foo/T.uss
EOF

# M-8, color literals: only in declaration values, and not in Styles/Tokens.uss.
M8F="$FIXTURES/fail/M-8/Assets"
CMP="_Project/Modules/UI/Runtime/Components"
assert_output_contains "M-8 Assets/$CMP/Tile/Tile.uss:2 " "$CHECK" --root "$M8F" --rules M-8
assert_output_contains "'#FF0000'" "$CHECK" --root "$M8F" --rules M-8
assert_exit 0 output_lacks "Tile missing" "$CHECK" --root "$M8F" --rules M-8

# uss <content> [path below Assets]: run M-8 on one .uss file.
uss() { run_at M-8 "${2:-_Project/Styles/Hud.uss}" "$1"; }

while IFS= read -r decl; do
  assert_exit 1 uss ".a { $decl }"
done <<'EOF'
color: #FF0000;
color: #f00;
color: #f00f;
color: #abcdef;
color: #FF000080;
color:#fff;
color: rgb(1, 2, 3);
color: rgba(1, 2, 3, 0.5);
color: RGBA(1, 2, 3, 0.5);
color: RGB(1,2,3);
flex-grow: 1; color: #fff;
border-color: red #fff;
--x: #fff;
EOF
assert_exit 1 uss '.a{color:#fff}'
assert_exit 1 uss '#badge-root:hover { color: #fff; }'
assert_exit 1 uss '.a {
    color:
        #fff;
}'
assert_exit 1 uss '.a {
    transition-property: color;
    border-color: red,
        #abc;
}'
assert_exit 1 uss '.a { background-image: url(project://database/x.png); color: #fff; }'
assert_output_contains "M-8 Assets/_Project/Styles/Hud.uss:2 " uss '.a {
    color: #fff;
}'
# Any .uss under Assets, and only Styles/Tokens.uss of the UI module is exempt.
assert_exit 1 uss '.a { color: #fff; }' Menus/Menu.uss
# Tooling is not excluded from M-8 (the exclusion is for the G rules only).
assert_exit 1 uss '.a { color: #fff; }' _Project/Modules/Tooling/Editor/Window.uss
assert_exit 1 uss ':root { --c: #fff; }' _Project/Modules/UI/Runtime/Styles/Other.uss
assert_exit 1 uss ':root { --c: #fff; }' _Project/Modules/UI/Runtime/Tokens.uss
assert_exit 1 uss ':root { --c: #fff; }' _Project/Modules/Gameplay/Runtime/Styles/Tokens.uss
assert_exit 0 uss ':root { --c: #fff; --d: rgb(1, 2, 3); --e: rgba(1, 2, 3, 0.5); }' _Project/Modules/UI/Runtime/Styles/Tokens.uss
while IFS= read -r css; do
  assert_exit 0 uss "$css"
done <<'EOF'
#badge-root { flex-grow: 1; }
#add { flex-grow: 1; }
#bad { flex-grow: 1; }
#Cafe { flex-grow: 1; }
#deadbe { flex-grow: 1; }
.a:hover #bad { color: red; }
.a #bad, .b #add { color: red; }
.a { color: var(--color-primary); }
.a { color: red; }
.a { color: #abcde; }
.a { color: #abcdefg; }
.a { color: #ggg; }
.a { width: #12; }
.a { --x: argb(1); }
/* .a { color: #fff; } */
.a { color: red; /* #fff */ }
// .a { color: #fff; }
.a { -unity-font: url("project://database/Assets/a.ttf#fff"); }
.a { -unity-font: url('project://database/Assets/a.ttf#fff'); }
EOF
assert_exit 0 uss '/*
.a { color: #fff; }
*/
.b { color: red; }'
assert_exit 1 uss '/* one */ .a { color: #fff; }'

# Nothing carries over from one file to the next, even when a file ends inside a block or comment.
LEAK="$(mktemp -d "$WORK/leak.XXXXXX")"
mkdir -p "$LEAK/Assets/_Project/Modules/UI/Runtime"
printf '%s\n' '.a { color: red;' > "$LEAK/Assets/_Project/Modules/UI/Runtime/A.uss"
printf '%s\n' '.b:hover #bad { color: red; }' > "$LEAK/Assets/_Project/Modules/UI/Runtime/B.uss"
printf '%s\n' '/* never closed' > "$LEAK/Assets/_Project/Modules/UI/Runtime/C.uss"
printf '%s\n' '.d { color: #fff; }' > "$LEAK/Assets/_Project/Modules/UI/Runtime/D.uss"
printf '%s\n' 'public class A {' > "$LEAK/Assets/_Project/Modules/UI/Runtime/A.cs"
printf '%s\n' '/* never closed' > "$LEAK/Assets/_Project/Modules/UI/Runtime/C.cs"
printf '%s\n' 'string s = @"never closed' > "$LEAK/Assets/_Project/Modules/UI/Runtime/E.cs"
for name in B D F; do
  printf '%s\n' "public class Wrong$name { }" > "$LEAK/Assets/_Project/Modules/UI/Runtime/$name.cs"
done
printf '%s\n' 'namespace Leak' > "$LEAK/Assets/_Project/Modules/UI/Runtime/G.cs"
printf '%s\n' 'public class H { public class Nested { } }' > "$LEAK/Assets/_Project/Modules/UI/Runtime/H.cs"
assert_output_contains "M-8 Assets/_Project/Modules/UI/Runtime/D.uss:1 " "$CHECK" --root "$LEAK/Assets" --rules M-8
assert_exit 0 output_lacks "B.uss" "$CHECK" --root "$LEAK/Assets" --rules M-8
assert_exit 0 output_lacks "H.cs" "$CHECK" --root "$LEAK/Assets" --rules M-3
assert_output_contains "M-3 Assets/_Project/Modules/UI/Runtime/B.cs:1 " "$CHECK" --root "$LEAK/Assets" --rules M-3
assert_output_contains "M-3 Assets/_Project/Modules/UI/Runtime/D.cs:1 " "$CHECK" --root "$LEAK/Assets" --rules M-3
assert_output_contains "M-3 Assets/_Project/Modules/UI/Runtime/F.cs:1 " "$CHECK" --root "$LEAK/Assets" --rules M-3

# M-8, component folders: <Name>.uxml, <Name>.uss and <Name>.cs, each from the Name folder.
# m8_files <path below Assets>...: run M-8 on a tree of those files.
m8_files() {
  local dir
  dir="$(mktemp -d "$WORK/m8.XXXXXX")"
  mk_files "$dir" "$@"
  "$CHECK" --root "$dir/Assets" --rules M-8 < /dev/null
}
assert_exit 0 m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.uss" "$CMP/Badge/Badge.cs"
assert_exit 0 m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.uss" "$CMP/Badge/Badge.cs" \
  "$CMP/Badge/Badge.uxml.meta" "$CMP/Badge/Helper.cs" "$CMP/Badge/Sub/Extra.cs" "$CMP/Badge.meta"
assert_exit 0 m8_files "$CMP/My Badge/My Badge.uxml" "$CMP/My Badge/My Badge.uss" "$CMP/My Badge/My Badge.cs"
assert_exit 1 m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.cs"
assert_output_contains "M-8 Assets/$CMP/Badge missing Badge.uss " m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.cs"
assert_output_contains "M-8 Assets/$CMP/Badge missing Badge.uxml " m8_files "$CMP/Badge/Badge.uss" "$CMP/Badge/Badge.cs"
assert_output_contains "M-8 Assets/$CMP/Badge missing Badge.cs " m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.uss"
assert_output_contains "M-8 Assets/$CMP/My Badge missing My Badge.cs " m8_files "$CMP/My Badge/My Badge.uxml" "$CMP/My Badge/My Badge.uss"
assert_exit 1 m8_files "$CMP/Badge/Other.cs"
assert_output_contains "Badge missing Badge.uxml" m8_files "$CMP/Badge/Other.cs"
assert_output_contains "Badge missing Badge.uss" m8_files "$CMP/Badge/Other.cs"
assert_output_contains "Badge missing Badge.cs" m8_files "$CMP/Badge/Other.cs"
# A wrong case, wrong names, and files in a sub-folder don't count.
assert_output_contains "Badge missing Badge.uss" m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/badge.uss" "$CMP/Badge/Badge.cs"
assert_exit 1 m8_files "$CMP/Badge/Chip.uxml" "$CMP/Badge/Chip.uss" "$CMP/Badge/Chip.cs"
assert_exit 1 m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.cs" "$CMP/Badge/Sub/Badge.uss"
assert_output_contains "Badge missing Badge.uss" m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.cs" "$CMP/Badge/Badge.uss.meta"
# Only the bad folder is reported.
assert_exit 0 output_lacks "Badge missing" m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.uss" "$CMP/Badge/Badge.cs" "$CMP/Chip/Chip.cs"
assert_output_contains "Chip missing Chip.uxml" m8_files "$CMP/Badge/Badge.uxml" "$CMP/Badge/Badge.uss" "$CMP/Badge/Badge.cs" "$CMP/Chip/Chip.cs"
# Not component folders: files directly in Components/, other modules, screens, the Editor folder.
assert_exit 0 m8_files "$CMP/Readme.md" "$CMP/Loose.cs" "$CMP/Loose.meta"
assert_exit 0 m8_files "_Project/Modules/Gameplay/Runtime/Components/Foo/Foo.cs"
assert_exit 0 m8_files "_Project/Modules/UI/Runtime/Screens/HudScreen/HudScreen.uxml"
assert_exit 0 m8_files "_Project/Modules/UI/Runtime/Styles/Tokens.uss"
assert_exit 0 m8_files "_Project/Modules/UI/Editor/Components/Foo/Foo.cs"

# ---------------------------------------------------------------------------------------------
# Rule selection and the default rule set.
# ---------------------------------------------------------------------------------------------
assert_exit 0 "$CHECK" --root "$G2" --rules G-1
assert_exit 1 "$CHECK" --root "$G2" --rules G-1,G-2
assert_exit 1 "$CHECK" --root "$G2"
assert_exit 1 "$CHECK" --root "$G1"
assert_exit 0 "$CHECK" --root "$FIXTURES/pass/G-3b/Assets"
# Hud.uss in the G-1 pass fixture writes rgba() on purpose (a text-shadow case), which M-8 forbids
# in any .uss but Tokens.uss. So G-1 is clean there and the default rule set is not.
assert_exit 0 "$CHECK" --root "$FIXTURES/pass/G-1/Assets" --rules G-1,G-2,G-3,G-3b
assert_output_contains "M-8 Assets/_Project/Styles/Hud.uss:" "$CHECK" --root "$FIXTURES/pass/G-1/Assets"
# The SOT-2 text rules are part of the default set: the pass fixtures are clean under every rule,
# the fail fixtures are not.
for rule in M-3 M-5 M-6 M-8; do
  assert_exit 0 "$CHECK" --root "$FIXTURES/pass/$rule/Assets"
  assert_exit 1 "$CHECK" --root "$FIXTURES/fail/$rule/Assets"
done
assert_exit 1 "$CHECK" --root "$M4/l401/Assets"
assert_exit 0 "$CHECK" --root "$M4/l301/Assets"
assert_exit 0 "$CHECK" --root "$G2" --rules M-3,M-4,M-5,M-8
assert_exit 1 "$CHECK" --root "$G2" --rules M-6
assert_exit 0 "$CHECK" --root "$M3F" --rules M-4,M-5,M-6,M-8
assert_exit 1 "$CHECK" --root "$M3F" --rules M-3,M-4
# M-1, M-2 and M-7 are editor-layer rules, not part of this script.
assert_exit 2 "$CHECK" --root "$G1" --rules M-7
assert_output_contains "unknown rule: M-7" "$CHECK" --root "$G1" --rules M-7

# Usage errors exit 2.
assert_exit 2 "$CHECK"
assert_exit 2 "$CHECK" --bogus
assert_exit 2 "$CHECK" --all --staged
assert_exit 2 "$CHECK" --root
assert_exit 2 "$CHECK" --root "$WORK/does not exist"
assert_exit 2 "$CHECK" --root "$G1" --rules G-99
assert_exit 2 "$CHECK" --root "$G1" --rules
assert_exit 2 "$CHECK" --root "$G1" --reviewed "$WORK/missing.txt"

# ---------------------------------------------------------------------------------------------
# The runner's rule contract, with rules injected into a copy of the lib: a warning doesn't fail
# the run, a violation does, and a rule that can't run is an error rather than a silent pass.
# ---------------------------------------------------------------------------------------------
X="$SPACED/extra rules repo"
copy_checks "$X"
cat >> "$X/tools/checks/lib/rules_gradients.sh" <<'EOF'
ALL_RULES="$ALL_RULES T-1 T-2"
rule_T_1() { echo "WARN T-1 Assets/x.cs:1 only a warning"; }
rule_T_2() { echo "T-2 Assets/x.cs:2 a violation"; }
EOF
X_CHECK="$X/tools/checks/check_sources_of_truth.sh"
X_ROOT="$FIXTURES/pass/G-1/Assets"
assert_exit 0 "$X_CHECK" --root "$X_ROOT" --rules T-1
assert_output_contains "WARN T-1 Assets/x.cs:1 only a warning" "$X_CHECK" --root "$X_ROOT" --rules T-1
assert_exit 1 "$X_CHECK" --root "$X_ROOT" --rules T-1,T-2
assert_exit 1 "$X_CHECK" --root "$X_ROOT"
assert_exit 0 "$X_CHECK" --root "$X_ROOT" --rules G-1
printf 'rule_G_1() { return 1; }\n' >> "$X/tools/checks/lib/rules_gradients.sh"
assert_exit 2 "$X_CHECK" --root "$X_ROOT" --rules G-1
assert_output_contains "rule G-1 failed to run" "$X_CHECK" --root "$X_ROOT" --rules G-1

# ---------------------------------------------------------------------------------------------
# Fail closed: a rules library that is missing, broken or empty is an error (exit 2), never a
# pass. Without this the hook would let every commit through. G1 has a banned gradient in it.
# ---------------------------------------------------------------------------------------------
NL="$SPACED/no lib repo"
mkdir -p "$NL/tools/checks"
cp "$CHECK" "$NL/tools/checks/"
assert_exit 2 "$NL/tools/checks/check_sources_of_truth.sh" --root "$G1"
assert_output_contains "cannot load rules library" "$NL/tools/checks/check_sources_of_truth.sh" --root "$G1"
assert_exit 2 "$NL/tools/checks/check_sources_of_truth.sh" --all

SE="$SPACED/syntax error repo"
copy_checks "$SE"
printf 'rule_BROKEN() {\n' >> "$SE/tools/checks/lib/rules_gradients.sh"
assert_exit 2 "$SE/tools/checks/check_sources_of_truth.sh" --root "$G1"
assert_output_contains "cannot load rules library" "$SE/tools/checks/check_sources_of_truth.sh" --root "$G1"

EL="$SPACED/empty lib repo"
copy_checks "$EL"
: > "$EL/tools/checks/lib/rules_gradients.sh"
assert_exit 2 "$EL/tools/checks/check_sources_of_truth.sh" --root "$G1"
assert_output_contains "no rules loaded" "$EL/tools/checks/check_sources_of_truth.sh" --root "$G1"

# The same for the SOT-2 library: without it M-3..M-8 would silently not run.
NS="$SPACED/no structure repo"
copy_checks "$NS"
rm "$NS/tools/checks/lib/rules_structure.sh"
assert_exit 2 "$NS/tools/checks/check_sources_of_truth.sh" --root "$G1"
assert_output_contains "cannot load rules library" "$NS/tools/checks/check_sources_of_truth.sh" --root "$G1"
assert_exit 2 "$NS/tools/checks/check_sources_of_truth.sh" --root "$FIXTURES/fail/M-6/Assets" --rules G-1

ES="$SPACED/empty structure repo"
copy_checks "$ES"
: > "$ES/tools/checks/lib/rules_structure.sh"
assert_exit 2 "$ES/tools/checks/check_sources_of_truth.sh" --root "$FIXTURES/fail/M-6/Assets"
assert_output_contains "no rules loaded" "$ES/tools/checks/check_sources_of_truth.sh" --root "$FIXTURES/fail/M-6/Assets"

BS="$SPACED/broken structure repo"
copy_checks "$BS"
printf 'rule_BROKEN() {\n' >> "$BS/tools/checks/lib/rules_structure.sh"
assert_exit 2 "$BS/tools/checks/check_sources_of_truth.sh" --root "$FIXTURES/fail/M-6/Assets"
assert_output_contains "cannot load rules library" "$BS/tools/checks/check_sources_of_truth.sh" --root "$FIXTURES/fail/M-6/Assets"

# ---------------------------------------------------------------------------------------------
# --all: scans unity/MotionBowling/Assets of the checkout the script lives in.
# ---------------------------------------------------------------------------------------------
MINI="$SPACED/mini repo"
copy_checks "$MINI"
MINI_CHECK="$MINI/tools/checks/check_sources_of_truth.sh"
assert_exit 0 in_dir "$WORK" "$MINI_CHECK" --all
assert_output_contains "nothing to scan: unity/MotionBowling/Assets not found" in_dir "$WORK" "$MINI_CHECK" --all
put "$MINI" "$UNITY/a.uss" "$BAD_USS"
assert_exit 1 in_dir "$WORK" "$MINI_CHECK" --all
assert_output_contains "G-1 Assets/_Project/a.uss:1 " in_dir "$WORK" "$MINI_CHECK" --all
put "$MINI" "$UNITY/Shaders/Flat.hlsl" 'float4 F() { return 0; }'
assert_output_contains "G-3b Assets/_Project/Shaders/Flat.hlsl " in_dir "$WORK" "$MINI_CHECK" --all
put "$MINI" "tools/checks/reviewed-shaders.txt" 'Assets/_Project/Shaders/Flat.hlsl # reviewed by test on 2026-10-18: flat'
assert_exit 0 output_lacks "G-3b" in_dir "$WORK" "$MINI_CHECK" --all

# ---------------------------------------------------------------------------------------------
# --staged: reads the index, only unity/MotionBowling/Assets, never a deleted file (Review Focus 2).
# ---------------------------------------------------------------------------------------------
R="$SPACED/staged repo"
new_repo "$R"
put "$R" "$UNITY/a.uss" "$BAD_USS"
git -C "$R" add -A
assert_exit 1 in_dir "$R" "$CHECK" --staged
assert_output_contains "G-1 Assets/_Project/a.uss:1 " in_dir "$R" "$CHECK" --staged

# The index wins over the working tree, in both directions.
put "$R" "$UNITY/a.uss" '.a { color: red; }'
assert_exit 1 in_dir "$R" "$CHECK" --staged
git -C "$R" add -A
assert_exit 0 in_dir "$R" "$CHECK" --staged
put "$R" "$UNITY/a.uss" "$BAD_USS"
assert_exit 0 in_dir "$R" "$CHECK" --staged

# A path with spaces, and files outside the Unity Assets folder or in excluded folders.
put "$R" "$UNITY/My Styles/b b.uss" "$BAD_USS"
git -C "$R" add -A
assert_output_contains "G-1 Assets/_Project/My Styles/b b.uss:1 " in_dir "$R" "$CHECK" --staged

# Deleted file: exit 0 and nothing printed.
D="$SPACED/deleted repo"
new_repo "$D"
put "$D" "$UNITY/a.uss" "$BAD_USS"
git -C "$D" add -A
git -C "$D" commit -q -m init
git -C "$D" rm -q "$UNITY/a.uss"
assert_exit 0 in_dir "$D" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$D" "$CHECK" --staged

# Renamed file: a clean one passes, a banned one is still found under its new name.
N="$SPACED/renamed repo"
new_repo "$N"
put "$N" "$UNITY/clean.uss" '.a { color: red; }'
put "$N" "$UNITY/bad.uss" "$BAD_USS"
git -C "$N" add -A
git -C "$N" commit -q -m init
git -C "$N" mv "$UNITY/clean.uss" "$UNITY/clean2.uss"
assert_exit 0 in_dir "$N" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$N" "$CHECK" --staged
git -C "$N" mv "$UNITY/bad.uss" "$UNITY/bad2.uss"
assert_exit 1 in_dir "$N" "$CHECK" --staged
assert_output_contains "G-1 Assets/_Project/bad2.uss:1 " in_dir "$N" "$CHECK" --staged

# Nothing in scope staged: a README, a banned fixture under tools/, the Tooling folder, a PNG.
O="$SPACED/outside repo"
new_repo "$O"
put "$O" README.md 'hello'
put "$O" "tools/checks/fixtures/fail/G-1/Assets/_Project/Styles/Gradient.uss" "$BAD_USS"
put "$O" "unity/MotionBowling/Packages/x/y.uss" "$BAD_USS"
put "$O" "$UNITY/Modules/Tooling/Editor/Banned.uss" "$BAD_USS"
put "$O" "$UNITY/Art/a.png" 'not really a png'
git -C "$O" add -A
assert_exit 0 in_dir "$O" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$O" "$CHECK" --staged
# Staging only README.md.
O2="$SPACED/readme repo"
new_repo "$O2"
put "$O2" README.md 'hello'
git -C "$O2" add README.md
assert_exit 0 in_dir "$O2" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$O2" "$CHECK" --staged

# Nothing staged at all, and not a git repo.
E="$SPACED/empty repo"
new_repo "$E"
assert_exit 0 in_dir "$E" "$CHECK" --staged
mkdir -p "$WORK/not a repo"
assert_exit 2 in_dir "$WORK/not a repo" env GIT_CEILING_DIRECTORIES="$WORK" "$CHECK" --staged

# G-3b reads the reviewed list from the index, not from the working tree.
S="$SPACED/shader repo"
new_repo "$S"
put "$S" "$UNITY/Shaders/Flat.hlsl" 'float4 F() { return 0; }'
git -C "$S" add -A
assert_output_contains "G-3b Assets/_Project/Shaders/Flat.hlsl " in_dir "$S" "$CHECK" --staged
put "$S" "tools/checks/reviewed-shaders.txt" 'Assets/_Project/Shaders/Flat.hlsl # reviewed by test on 2026-10-18: flat'
assert_exit 1 in_dir "$S" "$CHECK" --staged
git -C "$S" add tools/checks/reviewed-shaders.txt
assert_exit 0 in_dir "$S" "$CHECK" --staged

# SOT-2 rules in staged mode: the staged copy of a file is what is judged.
SR="$SPACED/structure repo"
new_repo "$SR"
put "$SR" "$UNITY/Scripts/Loose.cs" 'public class Loose { }'
put "$SR" "$UNITY/Modules/Gameplay/Runtime/Core/Wrong.cs" 'public class Right { }'
put "$SR" "$UNITY/Modules/Gameplay/Runtime/Core/Finder.cs" 'public class Finder { void F() { FindObjectOfType<Finder>(); } }'
git -C "$SR" add -A
assert_exit 1 in_dir "$SR" "$CHECK" --staged
assert_output_contains "M-6 Assets/_Project/Scripts/Loose.cs " in_dir "$SR" "$CHECK" --staged
assert_output_contains "M-3 Assets/_Project/Modules/Gameplay/Runtime/Core/Wrong.cs:1 " in_dir "$SR" "$CHECK" --staged
assert_output_contains "M-5 Assets/_Project/Modules/Gameplay/Runtime/Core/Finder.cs:1 " in_dir "$SR" "$CHECK" --staged
# Staged (ruling R25): G-2 and M-6 read all of Assets, the G rules skip Tooling, the M rules and FMOD
# are as everywhere.
TS="$SPACED/tooling staged repo"
new_repo "$TS"
put "$TS" "$UNITY/Modules/Tooling/Editor/Patterns.cs" 'public class Patterns { object _g = new Gradient(); }'
put "$TS" "$UNITY/Modules/Tooling/Loose.cs" 'public class Loose { }'
put "$TS" "unity/MotionBowling/Assets/Other/Gradients.cs" 'public class Gradients { object _g = new Gradient(); }'
put "$TS" "unity/MotionBowling/Assets/Plugins/FMOD/Vendor.cs" 'public class Vendor { object _g = new Gradient(); }'
git -C "$TS" add -A
assert_exit 1 in_dir "$TS" "$CHECK" --staged
assert_output_contains "G-2 Assets/Other/Gradients.cs:1 " in_dir "$TS" "$CHECK" --staged
assert_output_contains "M-6 Assets/Other/Gradients.cs " in_dir "$TS" "$CHECK" --staged
assert_output_contains "M-6 Assets/_Project/Modules/Tooling/Loose.cs " in_dir "$TS" "$CHECK" --staged
assert_exit 0 output_lacks "G-2 Assets/_Project/Modules/Tooling" in_dir "$TS" "$CHECK" --staged
assert_exit 0 output_lacks "FMOD" in_dir "$TS" "$CHECK" --staged
# A long file: 301 lines only warn (exit 0), 401 fail.
LONG="$SPACED/long repo"
new_repo "$LONG"
put "$LONG" "$UNITY/Modules/Gameplay/Runtime/Core/Long.cs" "$(gen_lines 301)"
git -C "$LONG" add -A
assert_exit 0 in_dir "$LONG" "$CHECK" --staged
assert_output_contains "WARN M-4 Assets/_Project/Modules/Gameplay/Runtime/Core/Long.cs 301 lines (soft limit 300)" in_dir "$LONG" "$CHECK" --staged
put "$LONG" "$UNITY/Modules/Gameplay/Runtime/Core/Long.cs" "$(gen_lines 401)"
git -C "$LONG" add -A
assert_exit 1 in_dir "$LONG" "$CHECK" --staged
assert_output_contains "M-4 Assets/_Project/Modules/Gameplay/Runtime/Core/Long.cs 401 lines (max 400)" in_dir "$LONG" "$CHECK" --staged

# M-8 folder completeness in staged mode (ruling R6): every UI component folder with a staged file
# is judged by the files it has in the index, not only by the staged ones.
UI="$UNITY/Modules/UI/Runtime/Components"
UI_MSG="Assets/_Project/Modules/UI/Runtime/Components"
# put_component <repo> <name>: a complete component folder.
put_component() {
  put "$1" "$UI/$2/$2.uxml" '<ui:UXML xmlns:ui="UnityEngine.UIElements" />'
  put "$1" "$UI/$2/$2.uss" '.a { flex-grow: 1; }'
  put "$1" "$UI/$2/$2.cs" "public class ${2// /_} { }"
}

# A complete folder is committed; staging a change to just Badge.cs passes.
CR="$SPACED/component repo"
new_repo "$CR"
put_component "$CR" Badge
put_component "$CR" "My Badge"
git -C "$CR" add -A
git -C "$CR" commit -q -m init
put "$CR" "$UI/Badge/Badge.cs" 'public class Badge { int _x; }'
git -C "$CR" add "$UI/Badge/Badge.cs"
assert_exit 0 in_dir "$CR" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$CR" "$CHECK" --staged
# A folder name with a space (only M-8 is run: a type can't be named like "My Badge.cs").
put "$CR" "$UI/My Badge/My Badge.cs" 'public class My_Badge { int _x; }'
git -C "$CR" add "$UI/My Badge/My Badge.cs"
assert_exit 0 prints_nothing in_dir "$CR" "$CHECK" --staged --rules M-8
git -C "$CR" rm -q -f "$UI/My Badge/My Badge.uss"
assert_output_contains "M-8 $UI_MSG/My Badge missing My Badge.uss " in_dir "$CR" "$CHECK" --staged --rules M-8
# Deleting a file from the folder (staged) fails, even though no .cs/.uss/.uxml is staged otherwise.
git -C "$CR" reset -q
git -C "$CR" rm -q "$UI/Badge/Badge.uss"
assert_exit 1 in_dir "$CR" "$CHECK" --staged
assert_output_contains "M-8 $UI_MSG/Badge missing Badge.uss " in_dir "$CR" "$CHECK" --staged
# The index is what counts: a file deleted only in the working tree is still there for the commit.
git -C "$CR" reset -q --hard
rm "$CR/$UI/Badge/Badge.uss"
put "$CR" "$UI/Badge/Badge.cs" 'public class Badge { int _y; }'
git -C "$CR" add "$UI/Badge/Badge.cs"
assert_exit 0 in_dir "$CR" "$CHECK" --staged
# Renaming the folder leaves Chip.* missing.
git -C "$CR" reset -q --hard
git -C "$CR" mv "$UI/Badge" "$UI/Chip"
assert_exit 1 in_dir "$CR" "$CHECK" --staged
assert_output_contains "M-8 $UI_MSG/Chip missing Chip.uxml " in_dir "$CR" "$CHECK" --staged
# Moving a file out of the folder (git shows a rename; the old folder is still touched).
git -C "$CR" reset -q --hard
mkdir -p "$CR/$UNITY/Styles"
git -C "$CR" mv "$UI/Badge/Badge.uss" "$UNITY/Styles/Badge.uss"
assert_exit 1 in_dir "$CR" "$CHECK" --staged
assert_output_contains "M-8 $UI_MSG/Badge missing Badge.uss " in_dir "$CR" "$CHECK" --staged
# Deleting the whole folder is fine.
git -C "$CR" reset -q --hard
git -C "$CR" rm -r -q "$UI/Badge"
assert_exit 0 in_dir "$CR" "$CHECK" --staged
assert_exit 0 prints_nothing in_dir "$CR" "$CHECK" --staged

# A new folder with only Badge.uxml and Badge.cs fails, with the missing file named.
NB="$SPACED/new component repo"
new_repo "$NB"
put "$NB" "$UI/Badge/Badge.uxml" '<ui:UXML xmlns:ui="UnityEngine.UIElements" />'
put "$NB" "$UI/Badge/Badge.cs" 'public class Badge { }'
git -C "$NB" add -A
assert_exit 1 in_dir "$NB" "$CHECK" --staged
assert_output_contains "M-8 $UI_MSG/Badge missing Badge.uss " in_dir "$NB" "$CHECK" --staged
# A file that exists only in the working tree doesn't complete the folder.
put "$NB" "$UI/Badge/Badge.uss" '.a { flex-grow: 1; }'
assert_exit 1 in_dir "$NB" "$CHECK" --staged
git -C "$NB" add "$UI/Badge/Badge.uss"
assert_exit 0 in_dir "$NB" "$CHECK" --staged

# Folders that no staged file touches are not judged: an incomplete folder already in the index
# stays quiet while a complete one is changed. (M-8 in --all mode still reports it.)
OLD="$SPACED/old component repo"
new_repo "$OLD"
put "$OLD" "$UI/Old/Old.cs" 'public class Old { }'
put_component "$OLD" Fresh
git -C "$OLD" add -A
git -C "$OLD" commit -q -m init
put "$OLD" "$UI/Fresh/Fresh.cs" 'public class Fresh { int _x; }'
git -C "$OLD" add "$UI/Fresh/Fresh.cs"
assert_exit 0 prints_nothing in_dir "$OLD" "$CHECK" --staged
put "$OLD" "$UI/Old/Old.cs" 'public class Old { int _x; }'
git -C "$OLD" add "$UI/Old/Old.cs"
assert_exit 1 in_dir "$OLD" "$CHECK" --staged
assert_output_contains "M-8 $UI_MSG/Old missing Old.uxml " in_dir "$OLD" "$CHECK" --staged
assert_exit 0 output_lacks "Fresh" in_dir "$OLD" "$CHECK" --staged

# A staged color literal in a .uss is found; the same value in Tokens.uss is not.
CL="$SPACED/color repo"
new_repo "$CL"
put "$CL" "$UNITY/Modules/UI/Runtime/Styles/Tokens.uss" ':root { --color-primary: #FF0000; }'
put "$CL" "$UNITY/Modules/UI/Runtime/Styles/Hud.uss" '.a { color: var(--color-primary); }'
git -C "$CL" add -A
assert_exit 0 prints_nothing in_dir "$CL" "$CHECK" --staged
put "$CL" "$UNITY/Modules/UI/Runtime/Styles/Hud.uss" '.a { color: #FF0000; }'
git -C "$CL" add -A
assert_exit 1 in_dir "$CL" "$CHECK" --staged
assert_output_contains "M-8 Assets/_Project/Modules/UI/Runtime/Styles/Hud.uss:1 " in_dir "$CL" "$CHECK" --staged

# ---------------------------------------------------------------------------------------------
# install-hooks.sh and the pre-commit hook.
# ---------------------------------------------------------------------------------------------
H="$SPACED/hook repo"
new_repo "$H"
copy_checks "$H"
put "$H" README.md 'hello'
git -C "$H" add README.md
git -C "$H" commit -q -m init

# Installing twice is harmless: the hook is overwritten with the same content.
assert_exit 0 in_dir "$H" bash tools/checks/install-hooks.sh
cp "$H/.git/hooks/pre-commit" "$WORK/pre-commit.first"
assert_exit 0 in_dir "$H" bash tools/checks/install-hooks.sh
assert_exit 0 test -x "$H/.git/hooks/pre-commit"
assert_exit 0 cmp "$H/.git/hooks/pre-commit" "$WORK/pre-commit.first"

# Script present: a violation blocks the commit, a clean change passes.
put "$H" "$UNITY/a.uss" "$BAD_USS"
git -C "$H" add "$UNITY/a.uss"
assert_exit 1 git -C "$H" commit -q -m bad
assert_output_contains "G-1 Assets/_Project/a.uss:1 " git -C "$H" commit -q -m bad
assert_exit 0 test "$(git -C "$H" rev-list --count HEAD)" = 1
put "$H" "$UNITY/a.uss" '.a { color: red; }'
git -C "$H" add "$UNITY/a.uss"
assert_exit 0 git -C "$H" commit -q -m clean
assert_exit 0 test "$(git -C "$H" rev-list --count HEAD)" = 2

# A warning (a 301-line file, M-4) is printed but doesn't block the commit; a 401-line file does.
LONG_CS="$UNITY/Modules/Gameplay/Runtime/Core/Long.cs"
put "$H" "$LONG_CS" "$(gen_lines 401)"
git -C "$H" add "$LONG_CS"
assert_exit 1 git -C "$H" commit -q -m toolong
assert_output_contains "M-4 Assets/_Project/Modules/Gameplay/Runtime/Core/Long.cs 401 lines (max 400)" git -C "$H" commit -q -m toolong
put "$H" "$LONG_CS" "$(gen_lines 301)"
git -C "$H" add "$LONG_CS"
assert_output_contains "WARN M-4 Assets/_Project/Modules/Gameplay/Runtime/Core/Long.cs 301 lines" git -C "$H" commit -q -m warned
assert_exit 0 test "$(git -C "$H" rev-list --count HEAD)" = 3

# Script absent (a checkout without the checks, such as main before the merge): skip and pass.
rm "$H/tools/checks/check_sources_of_truth.sh"
put "$H" "$UNITY/a.uss" "$BAD_USS"
git -C "$H" add "$UNITY/a.uss"
assert_output_contains "source-of-truth checks not present in this checkout; skipping" git -C "$H" commit -q -m skipped
assert_exit 0 test "$(git -C "$H" rev-list --count HEAD)" = 4

# A git worktree shares the hooks directory of its main checkout (git rev-parse --git-path hooks).
W="$SPACED/main checkout"
WT="$SPACED/linked worktree"
new_repo "$W"
put "$W" README.md 'hello'
git -C "$W" add README.md
git -C "$W" commit -q -m init
git -C "$W" worktree add -q -b linked "$WT"
copy_checks "$WT"
assert_exit 0 in_dir "$WT" bash tools/checks/install-hooks.sh
assert_exit 0 test -x "$W/.git/hooks/pre-commit"
put "$WT" "$UNITY/a.uss" "$BAD_USS"
git -C "$WT" add "$UNITY/a.uss"
assert_exit 1 git -C "$WT" commit -q -m bad
assert_output_contains "G-1 Assets/_Project/a.uss:1 " git -C "$WT" commit -q -m bad
# The main checkout has no checks (copy_checks went to the worktree only), so its hook skips.
put "$W" README.md 'changed'
git -C "$W" add README.md
assert_output_contains "source-of-truth checks not present in this checkout; skipping" git -C "$W" commit -q -m ok

finish
