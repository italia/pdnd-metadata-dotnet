#!/usr/bin/env python3
"""Run the shared conformance corpus against this implementation.

Exit code 0 when every vector passes. This is the reference conformance runner: an
implementation in any language needs roughly this much code.
"""
import json
import sys
from pathlib import Path

from pdnd_metadata import extract

CORPUS = Path(__file__).resolve().parents[2] / "vectors" / "vectors.json"


def main() -> int:
    doc = json.loads(CORPUS.read_text())
    failures: list[str] = []
    passed = 0

    for vector in doc["vectors"]:
        vid = vector["id"]
        try:
            actual = extract(vector["request"]["headers"])
        except Exception as exc:  # noqa: BLE001 - extraction must never raise
            failures.append(f"{vid}: raised {type(exc).__name__}: {exc}")
            continue

        problems = []
        for key, expected in vector["expect"].items():
            if key == "__absent__":
                for absent in expected:
                    if absent in actual:
                        problems.append(f"{absent!r} should be absent, got {actual[absent]!r}")
            elif actual.get(key) != expected:
                problems.append(f"{key!r} expected {expected!r}, got {actual.get(key)!r}")

        if problems:
            failures.append(f"{vid}: " + "; ".join(problems))
        else:
            passed += 1

    print(f"{passed}/{len(doc['vectors'])} vectors passed")
    for failure in failures:
        print(f"  FAIL {failure}")

    return 0 if not failures else 1


if __name__ == "__main__":
    sys.exit(main())
