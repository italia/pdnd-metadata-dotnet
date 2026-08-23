#!/usr/bin/env python3
"""Structural validation for the PDND metadata conformance vectors.

Checks that every vector is well formed, that identifiers are unique, and that the encoded
tokens decode to valid JSON. It does not run an implementation: that is what the language
specific conformance runners do. This script exists so that a malformed corpus is caught in
CI rather than by whoever adopts it.
"""
import base64
import json
import sys
from pathlib import Path

CORPUS = Path(__file__).parent / "vectors.json"
REQUIRED = {"id", "group", "description", "request", "expect"}


def b64url_decode(segment: str) -> bytes:
    padding = "=" * (-len(segment) % 4)
    return base64.urlsafe_b64decode(segment + padding)


def main() -> int:
    errors: list[str] = []
    doc = json.loads(CORPUS.read_text())

    for field in ("specVersion", "vectors"):
        if field not in doc:
            errors.append(f"corpus is missing the '{field}' field")

    seen: set[str] = set()

    for index, vector in enumerate(doc.get("vectors", [])):
        label = vector.get("id", f"index {index}")

        missing = REQUIRED - vector.keys()
        if missing:
            errors.append(f"{label}: missing fields {sorted(missing)}")
            continue

        if vector["id"] in seen:
            errors.append(f"{label}: duplicate id")
        seen.add(vector["id"])

        headers = vector["request"].get("headers")
        if not isinstance(headers, dict) or not headers:
            errors.append(f"{label}: request.headers must be a non-empty object")
            continue

        expect = vector["expect"]
        if not isinstance(expect, dict) or not expect:
            errors.append(f"{label}: expect must be a non-empty object")

        absent = expect.get("__absent__", [])
        if not isinstance(absent, list):
            errors.append(f"{label}: __absent__ must be a list")
        for key in absent:
            if key in expect:
                errors.append(f"{label}: key '{key}' is both expected and absent")

        # Tokens in the adversarial group are malformed on purpose.
        if vector["group"] == "adversarial":
            continue

        for name, value in headers.items():
            token = value[len("Bearer "):] if value.startswith("Bearer ") else value
            if token.count(".") not in (1, 2, 4):
                continue
            for segment in token.split(".")[:2]:
                if not segment:
                    continue
                try:
                    json.loads(b64url_decode(segment))
                except Exception as exc:
                    errors.append(f"{label}: header '{name}' segment does not decode to JSON ({exc})")

    if errors:
        print(f"{len(errors)} problem(s) found:", file=sys.stderr)
        for error in errors:
            print(f"  {error}", file=sys.stderr)
        return 1

    groups = sorted({v["group"] for v in doc["vectors"]})
    print(f"OK: {len(doc['vectors'])} vectors, {len(groups)} groups ({', '.join(groups)})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
