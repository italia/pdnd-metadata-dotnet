# Logo

## Concept

The mark encodes what the library does, and nothing else.

The top row is a compact JWS: three segments of unequal width, separated by gaps, in neutral grey. It is the opaque artifact that arrives on the wire. The rows below are key and value pairs in institutional blue: a small square marker for the canonical key, a longer bar for the value. Grey means unparsed, blue means normalized. The transition between the two rows is the library.

The blue is `#0066CC`, the same institutional value used in `pdnd-eservices-graph`, so the two projects read as one body of work rather than two unrelated repositories.

The wordmark is set in a monospace stack. Monospace is the vernacular of HTTP headers and tokens, which is the subject matter, and it avoids the generic geometric sans that most developer tooling defaults to.

## Files

| File | Use |
|------|-----|
| `logo.svg` | Square mark, light backgrounds. README header, organisation avatar, favicon source. |
| `logo-dark.svg` | Square mark, dark backgrounds. Brighter blue (`#4D9BEF`) for contrast on GitHub dark. |
| `logo-wordmark.svg` | Horizontal lockup with wordmark and descriptor. README header at larger sizes. |

## README snippet

GitHub honours `<picture>` with `prefers-color-scheme` in Markdown, so the mark can adapt:

```html
<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.svg">
    <img src="assets/logo.svg" alt="Pdnd.Metadata" width="96">
  </picture>
</p>
```

## Notes

- The SVGs are hand written, dependency free, and use no external fonts for the mark itself. Only `logo-wordmark.svg` contains text, and it falls back through a monospace stack rather than requiring a webfont.
- No `viewBox` scaling assumptions: the mark is built on a 96 by 96 grid with 8 unit margins, so it stays legible down to 24 pixels.
- For a favicon, export `logo.svg` at 32 and 48 pixels. The three top segments merge visually below roughly 20 pixels, at which point the two blue rows alone still read as structured data.
- Colours are flat, with a single opacity value (`0.35`) used for value bars. There are no gradients and no shadows, so the mark reproduces correctly in monochrome print and in terminal rendering.
