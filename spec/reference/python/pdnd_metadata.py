"""Reference implementation of the PDND metadata extraction specification.

Written from `spec/PDND_METADATA_SCHEMA.md` and `spec/README.md` alone, deliberately without
consulting the .NET source. Its purpose is not to be used in production: it is to prove that
the specification is complete enough for an independent implementation, and to expose the
places where it is not.

Public API:

    extract(headers: Mapping[str, str | Sequence[str]]) -> dict[str, str]

Returns canonical keys mapped to their values. Absent claims produce no key.
"""

from __future__ import annotations

import base64
import binascii
import json
from collections.abc import Mapping, Sequence
from typing import Any

__all__ = ["extract", "SPEC_VERSION"]

SPEC_VERSION = "1.0.0"

MAX_TOKEN_LENGTH = 16384

# Header names carrying a compact JWS, mapped to the canonical key prefix for their claims.
_TOKEN_HEADERS: list[tuple[tuple[str, ...], str]] = [
    (("agid-jwt-tracking-evidence", "agid-jwt-trackingevidence"), "pdnd.trackingEvidence"),
    (("dpop",), "pdnd.dpop"),
    (("agid-jwt-signature",), "pdnd.signature"),
]

# Claims promoted from the JOSE header, in the order they appear in the specification.
_JOSE_CLAIMS = ("alg", "kid", "typ")

_PAYLOAD_CLAIMS: dict[str, tuple[str, ...]] = {
    "pdnd.voucher": (
        "iss", "sub", "aud", "jti", "iat", "nbf", "exp",
        "purposeId", "clientId", "client_id", "organizationId", "dnonce",
    ),
    "pdnd.trackingEvidence": ("iss", "sub", "jti", "aud", "iat", "nbf", "exp"),
    "pdnd.dpop": ("htm", "htu", "jti", "iat", "exp", "ath", "nonce"),
    "pdnd.signature": ("iss", "sub", "jti", "aud", "iat", "exp"),
}


def _first(value: str | Sequence[str]) -> str | None:
    if isinstance(value, str):
        return value
    for item in value:
        return item
    return None


def _b64url_decode(segment: str) -> bytes | None:
    """Decode one Base64Url segment, rejecting anything outside the alphabet."""
    if not segment:
        return None

    allowed = set("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_")
    if any(character not in allowed for character in segment):
        return None

    padded = segment + "=" * (-len(segment) % 4)
    try:
        return base64.urlsafe_b64decode(padded)
    except (binascii.Error, ValueError):
        return None


def _decode_token(token: str) -> tuple[dict[str, Any], dict[str, Any]] | None:
    """Decode the JOSE header and payload of a compact token. Never raises."""
    if not token or len(token) > MAX_TOKEN_LENGTH:
        return None

    segments = token.split(".")
    if len(segments) not in (2, 3, 5):
        return None

    header_bytes = _b64url_decode(segments[0])
    if header_bytes is None:
        return None

    # A token is decoded as a unit. For a two or three segment token the payload must decode
    # as well: emitting header claims from a token whose payload is unreadable would tell a
    # consumer that a well-formed token was present when it was not.
    payload_bytes: bytes | None = None
    if len(segments) != 5:
        payload_bytes = _b64url_decode(segments[1])
        if payload_bytes is None:
            return None

    try:
        header = json.loads(header_bytes.decode("utf-8"))
    except (UnicodeDecodeError, json.JSONDecodeError):
        return None

    if not isinstance(header, dict):
        return None

    payload: dict[str, Any] = {}
    if payload_bytes is not None:
        try:
            decoded = json.loads(payload_bytes.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            return None
        if not isinstance(decoded, dict):
            return None
        payload = decoded

    return header, payload


def _scalar(value: Any) -> str | None:
    """Render a claim value as a string, or None when it is not a scalar."""
    if isinstance(value, bool):
        return None
    if isinstance(value, str):
        return value
    if isinstance(value, (int, float)):
        return json.dumps(value)
    return None


def _audience(value: Any) -> str | None:
    if isinstance(value, list):
        parts = [item for item in value if isinstance(item, str)]
        return ", ".join(parts) if parts else None
    return _scalar(value)


def _signed_headers(value: Any) -> list[tuple[str, str]] | None:
    """Normalize the ModI signed_headers claim into (name, value) pairs."""
    pairs: list[tuple[str, str]] = []

    if isinstance(value, list):
        for element in value:
            if isinstance(element, dict):
                for name, item in element.items():
                    if not name or not name.strip():
                        continue
                    rendered = item if isinstance(item, str) else json.dumps(item, separators=(",", ":"))
                    pairs.append((name.lower(), rendered))
            elif isinstance(element, str) and element.strip():
                pairs.append((element.lower(), ""))
    elif isinstance(value, str):
        for name in value.split(","):
            name = name.strip()
            if name:
                pairs.append((name.lower(), ""))
    else:
        return None

    return pairs or None


def _parse_digest(raw: str) -> tuple[str, str] | None:
    """Parse a legacy RFC 3230 Digest header value."""
    for part in raw.split(","):
        part = part.strip()
        separator = part.find("=")
        if separator <= 0 or separator >= len(part) - 1:
            continue
        return part[:separator].strip(), part[separator + 1:].strip()
    return None


def _parse_content_digest(raw: str) -> tuple[str, str] | None:
    """Parse an RFC 9530 Content-Digest header value."""
    for part in raw.split(","):
        part = part.strip()
        separator = part.find("=")
        if separator <= 0 or separator >= len(part) - 1:
            continue

        algorithm = part[:separator].strip()
        value = part[separator + 1:].strip()

        parameters = value.find(";")
        if parameters >= 0:
            value = value[:parameters].strip()

        if len(value) >= 2 and value.startswith(":") and value.endswith(":"):
            value = value[1:-1]

        return algorithm, value
    return None


def extract(headers: Mapping[str, str | Sequence[str]]) -> dict[str, str]:
    """Extract canonical PDND metadata from a set of inbound request headers."""
    lookup: dict[str, str] = {}
    for name, value in headers.items():
        key = name.lower()
        if key not in lookup:
            first = _first(value)
            if first is not None:
                lookup[key] = first

    metadata: dict[str, str] = {}

    def emit(key: str, value: str | None) -> None:
        if value is not None and value != "" and key not in metadata:
            metadata[key] = value

    def emit_token(prefix: str, token: str) -> None:
        decoded = _decode_token(token)
        if decoded is None:
            return

        header, payload = decoded

        for claim in _JOSE_CLAIMS:
            emit(f"{prefix}.{claim}", _scalar(header.get(claim)))

        for claim in _PAYLOAD_CLAIMS[prefix]:
            if claim == "aud":
                emit(f"{prefix}.aud", _audience(payload.get("aud")))
            elif claim in payload:
                emit(f"{prefix}.{claim}", _scalar(payload[claim]))

        if prefix == "pdnd.signature":
            pairs = _signed_headers(payload.get("signed_headers"))
            if pairs:
                emit("pdnd.signature.signed_headers", ", ".join(name for name, _ in pairs))
                for name, value in pairs:
                    if value:
                        emit(f"pdnd.signature.signed_header.{name}", value)

    authorization = lookup.get("authorization")
    if authorization:
        scheme, _, rest = authorization.partition(" ")
        if scheme.lower() == "bearer" and rest.strip():
            emit_token("pdnd.voucher", rest.strip())

    for names, prefix in _TOKEN_HEADERS:
        for name in names:
            raw = lookup.get(name)
            if raw:
                emit_token(prefix, raw)
                break

    digest = lookup.get("digest")
    if digest:
        parsed = _parse_digest(digest)
        if parsed:
            emit("pdnd.digest.alg", parsed[0])
            emit("pdnd.digest.value", parsed[1])

    content_digest = lookup.get("content-digest")
    if content_digest:
        parsed = _parse_content_digest(content_digest)
        if parsed:
            emit("pdnd.content_digest.alg", parsed[0])
            emit("pdnd.content_digest.value", parsed[1])

    return metadata
