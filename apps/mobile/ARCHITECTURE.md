# Construction Connect Mobile — Architecture

## Stack

| Concern | Choice |
| --- | --- |
| Runtime | Expo SDK 58, React Native 0.88, React 19.3, New Architecture, React Compiler |
| Routing | Expo Router (file based, typed routes) in `src/app` |
| Styling | Uniwind (Tailwind v4 bindings for React Native), tokens generated from TypeScript |
| Motion and gestures | Reanimated 4, Worklets, Gesture Handler |
| Server state | TanStack Query, offline first network mode |
| Client state | Zustand, persisted through `expo-sqlite/kv-store` (synchronous, no hydration flash) |
| i18n | i18next + react-i18next, English and Arabic, RTL through `I18nManager` |
| Fonts | Geist, Geist Mono, IBM Plex Sans Arabic |
| Materials | expo-blur and expo-linear-gradient behind `GlassSurface` and `ElevatedCard` |

## Folder layout

```
src/
  app/                  routes only, thin screens that compose features
  components/ui/        design system primitives (Text now; Button, Card, Chip, TapeProgress next)
  core/                 app-wide infrastructure, no UI
    i18n/               languages, locales, direction handling
    preferences/        persisted language and theme
    providers/          AppProviders
    query/              QueryClient
  design/
    tokens/             single source of truth (colors, typography, spacing, radius, elevation, motion, layout)
    theme.css           generated from tokens, never edited by hand
    fonts.ts            font sources, names match tokens/typography.ts
    theme.ts            useThemeName, useColors for non className uses (SVG, charts, Reanimated)
  features/             one folder per domain, added as each module is built
    <feature>/api/        query and mutation hooks, request functions
    <feature>/components/ feature UI
    <feature>/model/      types, mappers, zod schemas
  global.css            Tailwind + Uniwind entry, imports theme.css
scripts/
  generate-theme.mjs    tokens -> src/design/theme.css
  check-contrast.mjs    WCAG 4.5:1 check for every text pair in both themes
```

Planned features, matching the design: `today`, `capture`, `tasks`, `projects`, `trades`, `inspections`, `daily-report`, `procurement`, `finance`, `inbox`.

Planned routes: `(tabs)` with `index` (Today), `projects`, `tasks`, `inbox`; `capture` as a modal route; stack screens for project, trade, inspection, report and approval.

## Dependency direction

`app` → `features` → `components/ui` → `design`, and everything may use `core`. `design` and `core` never import from `features` or `components`.

## Design tokens

1. Edit values in `src/design/tokens/*.ts`.
2. Run `npm run tokens` to regenerate `src/design/theme.css`.
3. Run `npm run contrast` after touching colors.

See `IMPLEMENTATION_BRIEF.md` for the full design system contract.

Colors are semantic, not literal: `bg-surface`, `text-foreground-soft`, `bg-action`, `text-on-action`, `bg-capture`, `bg-success-tint`. There is one theme, so components never branch on it. Radius, shadow and spacing come through the same CSS: `rounded-card`, `rounded-sheet`, `shadow-sheet`, and a 4 pt scale (`p-4` is 16).

Typography is applied by `<Text variant tone>`, not by class names, because font weight selects a different font file on React Native and Arabic needs a different family, line height and zero letter spacing.

Ember (`action`) is reserved for primary action, active tab, selection and focus. Status always carries an icon and a label.

## RTL and Arabic rules

- Use logical utilities only: `ms-*`, `me-*`, `ps-*`, `pe-*`, `start-*`, `end-*`, `text-start`. Never `left`, `right`, `ml`, `mr`, `pl`, `pr`.
- `flex-row` flips automatically. Directional icons (chevrons, back arrows) must mirror.
- Figures, money and codes stay Western digits in Geist (`numeric` text variants ignore the Arabic family).
- `changeLanguage()` switches the language, flips the native layout direction and reloads the app, which is required by React Native.

## Commands

```
npm start              dev server
npm run tokens         regenerate theme.css
npm run contrast       check text contrast in both themes
npm run typecheck      tsc --noEmit
npm run lint           expo lint
```

## Known limits

- `expo install` could not reach the Expo versions API in the build environment, so versions were pinned from the SDK's own bundled version list. Run `npx expo install --check` once on a normal connection.
- `.npmrc` sets `legacy-peer-deps` because `expo-router` declares an optional `react-dom` peer that is newer than the SDK's pinned React.
- Bundle identifier and Android package (`com.constructionconnect.mobile`) are placeholders. Icons and splash are the Expo template defaults.
- Obsidian Liquid is a single dark theme. There is no light theme.
- Glass blur is iOS only. Android and reduce-transparency use the solid fallback.
- Nothing has been reviewed on a physical device yet.
- `onlineManager` for TanStack Query is not wired yet; it needs a connectivity source.