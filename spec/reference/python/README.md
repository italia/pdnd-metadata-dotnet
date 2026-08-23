# Python reference implementation

Not a published package. No PyPI release, no users, no dependencies beyond the standard library.

It exists for one reason. A specification with a single implementation is that implementation's
documentation: every question the prose forgot to answer is answered silently by the code, and
the gap is invisible from the inside. Writing a second implementation from the document alone is
the only reliable way to find those gaps.

It found one on its first run. See `../../CHANGELOG.md`, entry 1.1.0.

## Running

```
python3 conformance.py
```

Reads `../../vectors/vectors.json` and reports how many vectors passed. Exit code 0 when all do.

## Using it as a template

`conformance.py` is deliberately short. An implementation in any language needs roughly this
much: read the corpus, feed each vector's headers to the extractor, compare the produced keys
against `expect`, and check that nothing under `__absent__` was produced.

A header value is either a string or an array of strings, the latter when the vector exercises a
repeated header.
