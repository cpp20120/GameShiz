#!/usr/bin/env python3
"""Fail unless every line in games/**/Domain/** is covered by Cobertura."""

from __future__ import annotations

import argparse
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("coverage_file", type=Path)
    args = parser.parse_args()

    root = ET.parse(args.coverage_file).getroot()
    total = 0
    covered = 0
    partial: list[tuple[str, list[str]]] = []
    classes = 0

    for class_element in root.findall(".//class"):
        filename = class_element.attrib.get("filename", "").replace("\\", "/")
        if not filename.startswith("games/") or "/Domain/" not in filename:
            continue

        classes += 1
        lines = class_element.find("lines")
        if lines is None:
            continue

        total += len(lines)
        uncovered = [
            line.attrib["number"]
            for line in lines
            if int(line.attrib.get("hits", "0")) == 0
        ]
        covered += len(lines) - len(uncovered)
        if uncovered:
            partial.append((filename, uncovered))

    if not classes:
        print("No games/**/Domain/** classes found in coverage report.", file=sys.stderr)
        return 1

    print(f"Domain coverage: {covered}/{total} lines ({covered / total:.2%}), classes={classes}")
    if partial:
        for filename, lines in partial:
            print(f"  {filename}: uncovered lines {', '.join(lines)}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
