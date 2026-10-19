#!/usr/bin/env bash
# SOT-2 text rules M-3, M-4, M-5, M-6 and M-8 (foundation spec, section 8.2). M-1, M-2 and M-7 need
# the Unity editor and live in Bowling.Tooling.Editor.
# Sourced by check_sources_of_truth.sh, which provides list_files, run_awk, scan_tokens, tree_files,
# touched_files and TMP. FMOD (vendor code) is already left out of every file list. Tooling is
# judged like any other module: only the G rules skip it.
# The last statement of this file registers the rules, so a lib that fails to load is an error.

# ---------------------------------------------------------------------------------------------
# M-3: one public top-level type per .cs file, and its name is the file name (without .cs).
#
# A public top-level type is a public class, struct, interface, enum, record or delegate, with any
# modifiers (sealed static abstract readonly partial unsafe ref ...), that is not inside another
# type: every brace open around it belongs to a namespace. That is depth 1 for a block namespace and
# depth 0 for a file-scoped "namespace X;". For a generic type the name before "<" is compared. Files
# with no public top-level type pass.
#
# awk reads the code with comments and string and char literals cut out (strip), walks it in
# pieces between { } ; and keeps a stack of open braces, each one a namespace or something else.
# What a simple awk can't do:
#   - $"..." strings are read as plain strings: a nested quote inside a {hole} can confuse it
#   - raw string literals ("""...""") are not understood (Unity 6 compiles C# 9, which has none)
#   - #if/#else branches are not evaluated: a type declared in both branches counts twice
#   - a declaration split over lines ("public" on one, "class" on the next) is not seen
#   - an attribute list that runs over several lines and ends on the declaration's line hides it
#   - type names with non-ASCII letters end at the first such byte
# ---------------------------------------------------------------------------------------------
IFS= read -r -d '' AWK_M3 <<'EOF' || true
# Returns the code of one line: comments, strings and chars cut out (replaced by a space). Comments
# and strings that run over several lines are tracked in in_block (/* */) and in_verb (@"...").
function strip(s,    out, c, n, t) {
  out = ""
  while (s != "") {
    if (in_block) {
      t = index(s, "*/")
      if (t == 0) return out
      s = substr(s, t + 2)
      in_block = 0
      out = out " "
      continue
    }
    if (in_verb) {
      t = index(s, "\"")
      if (t == 0) return out
      if (substr(s, t + 1, 1) == "\"") { s = substr(s, t + 2); continue }
      s = substr(s, t + 1)
      in_verb = 0
      continue
    }
    if (!match(s, /["'\/]/)) return out s
    out = out substr(s, 1, RSTART - 1)
    c = substr(s, RSTART, 1)
    s = substr(s, RSTART)
    if (c == "/") {
      n = substr(s, 2, 1)
      if (n == "/") return out
      if (n == "*") { in_block = 1; s = substr(s, 3); continue }
      out = out "/"
      s = substr(s, 2)
    } else if (c == "'") {
      if (match(s, /^'([^'\\]|\\.[^']*)'/)) s = substr(s, RLENGTH + 1)
      else s = substr(s, 2)
      out = out " "
    } else {
      t = substr(out, length(out) - 1)
      if (t ~ /@$/ || t == "@$") {
        in_verb = 1
        s = substr(s, 2)
      } else if (match(s, /^"([^"\\]|\\.)*"/)) {
        s = substr(s, RLENGTH + 1)
      } else {
        s = ""
      }
      out = out " "
    }
  }
  return out
}

# One piece of code between { } ; at namespace level: remember it if it declares a public type.
function declaration(seg, ln,    head, kw, mods, after, p, pre, name) {
  gsub(/\[[^]]*\]/, " ", seg)
  if (!match(seg, /^[[:space:]]*(((public|internal|private|protected|sealed|static|abstract|readonly|partial|unsafe|ref|new|file)[[:space:]]+)*)(class|struct|interface|enum|record|delegate)([^A-Za-z0-9_]|$)/)) return
  head = substr(seg, 1, RLENGTH)
  sub(/[^A-Za-z0-9_]$/, "", head)
  match(head, /[a-z]+$/)
  kw = substr(head, RSTART)
  mods = substr(head, 1, RSTART - 1)
  if (mods !~ /(^|[[:space:]])public([[:space:]]|$)/) return
  after = substr(seg, length(head) + 1)
  name = ""
  if (kw == "delegate") {
    # public delegate <return type> Name<T>(...): the name is the last word before the "("
    p = index(after, "(")
    if (p > 0) {
      pre = substr(after, 1, p - 1)
      sub(/[[:space:]]+$/, "", pre)
      while (pre ~ /<[^<>]*>$/) sub(/<[^<>]*>$/, "", pre)
      if (match(pre, /@?[A-Za-z_][A-Za-z0-9_]*$/)) name = substr(pre, RSTART, RLENGTH)
    }
  } else {
    if (kw == "record") sub(/^[[:space:]]+(struct|class)[[:space:]]+/, " ", after)
    if (match(after, /^[[:space:]]*@?[A-Za-z_][A-Za-z0-9_]*/)) name = substr(after, 1, RLENGTH)
  }
  sub(/^[[:space:]]*@?/, "", name)
  ntypes++
  tname[ntypes] = name
  tline[ntypes] = ln
}

function begin_file() {
  file = FILENAME
  base = FILENAME
  sub(/^.*\//, "", base)
  sub(/[.]cs$/, "", base)
  ntypes = 0
  sp = 0
  nonns = 0
  pending_ns = 0
  in_block = 0
  in_verb = 0
}

function end_file(    i) {
  if (file == "") return
  if (ntypes > 1) {
    for (i = 2; i <= ntypes; i++)
      print rule " Assets/" file ":" tline[i] " more than one public top-level type: '" tname[i] "' (the first is '" tname[1] "' on line " tline[1] "); one public type per file"
  } else if (ntypes == 1 && tname[1] != "" && tname[1] != base) {
    print rule " Assets/" file ":" tline[1] " public type '" tname[1] "' must be in a file named " tname[1] ".cs"
  }
}

FNR == 1 { end_file(); begin_file() }
{
  line = $0
  if (FNR == 1) sub(/^\357\273\277/, "", line)
  if (!in_block && !in_verb && line ~ /^[[:space:]]*#/) next
  rest = strip(line)
  while (rest != "") {
    if (match(rest, /[{};]/)) {
      seg = substr(rest, 1, RSTART - 1)
      ch = substr(rest, RSTART, 1)
      rest = substr(rest, RSTART + 1)
    } else {
      seg = rest
      ch = ""
      rest = ""
    }
    if (nonns == 0) {
      if (seg ~ /(^|[^A-Za-z0-9_.@])namespace[[:space:]]+[A-Za-z_@]/) pending_ns = 1
      else declaration(seg, FNR)
    }
    if (ch == "{") {
      sp++
      if (pending_ns) { kind[sp] = "n"; pending_ns = 0 }
      else { kind[sp] = "o"; nonns++ }
    } else if (ch == "}") {
      if (sp > 0) { if (kind[sp] == "o") nonns--; sp-- }
    } else if (ch == ";") {
      pending_ns = 0
    }
  }
}
END { end_file() }
EOF

rule_M_3() {
  run_awk project cs -v rule=M-3 "$AWK_M3"
}

# ---------------------------------------------------------------------------------------------
# M-4: file length. Over 400 lines fails, over 300 only warns. The count is the number of lines in
# the file as an editor shows it, so a last line without a newline counts (plain wc -l would not).
# ---------------------------------------------------------------------------------------------
IFS= read -r -d '' AWK_M4 <<'EOF' || true
function report() {
  if (file == "") return
  if (n > 400) print rule " Assets/" file " " n " lines (max 400)"
  else if (n > 300) print "WARN " rule " Assets/" file " " n " lines (soft limit 300)"
}
FNR == 1 { report(); file = FILENAME }
{ n = FNR }
END { report() }
EOF

rule_M_4() {
  run_awk project cs -v rule=M-4 "$AWK_M4"
}

# ---------------------------------------------------------------------------------------------
# M-5: scene lookups and singletons in runtime code: .cs under Modules/<Module>/Runtime/, except
# Modules/App/ (the composition root). Full-line // comments are skipped, like G-2.
# The singleton pattern is "static <Type> Instance", also with readonly, a qualified or nullable
# type and a generic one without spaces (Foo<Bar>). The word ends at Instance, so InstanceCount and
# Instances pass.
# ---------------------------------------------------------------------------------------------
M5_PATTERNS='FindObjectOfType
FindFirstObjectByType
FindAnyObjectByType
GameObject[.]Find
Resources[.]Load
static[[:space:]]+(readonly[[:space:]]+)?[A-Za-z_][A-Za-z0-9_<>,.?]*[[:space:]]+Instance([^A-Za-z0-9_]|$)'

rule_M_5() {
  scan_tokens M-5 runtime cs 1 "runtime code must not use a scene lookup or a singleton" "$M5_PATTERNS"
}

# ---------------------------------------------------------------------------------------------
# M-6: every .cs file under Assets/ is inside a module asmdef folder: a folder inside
# Modules/<Module>/ that holds an .asmdef, or a folder below one (a .cs anywhere else would compile
# into the default Assembly-CSharp). The asmdef decides, not the folder name; where an asmdef may sit
# is M-1's job. Tooling counts as a module. FMOD (vendor) is not listed at all, and a .cs in a folder
# whose name ends in ~ is skipped: Unity doesn't import such folders (Samples~/, Documentation~/).
# The asmdefs come from tree_files, so in staged mode an asmdef counts when it is in the index. Staged
# mode judges only the staged .cs files, so staging just the deletion of an asmdef is not seen here;
# --all (verify.sh step 1) and the editor rule M-1 report it.
# ---------------------------------------------------------------------------------------------
IFS= read -r -d '' AWK_M6 <<'EOF' || true
BEGIN { while ((getline d < dirs) > 0) asmdef_dir[d] = 1 }
index($0, "~/") > 0 { next }
{
  d = $0
  while (sub(/\/[^\/]*$/, "", d)) if (d in asmdef_dir) next
  print "M-6 Assets/" $0 " code must live in a module asmdef folder (a folder in Assets/_Project/Modules/<Module>/ with an .asmdef, or below one): move it into the module's Runtime/, Editor/, Tests/EditMode/ or Tests/PlayMode/ folder, and give that folder its asmdef if it has none"
}
EOF

rule_M_6() {
  local files
  files="$(tree_files _Project/Modules)" || return 1
  printf '%s\n' "$files" | sed -n -E 's#^(_Project/Modules/[^/]+(/.*)?)/[^/]+[.]asmdef$#\1#p' \
    | sort -u > "$TMP/m6-asmdef-dirs.txt"
  list_files assets cs | awk -v dirs="$TMP/m6-asmdef-dirs.txt" "$AWK_M6"
}

# ---------------------------------------------------------------------------------------------
# M-8: UI components and colors.
#   - a UI component folder Modules/UI/Runtime/Components/<Name>/ has <Name>.uxml, <Name>.uss and
#     <Name>.cs. A folder is judged whole, from tree_files (in staged mode: the index), when
#     touched_files has a file in it. Files in sub-folders don't count, and neither do files that sit
#     directly in Components/.
#   - a color literal (#hex with 3, 4, 6 or 8 digits at a word boundary, rgb( or rgba() in the value
#     of a declaration of any .uss, except Modules/UI/Runtime/Styles/Tokens.uss. Selectors are not
#     read, so an ID selector like #bad { is never a color. Comments and strings are cut out first.
#     A // is a comment unless it follows a colon, which keeps url(project://...) whole.
# ---------------------------------------------------------------------------------------------
M8_COMPONENTS="_Project/Modules/UI/Runtime/Components"
M8_TOKENS="_Project/Modules/UI/Runtime/Styles/Tokens.uss"

IFS= read -r -d '' AWK_M8_COLORS <<'EOF' || true
function strip(s,    out, c, n, t) {
  out = ""
  while (s != "") {
    if (in_comment) {
      t = index(s, "*/")
      if (t == 0) return out
      s = substr(s, t + 2)
      in_comment = 0
      out = out " "
      continue
    }
    if (!match(s, /["'\/]/)) return out s
    out = out substr(s, 1, RSTART - 1)
    c = substr(s, RSTART, 1)
    s = substr(s, RSTART)
    if (c == "/") {
      n = substr(s, 2, 1)
      if (n == "*") { in_comment = 1; s = substr(s, 3); continue }
      if (n == "/" && substr(out, length(out)) != ":") return out
      out = out "/"
      s = substr(s, 2)
    } else {
      if (c == "\"") t = match(s, /^"([^"\\]|\\.)*"/)
      else t = match(s, /^'([^'\\]|\\.)*'/)
      if (t == 0) return out " "
      s = substr(s, RLENGTH + 1)
      out = out " "
    }
  }
  return out
}

function literal(text, ln) {
  print rule " Assets/" FILENAME ":" ln " color literal '" text "' outside Styles/Tokens.uss: use a var(--token) from Tokens.uss"
}

# v is part of a declaration's value
function check(v, ln,    t, lv, tok, off, pc, len) {
  lv = tolower(v)
  t = lv
  off = 0
  while (match(t, /rgba?[(]/)) {
    pc = (RSTART + off > 1) ? substr(lv, RSTART + off - 1, 1) : ""
    if (pc !~ /[a-z0-9_-]/) { literal(substr(v, RSTART + off, RLENGTH), ln); break }
    off += RSTART + RLENGTH - 1
    t = substr(t, RSTART + RLENGTH)
  }
  t = v
  while (match(t, /#[A-Za-z0-9_]+/)) {
    tok = substr(t, RSTART + 1, RLENGTH - 1)
    len = length(tok)
    if ((len == 3 || len == 4 || len == 6 || len == 8) && tok ~ /^[0-9A-Fa-f]+$/) literal("#" tok, ln)
    t = substr(t, RSTART + RLENGTH)
  }
}

FNR == 1 { skip = (FILENAME == exempt); in_comment = 0; depth = 0; invalue = 0 }
skip { next }
{
  rest = strip($0)
  while (rest != "") {
    if (match(rest, /[{};]/)) {
      seg = substr(rest, 1, RSTART - 1)
      ch = substr(rest, RSTART, 1)
      rest = substr(rest, RSTART + 1)
    } else {
      seg = rest
      ch = ""
      rest = ""
    }
    if (depth > 0) {
      # inside { }: the value of a declaration starts after its first colon and ends at ; or }
      if (!invalue) {
        t = index(seg, ":")
        if (t > 0) { invalue = 1; seg = substr(seg, t + 1) }
        else seg = ""
      }
      if (seg != "") check(seg, FNR)
    }
    if (ch == "{") depth++
    else if (ch == "}") { if (depth > 0) depth--; invalue = 0 }
    else if (ch == ";") invalue = 0
  }
}
EOF

rule_M_8() {
  local name ext files
  touched_files "$M8_COMPONENTS" \
    | sed -n "s|^$M8_COMPONENTS/\\([^/][^/]*\\)/.*|\\1|p" | sort -u > "$TMP/m8-folders.txt"
  while IFS= read -r name <&3; do
    files="$(tree_files "$M8_COMPONENTS/$name")" || return 1
    [ -n "$files" ] || continue
    for ext in uxml uss cs; do
      printf '%s\n' "$files" | grep -Fxq "$M8_COMPONENTS/$name/$name.$ext" \
        || echo "M-8 Assets/$M8_COMPONENTS/$name missing $name.$ext (a UI component folder needs <Name>.uxml, <Name>.uss and <Name>.cs)"
    done
  done 3< "$TMP/m8-folders.txt"
  run_awk assets uss -v rule=M-8 -v exempt="$M8_TOKENS" "$AWK_M8_COLORS"
}

ALL_RULES="$ALL_RULES M-3 M-4 M-5 M-6 M-8"
