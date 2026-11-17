# Fonts

Self-hosted IBM Plex (SIL Open Font License 1.1), served through `next/font/local`.
Self-hosting keeps builds offline-safe and fast for users in Egypt and KSA.

| Files | Family | Subset | Weights |
| --- | --- | --- | --- |
| `plex-arabic-*.woff2` | IBM Plex Sans Arabic | arabic | 400 500 600 700 |
| `plex-sans-*.woff2` | IBM Plex Sans | latin | 400 500 600 700 |
| `plex-mono-*.woff2` | IBM Plex Mono | latin | 400 500 600 |

Source: the `@fontsource/ibm-plex-*` npm packages (`files/*-normal.woff2`).
To add a subset or weight: `npm i -D @fontsource/<family>`, copy the file here, update `src/lib/fonts.ts`, then uninstall.
