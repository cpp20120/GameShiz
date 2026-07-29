#!/usr/bin/env python3
"""Audit and conservatively split C# files that contain multiple top-level types."""

from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass
from pathlib import Path


TYPE_KEYWORDS = {"class", "record", "interface", "enum", "struct"}
RECORD_MODIFIERS = {"class", "struct"}
DECLARATION_MODIFIERS = {
    "abstract",
    "file",
    "internal",
    "new",
    "partial",
    "private",
    "protected",
    "public",
    "readonly",
    "ref",
    "sealed",
    "static",
    "unsafe",
}


@dataclass(frozen=True)
class Token:
    value: str
    start: int
    end: int


@dataclass(frozen=True)
class TypeDeclaration:
    name: str
    start: int
    end: int
    namespace_depth: int


def is_identifier_start(char: str) -> bool:
    return char == "_" or char.isalpha()


def is_identifier_part(char: str) -> bool:
    return char == "_" or char.isalnum()


def skip_quoted(source: str, index: int) -> int:
    quote = source[index]
    index += 1
    while index < len(source):
        if source[index] == "\\":
            index += 2
            continue
        if source[index] == quote:
            return index + 1
        index += 1
    return len(source)


def skip_verbatim_string(source: str, index: int) -> int:
    index += 2
    while index < len(source):
        if source[index] != '"':
            index += 1
            continue
        if index + 1 < len(source) and source[index + 1] == '"':
            index += 2
            continue
        return index + 1
    return len(source)


def skip_raw_string(source: str, index: int) -> int:
    quote_length = 0
    while index + quote_length < len(source) and source[index + quote_length] == '"':
        quote_length += 1
    end_marker = '"' * max(quote_length, 3)
    end = source.find(end_marker, index + quote_length)
    return len(source) if end < 0 else end + len(end_marker)


def tokenize(source: str) -> list[Token]:
    tokens: list[Token] = []
    index = 0
    while index < len(source):
        char = source[index]
        if char.isspace():
            index += 1
            continue
        if char == "/" and index + 1 < len(source) and source[index + 1] == "/":
            newline = source.find("\n", index + 2)
            index = len(source) if newline < 0 else newline + 1
            continue
        if char == "/" and index + 1 < len(source) and source[index + 1] == "*":
            end = source.find("*/", index + 2)
            index = len(source) if end < 0 else end + 2
            continue
        if char == "#" and (index == 0 or source[index - 1] == "\n"):
            newline = source.find("\n", index + 1)
            index = len(source) if newline < 0 else newline + 1
            continue

        if char == "@" and index + 1 < len(source) and source[index + 1] == '"':
            index = skip_verbatim_string(source, index)
            continue
        if char == '"':
            if source.startswith('"""', index):
                index = skip_raw_string(source, index)
            else:
                index = skip_quoted(source, index)
            continue
        if char == "'":
            index = skip_quoted(source, index)
            continue
        if char == "$":
            string_start = index + 1
            while string_start < len(source) and source[string_start] == "@":
                string_start += 1
            if string_start < len(source) and source[string_start] == '"':
                index = skip_verbatim_string(source, string_start - 1) if string_start > index + 1 else skip_quoted(source, string_start)
                continue

        if is_identifier_start(char) or (char == "@" and index + 1 < len(source) and is_identifier_start(source[index + 1])):
            start = index
            index += 1
            while index < len(source) and is_identifier_part(source[index]):
                index += 1
            tokens.append(Token(source[start:index], start, index))
            continue

        tokens.append(Token(char, index, index + 1))
        index += 1
    return tokens


def matching_brace(tokens: list[Token], opening_index: int) -> int | None:
    depth = 0
    for index in range(opening_index, len(tokens)):
        if tokens[index].value == "{":
            depth += 1
        elif tokens[index].value == "}":
            depth -= 1
            if depth == 0:
                return index
    return None


def declaration_name(tokens: list[Token], keyword_index: int) -> str | None:
    index = keyword_index + 1
    if tokens[keyword_index].value == "record" and index < len(tokens) and tokens[index].value in RECORD_MODIFIERS:
        index += 1
    if index >= len(tokens):
        return None
    candidate = tokens[index].value
    return candidate[1:] if candidate.startswith("@") else candidate if is_identifier_start(candidate[:1]) else None


def declaration_start(tokens: list[Token], keyword_index: int) -> int:
    index = keyword_index - 1
    while index >= 0:
        value = tokens[index].value
        if value in DECLARATION_MODIFIERS:
            index -= 1
            continue
        if value == "]":
            square_depth = 1
            index -= 1
            while index >= 0:
                if tokens[index].value == "]":
                    square_depth += 1
                elif tokens[index].value == "[":
                    square_depth -= 1
                    if square_depth == 0:
                        index -= 1
                        break
                index -= 1
            continue
        break
    return tokens[index + 1].start if index + 1 < keyword_index else tokens[keyword_index].start


def find_declarations(source: str) -> list[TypeDeclaration]:
    tokens = tokenize(source)
    declarations: list[TypeDeclaration] = []
    stack: list[str] = []
    pending_namespace_depth: int | None = None
    pending_type_depth: int | None = None
    pending_type_index: int | None = None
    namespace_count = 0

    for index, token in enumerate(tokens):
        if token.value in TYPE_KEYWORDS and all(kind == "namespace" for kind in stack):
            name = declaration_name(tokens, index)
            if name is not None:
                declarations.append(TypeDeclaration(name, declaration_start(tokens, index), -1, len(stack)))
                pending_type_depth = len(stack)
                pending_type_index = len(declarations) - 1

        if token.value == "namespace":
            pending_namespace_depth = len(stack)
            namespace_count += 1
        elif token.value == "{":
            if pending_type_depth == len(stack) and pending_type_index is not None:
                stack.append("type")
                closing_index = matching_brace(tokens, index)
                if closing_index is not None:
                    declaration = declarations[pending_type_index]
                    declarations[pending_type_index] = TypeDeclaration(
                        declaration.name,
                        declaration.start,
                        tokens[closing_index].end,
                        declaration.namespace_depth,
                    )
                pending_type_depth = None
                pending_type_index = None
            elif pending_namespace_depth == len(stack):
                stack.append("namespace")
                pending_namespace_depth = None
            else:
                stack.append("other")
        elif token.value == "}":
            if stack:
                stack.pop()
        elif token.value == ";":
            if pending_type_depth == len(stack) and pending_type_index is not None:
                declaration = declarations[pending_type_index]
                declarations[pending_type_index] = TypeDeclaration(
                    declaration.name, declaration.start, token.end, declaration.namespace_depth
                )
                pending_type_depth = None
                pending_type_index = None
            if pending_namespace_depth == len(stack):
                pending_namespace_depth = None

    complete = [declaration for declaration in declarations if declaration.end >= 0]
    if namespace_count > 1:
        return complete
    return complete


def has_unsafe_prefix(source: str, first_start: int) -> bool:
    prefix = source[:first_start]
    for line in prefix.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith(("//", "/*", "*", "#")):
            continue
        if stripped.startswith(("using ", "global using ", "extern alias ", "namespace ", "[")):
            continue
        return True
    return False


def safe_to_split(source: str, declarations: list[TypeDeclaration]) -> bool:
    if len(declarations) < 2 or any(declaration.end < 0 for declaration in declarations):
        return False
    if sum(token.value == "namespace" for token in tokenize(source)) > 1:
        return False
    if any(
        line.lstrip().startswith(("#if", "#elif", "#else", "#endif", "#region", "#endregion", "#pragma"))
        for line in source.splitlines()
    ):
        return False
    if len({declaration.namespace_depth for declaration in declarations}) != 1:
        return False
    return not has_unsafe_prefix(source, declarations[0].start)


def split_file(path: Path, source: str, declarations: list[TypeDeclaration], apply: bool) -> tuple[bool, str]:
    if not safe_to_split(source, declarations):
        return False, "manual review required"
    names = [declaration.name for declaration in declarations]
    if len(names) != len(set(names)):
        return False, "duplicate type name/partial declaration"

    prefix = source[: declarations[0].start]
    suffix = source[declarations[-1].end :]
    targets = [path.with_name(f"{name}.cs") for name in names]
    conflicts = [target for target in targets if target.exists() and target != path]
    if conflicts:
        return False, "target already exists: " + ", ".join(target.name for target in conflicts)

    if not apply:
        return True, " -> " + ", ".join(target.name for target in targets)

    original_target_content: str | None = None
    for declaration, target in zip(declarations, targets):
        content = prefix + source[declaration.start : declaration.end] + suffix
        if target == path:
            original_target_content = content
        else:
            target.write_text(content, encoding="utf-8")
    if original_target_content is None:
        path.unlink()
    else:
        path.write_text(original_target_content, encoding="utf-8")
    return True, " -> " + ", ".join(target.name for target in targets)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path("games"))
    parser.add_argument("--apply", action="store_true", help="write safe splits and remove the original files")
    parser.add_argument("--fail-on-multi", action="store_true", help="return non-zero when any multi-type file remains")
    args = parser.parse_args()

    files = sorted(args.root.rglob("*.cs"))
    files = [path for path in files if "bin" not in path.parts and "obj" not in path.parts]
    multi_type = 0
    safe_count = 0
    manual_count = 0

    for path in files:
        source = path.read_text(encoding="utf-8")
        declarations = find_declarations(source)
        if len(declarations) < 2:
            continue
        multi_type += 1
        safe, detail = split_file(path, source, declarations, args.apply)
        if safe:
            safe_count += 1
            print(f"{'split' if args.apply else 'would split'}: {path}: {detail}")
        else:
            manual_count += 1
            print(f"manual: {path}: {detail}; types={', '.join(d.name for d in declarations)}")

    print(f"layout: {multi_type} multi-type file(s), {safe_count} safe, {manual_count} manual")
    if args.fail_on_multi and (manual_count if args.apply else multi_type):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
