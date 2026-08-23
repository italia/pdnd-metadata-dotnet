#!/usr/bin/env python3
"""Generates the documentation diagrams.

Every figure is produced from this one file so that grid, palette, stroke weight, and
typography cannot drift between them. Run it after editing:

    python3 assets/diagrams/build.py

Design rules, applied uniformly:

- 8 pixel grid. Every coordinate is a multiple of 4, most are multiples of 8.
- One accent (institutional blue), one neutral ramp, one semantic colour used sparingly.
- Three type sizes only: eyebrow 9.5 uppercase tracked, title 13 medium, caption 10.5.
- Identifiers are monospace, prose is sans. Never mixed within one string.
- Section labels sit outside their container, never inside it.
- Solid border means the request passes through it. Dashed means it is out of scope here.
"""

from __future__ import annotations

import pathlib

OUT = pathlib.Path(__file__).parent

LIGHT = {
    "ink": "#0F1720", "muted": "#5F6C7B", "faint": "#8A97A6",
    "rule": "#DCE3EA", "line": "#A9B4C0",
    "surface": "#FFFFFF", "sunken": "#F5F7F9",
    "accent": "#0066CC", "accentFill": "#EAF2FB", "accentLine": "#B4D2F0",
    "flag": "#A65A18", "flagFill": "#FCF4EA", "flagLine": "#E6CFAE",
    "good": "#17693C", "goodFill": "#EDF5F0", "goodLine": "#B5D4C2",
}

DARK = {
    "ink": "#E7EAEE", "muted": "#9AA6B2", "faint": "#71808F",
    "rule": "#2B333C", "line": "#5A6773",
    "surface": "#141920", "sunken": "#1A2029",
    "accent": "#5AA2F0", "accentFill": "#14243A", "accentLine": "#2D5688",
    "flag": "#D9A055", "flagFill": "#2A2114", "flagLine": "#5C4726",
    "good": "#5DBE87", "goodFill": "#132619", "goodLine": "#2B573A",
}
DARK["accentLine"] = "#2D5688"

SANS = "ui-sans-serif, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"
MONO = "ui-monospace, SFMono-Regular, 'SF Mono', Menlo, Consolas, monospace"


def head(w: int, h: int, title: str, desc: str, c: dict) -> str:
    return (
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} {h}" width="{w}" height="{h}" '
        f'role="img" aria-labelledby="figTitle figDesc" font-family="{SANS}">'
        f'<title id="figTitle">{title}</title><desc id="figDesc">{desc}</desc>'
        f'<defs>'
        f'<marker id="tip" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="7" markerHeight="7" '
        f'orient="auto-start-reverse">'
        f'<path d="M1 1L6.5 4L1 7" fill="none" stroke="{c["line"]}" stroke-width="1.25" '
        f'stroke-linecap="round" stroke-linejoin="round"/></marker>'
        f'</defs>'
    )


def panel(x, y, w, h, c, fill="surface", stroke="rule", r=8, dashed=False):
    d = ' stroke-dasharray="4 4"' if dashed else ""
    return (f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{c[fill]}" '
            f'stroke="{c[stroke]}" stroke-width="1"{d}/>')


def eyebrow(x, y, s, c, col="faint", anchor="start"):
    return (f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-size="9.5" font-weight="500" '
            f'letter-spacing="0.1em" fill="{c[col]}" dominant-baseline="central">{s.upper()}</text>')


def title(x, y, s, c, col="ink", anchor="middle", size=13):
    return (f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-size="{size}" font-weight="500" '
            f'fill="{c[col]}" dominant-baseline="central">{s}</text>')


def caption(x, y, s, c, col="muted", anchor="middle", size=10.5):
    return (f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-size="{size}" '
            f'fill="{c[col]}" dominant-baseline="central">{s}</text>')


def mono(x, y, s, c, col="accent", anchor="start", size=10.5):
    return (f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-size="{size}" font-family="{MONO}" '
            f'fill="{c[col]}" dominant-baseline="central">{s}</text>')


def flow(x1, y1, x2, y2, c, dashed=False, col="line"):
    d = ' stroke-dasharray="4 4"' if dashed else ""
    return (f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{c[col]}" stroke-width="1.25" '
            f'marker-end="url(#tip)"{d}/>')


def rule(x1, y, x2, c):
    return f'<line x1="{x1}" y1="{y}" x2="{x2}" y2="{y}" stroke="{c["rule"]}" stroke-width="1"/>'


# --------------------------------------------------------------------------- architecture
def architecture(c: dict) -> str:
    W, H = 960, 448
    s = [head(W, H, "Where Pdnd.Metadata sits",
              "The library runs as ASP.NET Core middleware inside the provider service, after the "
              "reverse proxy and before validation, and never participates in the trust decision.", c)]

    # PDND platform, upstream and out of scope
    s += [panel(32, 56, 176, 72, c, "sunken", "rule", dashed=True),
          eyebrow(32, 40, "platform", c),
          title(120, 82, "PDND", c),
          caption(120, 102, "issues the voucher", c)]

    # Consumer
    s += [panel(32, 184, 176, 72, c, "sunken"),
          eyebrow(32, 168, "consumer", c),
          title(120, 210, "Fruitore", c),
          caption(120, 230, "calls the e-service", c)]

    s.append(flow(120, 136, 120, 176, c, dashed=True))
    s.append(caption(132, 156, "voucher", c, col="faint", anchor="start", size=10))

    # Edge
    s += [panel(240, 184, 128, 72, c, "sunken"),
          eyebrow(240, 168, "edge", c),
          title(304, 210, "Reverse proxy", c),
          caption(304, 230, "TLS, forwarded", c)]

    s.append(flow(216, 220, 232, 220, c))
    s.append(flow(376, 220, 392, 220, c))

    # Provider service container
    s += [panel(400, 136, 528, 216, c, "surface", "rule"),
          eyebrow(400, 120, "provider service · asp.net core", c)]

    # Middleware strip
    s.append(caption(424, 164, "Middleware pipeline", c, col="faint", anchor="start", size=10))

    s += [panel(424, 180, 144, 64, c, "accentFill", "accentLine", r=6),
          title(496, 202, "Pdnd.Metadata", c, col="accent"),
          caption(496, 222, "extract, normalize", c, col="accent")]

    s += [panel(592, 180, 144, 64, c, "sunken", "rule", r=6, dashed=True),
          title(664, 202, "Validation", c),
          caption(664, 222, "not in this library", c, col="flag")]

    s += [panel(760, 180, 144, 64, c, "sunken", "rule", r=6),
          title(832, 202, "Your endpoints", c),
          caption(832, 222, "business logic", c)]

    s.append(flow(576, 212, 584, 212, c))
    s.append(flow(744, 212, 752, 212, c))

    s.append(rule(424, 272, 904, c))

    # Diagnostics
    s += [panel(424, 288, 144, 48, c, "accentFill", "accentLine", r=6),
          title(496, 312, "Diagnostics", c, col="accent", size=12)]
    s.append(caption(592, 302, "Activity tags and a request counter,", c, anchor="start", size=10))
    s.append(caption(592, 320, "read by any OpenTelemetry exporter", c, anchor="start", size=10))

    s.append(flow(496, 248, 496, 280, c, dashed=True))

    # Legend
    s.append(rule(32, 376, 928, c))
    lx = 32
    for label, dashed in (("request path", False), ("out of scope for this library", True)):
        d = ' stroke-dasharray="4 4"' if dashed else ""
        s.append(f'<line x1="{lx}" y1="400" x2="{lx+28}" y2="400" stroke="{c["line"]}" '
                 f'stroke-width="1.25"{d}/>')
        s.append(caption(lx + 38, 400, label, c, col="faint", anchor="start", size=10))
        lx += 38 + int(len(label) * 5.4) + 40

    s.append(caption(32, 428, "The library never rejects a request and never verifies a signature. "
                              "It runs before validation so the snapshot is available to it.",
                     c, col="faint", anchor="start", size=10))
    s.append("</svg>")
    return "".join(s)


# --------------------------------------------------------------------------- pipeline
def pipeline(c: dict) -> str:
    W, H = 960, 400
    s = [head(W, H, "Extraction pipeline",
              "Inbound headers are decoded into a canonical snapshot of stable keys, consumed by "
              "logs and traces.", c)]

    s.append(eyebrow(32, 40, "inbound headers", c))
    s.append(panel(32, 56, 216, 264, c, "sunken"))
    rows = ["Authorization", "Agid-JWT-Signature", "Agid-JWT-Tracking-Evidence",
            "DPoP", "Digest / Content-Digest"]
    for i, r in enumerate(rows):
        y = 76 + i * 48
        s.append(panel(48, y, 184, 36, c, "surface", "rule", r=6))
        s.append(mono(60, y + 18, r, c, col="muted", size=10))

    s.append(flow(256, 188, 296, 188, c))

    s.append(eyebrow(312, 40, "this library", c, col="accent"))
    s.append(panel(312, 56, 224, 264, c, "accentFill", "accentLine"))
    for i, (t, d) in enumerate([("Decode", "JOSE header and payload"),
                                ("Normalize", "canonical key names"),
                                ("Bound", "length, count, values")]):
        y = 96 + i * 64
        s.append(title(424, y, t, c, col="accent"))
        s.append(caption(424, y + 18, d, c, col="accent", size=10))
    s.append(rule(336, 268, 512, c))
    s.append(caption(424, 292, "no signature verified, no request rejected", c, col="flag", size=10))

    s.append(flow(544, 188, 584, 188, c))

    s.append(eyebrow(600, 40, "canonical snapshot", c))
    s.append(panel(600, 56, 264, 264, c, "sunken"))
    keys = [("pdnd.voucher.organizationId", "org-001"),
            ("pdnd.voucher.purposeId", "purpose-001"),
            ("pdnd.signature.signed_headers", "digest, content-type"),
            ("pdnd.dpop.htm", "POST"),
            ("correlation.id", "b7c1…")]
    for i, (k, v) in enumerate(keys):
        y = 84 + i * 48
        s.append(mono(616, y, k, c, size=10))
        s.append(mono(616, y + 16, v, c, col="muted", size=10))

    s.append(flow(872, 156, 904, 132, c))
    s.append(flow(872, 220, 904, 244, c))
    s.append(caption(912, 132, "logs", c, col="faint", anchor="start", size=10))
    s.append(caption(912, 244, "traces", c, col="faint", anchor="start", size=10))

    s.append(rule(32, 352, 928, c))
    s.append(caption(32, 376, "Absent claims produce no key. Validation happens elsewhere: in your "
                              "authentication layer, a gateway, or dedicated middleware.",
                     c, col="faint", anchor="start", size=10))
    s.append("</svg>")
    return "".join(s)


# --------------------------------------------------------------------------- keyspace
def keyspace(c: dict) -> str:
    W, H = 960, 400
    s = [head(W, H, "Canonical key namespace",
              "The four families of canonical keys and the part of the request each is derived from.", c)]

    groups = [
        ("transport", [("http.*", "method, scheme, host, path, query"),
                       ("net.*", "addresses and ports")]),
        ("correlation", [("trace.*", "W3C Trace Context"),
                         ("correlation.id", "request identifier")]),
        ("pdnd tokens", [("pdnd.voucher.*", "voucher claims"),
                         ("pdnd.trackingEvidence.*", "tracking evidence claims"),
                         ("pdnd.dpop.*", "proof of possession")]),
        ("integrity", [("pdnd.digest.*", "RFC 3230 body digest"),
                       ("pdnd.content_digest.*", "RFC 9530 body digest"),
                       ("pdnd.signature.*", "ModI signature claims")]),
    ]

    x = 32
    for name, rows in groups:
        w = 216
        h = 32 + len(rows) * 56
        s.append(eyebrow(x, 56, name, c))
        s.append(panel(x, 72, w, h, c, "sunken"))
        for i, (key, desc) in enumerate(rows):
            y = 88 + i * 56
            s.append(panel(x + 12, y, w - 24, 44, c, "surface", "rule", r=6))
            s.append(mono(x + 26, y + 16, key, c, size=10.5))
            s.append(caption(x + 26, y + 32, desc, c, anchor="start", size=10))
        x += w + 16

    s.append(eyebrow(32, 32, "canonical key namespace", c, col="muted"))
    s.append(rule(32, 336, 928, c))
    s.append(caption(32, 360, "Keys are lowercase and hierarchical. An absent claim produces no key, "
                              "so absence and emptiness are indistinguishable by design.",
                     c, col="faint", anchor="start", size=10))
    s.append(caption(32, 378, "The single exception is metadata.truncated, emitted when header "
                              "capture stopped at a configured bound.",
                     c, col="faint", anchor="start", size=10))
    s.append("</svg>")
    return "".join(s)


# --------------------------------------------------------------------------- conformance
def conformance(c: dict) -> str:
    W, H = 960, 384
    s = [head(W, H, "Conformance model",
              "The specification and its vector corpus form a contract that independent "
              "implementations satisfy and anyone can verify.", c)]

    s.append(eyebrow(336, 48, "the contract", c, col="accent"))
    s.append(panel(336, 64, 288, 88, c, "accentFill", "accentLine"))
    s.append(title(480, 92, "Specification 1.1.0", c, col="accent"))
    s.append(caption(480, 116, "canonical vocabulary · 30 vectors", c, col="accent", size=10))

    for x in (152, 480, 808):
        s.append(flow(480, 160, x, 208, c))

    impls = [
        (72, "Pdnd.Metadata", "C# .NET", "30 / 30", True),
        (400, "reference", "Python", "30 / 30", True),
        (728, "your implementation", "any language", "run the corpus", False),
    ]
    for x, name, lang, result, ok in impls:
        s.append(panel(x, 216, 160, 96, c, "goodFill" if ok else "sunken",
                       "goodLine" if ok else "rule", dashed=not ok))
        s.append(title(x + 80, 244, name, c))
        s.append(caption(x + 80, 264, lang, c, size=10))
        s.append(caption(x + 80, 288, result, c, col="good" if ok else "faint", size=10.5))

    s.append(rule(32, 336, 928, c))
    s.append(caption(32, 360, "Conformance is a verifiable claim, not a certification. There is no "
                              "authority and no badge: anyone can run the corpus and check it.",
                     c, col="faint", anchor="start", size=10))
    s.append("</svg>")
    return "".join(s)


FIGURES = {
    "architecture": architecture,
    "pipeline": pipeline,
    "keyspace": keyspace,
    "conformance": conformance,
}

if __name__ == "__main__":
    for name, fn in FIGURES.items():
        (OUT / f"{name}.svg").write_text(fn(LIGHT) + "\n")
        (OUT / f"{name}-dark.svg").write_text(fn(DARK) + "\n")
        print(f"wrote {name}.svg and {name}-dark.svg")
