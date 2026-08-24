#!/usr/bin/env python3
"""Rewrite C# 12 constructs to C# 11 so the managed OpenCvSharp compiles in
Unity (whose bundled Roslyn supports up to C# 11, not collection expressions
or primary constructors).

Transforms (all observed patterns in OpenCvSharp 4.11.0 managed sources):
  1. Primary constructors:
       class Ptr(IntPtr ptr) : OpenCvSharp.Ptr(ptr)  ->  classic ctor + base(ptr)
       readonly struct LSDParam(a = 1, b = 2) { public readonly T F = a; ... }
                                                        ->  fields + ctor
       public class X; / interface X; / struct X;      ->  public X { }
  2. Collection expressions -> typed array / span creation:
       [MatType.A, MatType.B]        -> new MatType[] { MatType.A, MatType.B }
       new InputArray([a, b])        -> new InputArray(new[] { a, b })
       (c is null) ? [] : arr        -> (c is null) ? new string[0] : arr
       cond ? x : []                 -> cond ? x : Span<T>.Empty   (Mat.cs:4146)
       FilterByLabels(s, d, [v])     -> FilterByLabels(s, d, new int[] { v })
       float[] X = [ ... ];          -> float[] X = new float[] { ... };
       char[] fourcc = [ ... ];      -> char[] fourcc = new char[] { ... };
       int[] GetParameters() => [..] -> int[] GetParameters() => new int[] { .. };
       prms ??= [];                  -> prms ??= new int[0];
       return []; (in T[] method)    -> return new T[0];
       various exact inline forms

Usage: cs11ify.py <scripts_dir>
"""

import re
import sys
from pathlib import Path

PRIMARY_CTOR = re.compile(
    r"(?m)^(\s*)(internal|public|private|protected)\s+(new\s+)?class\s+Ptr"
    r"\s*\(\s*IntPtr\s+ptr\s*\)\s*:\s*OpenCvSharp\.Ptr\s*\(\s*ptr\s*\)\s*\n"
    r"\1\{\n"
)


def fix_primary_ctor(m: re.Match) -> str:
    indent, access, newkw = m.group(1), m.group(2), m.group(3) or ""
    return (
        f"{indent}{access} {newkw}class Ptr : OpenCvSharp.Ptr\n"
        f"{indent}{{\n"
        f"{indent}    public Ptr(IntPtr ptr) : base(ptr) {{ }}\n"
    )


# Empty-type `;` shorthand: `public class X;` -> `public class X { }`
EMPTY_TYPE = re.compile(
    r"(\bpublic\s+(?:readonly\s+)?(?:class|struct|interface|enum)\s+[^\n{;]+?)\s*;"
)

# `readonly struct LSDParam(params) { public readonly T F = p; ... }`
LSD_PARAM = re.compile(
    r"public readonly struct LSDParam\((.*?)\)\s*\{(.*?)\}", re.S
)


def fix_lsd_param(text: str) -> str:
    m = LSD_PARAM.search(text)
    if not m:
        return text
    params_block, body_block = m.group(1), m.group(2)
    params = []
    for line in params_block.split(","):
        line = line.strip()
        if not line:
            continue
        mm = re.match(r"(\w+(?:\s*<[^>]*>)?)\s+(\w+)\s*=\s*(.+)", line)
        if mm:
            params.append((mm.group(1), mm.group(2), mm.group(3)))
    fields = []
    for line in body_block.strip().split("\n"):
        line = line.strip()
        mm = re.match(
            r"public readonly (\w+(?:\s*<[^>]*>)?)\s+(\w+)\s*=\s*(\w+)\s*;", line
        )
        if mm:
            fields.append((mm.group(1), mm.group(2), mm.group(3)))
    field_decls = "\n".join(f"    public readonly {t} {f};" for t, f, s in fields)
    ctor_params = ", ".join(f"{t} {n} = {d}" for t, n, d in params)
    ctor_body = "\n".join(f"        {f} = {s};" for t, f, s in fields)
    repl = (
        "public readonly struct LSDParam\n"
        "{\n"
        f"{field_decls}\n"
        "\n"
        f"    public LSDParam({ctor_params})\n"
        "    {\n"
        f"{ctor_body}\n"
        "    }\n"
        "}"
    )
    return text[: m.start()] + repl + text[m.end():]


METHOD_RET_ARRAY = re.compile(r"([\w.]+(?:<[^>]+>)?)((\[\])+)\s+\w+\s*(?:<[^>]*>)?\s*\(")


def fix_return_array(text: str) -> tuple[str, int]:
    """Convert `return [];` inside an `X[] Method()` to `return new X[0];`."""
    lines = text.split("\n")
    cur = None
    base = None
    n = 0
    out = []
    for line in lines:
        m = METHOD_RET_ARRAY.search(line)
        if m:
            base = m.group(1)
            cur = m.group(2)
        if line.strip() == "return [];" and cur:
            line = f"return new {base}[0]{cur[2:]};"
            cur = None
            n += 1
        out.append(line)
    return "\n".join(out), n


COLLECTION_REPLACEMENTS = [
    ("prms ??= [];", "prms ??= new int[0];"),
    ("private readonly List<string> loadedAssemblies = [];",
     "private readonly List<string> loadedAssemblies = new List<string>();"),
    ("AdditionalPaths = [];", "AdditionalPaths = new List<string>();"),
    ("public IList<string> AdditionalPaths { get; } = [];",
     "public IList<string> AdditionalPaths { get; } = new List<string>();"),
    ("var additionalPathsArray = additionalPaths?.ToArray() ?? [];",
     "var additionalPathsArray = additionalPaths?.ToArray() ?? new string[0];"),
    ("public List<string> Warnings { get; } = [];",
     "public List<string> Warnings { get; } = new List<string>();"),
    ("Warnings = [];", "Warnings = new List<string>();"),
    ("uint[] mag01 = [0x0U, /*MATRIX_A*/ 0x9908b0dfU];",
     "uint[] mag01 = new uint[] { 0x0U, /*MATRIX_A*/ 0x9908b0dfU };"),
    ("straightQrCode = [];", "straightQrCode = new Mat[0];"),
    ("var layersTypesArray = layersTypes as string[] ?? layersTypes?.ToArray() ?? [];",
     "var layersTypesArray = layersTypes as string[] ?? layersTypes?.ToArray() ?? new string[0];"),
]


def fix_collection_exprs(text: str) -> tuple[str, int]:
    orig = text
    n = 0

    # empty-type `;` shorthand
    text, c = EMPTY_TYPE.subn(r"\1 { }", text)
    n += c

    # dictionary values: [MatType.A, MatType.B] -> new MatType[] { MatType.A, MatType.B }
    text, c = re.subn(r"\[MatType\.(.*?)\]", r"new MatType[] { MatType.\1 }", text)
    n += c

    # new InputArray([a, b]) -> new InputArray(new[] { a, b })
    text, c = re.subn(
        r"new InputArray\(\[(.*?)\]\)", r"new InputArray(new[] { \1 })", text
    )
    n += c

    # (cond) ? [] : arr  ->  (cond) ? new string[0] : arr
    text, c = re.subn(r"\? \[\]\s*:", r"? new string[0] :", text)
    n += c

    # cond ? x : []  (Span<T> empty)  ->  : Span<T>.Empty;
    text, c = re.subn(r": \[\]\s*;", r": Span<T>.Empty;", text)
    n += c

    # FilterByLabels(s, d, [v]) -> FilterByLabels(s, d, new int[] { v })
    text, c = re.subn(
        r"FilterByLabels\(([^)]*?),\s*\[([^\]]*)\]\)",
        r"FilterByLabels(\1, new int[] { \2 })",
        text,
    )
    n += c

    # multi-line array initializers
    text, c = re.subn(
        r"(float\[\] (?:DefaultPeopleDetector|DaimlerPeopleDetector)\s*=\s*)\[\n(.*?)\n\s*\](\s*;)",
        lambda m: f'{m.group(1)}\n    new float[] {{\n{m.group(2)}\n    }};',
        text,
        flags=re.S,
    )
    n += c
    text, c = re.subn(
        r"(char\[\] fourcc\s*=\s*)\[\n(.*?)\n\s*\](\s*;)",
        lambda m: f'{m.group(1)}\n    new char[] {{\n{m.group(2)}\n    }};',
        text,
        flags=re.S,
    )
    n += c
    text, c = re.subn(
        r"(int\[\] GetParameters\(\)\s*=>\s*)\[\n(.*?)\n\s*\](\s*;)",
        lambda m: f'{m.group(1)}\n    new int[] {{\n{m.group(2)}\n    }};',
        text,
        flags=re.S,
    )
    n += c

    return text, n


def main() -> int:
    root = Path(sys.argv[1]).resolve()
    if not root.is_dir():
        print(f"not a directory: {root}", file=sys.stderr)
        return 1

    ctor_total = 0
    lsd_total = 0
    array_total = 0
    coll_total = 0
    extra_total = 0
    for f in root.rglob("*.cs"):
        text = f.read_text(encoding="utf-8")
        orig = text

        text, n = PRIMARY_CTOR.subn(fix_primary_ctor, text)
        ctor_total += n

        before = text
        text = fix_lsd_param(text)
        if text != before:
            lsd_total += 1

        for old, new in COLLECTION_REPLACEMENTS:
            c = text.count(old)
            if c:
                coll_total += c
                text = text.replace(old, new)

        # System.Runtime.CompilerServices.Unsafe is not referenced by Unity's
        # Assembly-CSharp; Unsafe.AsRef<T>(p) == *(T*)p (valid ref expression).
        text, c = re.subn(r"Unsafe\.AsRef<(\w+)>\((.*?)\)", r"*(\1*)(\2)", text)
        coll_total += c

        text, n = fix_return_array(text)
        array_total += n

        text, n = fix_collection_exprs(text)
        extra_total += n

        if text != orig:
            f.write_text(text, encoding="utf-8")

    print(f"primary ctors rewritten: {ctor_total}")
    print(f"LSDParam primary ctors expanded: {lsd_total}")
    print(f"return [] -> new T[0] rewritten: {array_total}")
    print(f"collection expressions rewritten: {coll_total}")
    print(f"additional collection exprs rewritten: {extra_total}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
