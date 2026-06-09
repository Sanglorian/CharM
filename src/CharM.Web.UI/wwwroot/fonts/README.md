# Bundled fonts

These font files ship with CharM.Web.UI as static assets. They are
checked into the repository (rather than fetched at build time) so the
build works offline and so the OFL copyright + reserved-font-name
notices stay attached to the binaries as the licenses require.

| Files                       | Family            | Upstream                                                                 | License        | License file                       |
|-----------------------------|-------------------|--------------------------------------------------------------------------|----------------|------------------------------------|
| `ms-*.woff2`, `material-symbols.css` | Material Symbols Outlined | https://fonts.google.com/icons (https://github.com/google/material-design-icons) | Apache 2.0     | `LICENSE-MaterialSymbols.txt`      |
| `nm-*.woff2`, `noto-manrope.css`     | Manrope                   | https://fonts.google.com/specimen/Manrope (https://github.com/sharanda/manrope) | SIL OFL 1.1    | `LICENSE-OFL.txt`                  |
| `ns-*.woff2`, `noto-serif.css`       | Noto Serif                | https://fonts.google.com/noto/specimen/Noto+Serif (https://github.com/notofonts/latin-greek-cyrillic) | SIL OFL 1.1    | `LICENSE-OFL.txt`                  |

The `.css` files are the Google Fonts CSS responses with the upstream
URLs rewritten to the local `nm-*` / `ns-*` / `ms-*` filenames. Each
`@font-face` block carries the original `unicode-range` so the browser
only downloads the subsets it actually needs.

If you bump a font version, update the file listing above and confirm
the upstream still publishes under the same license; OFL in particular
requires that the copyright notice and Reserved Font Name in
`LICENSE-OFL.txt` are updated to match the new release.
