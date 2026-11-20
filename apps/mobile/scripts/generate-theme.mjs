import { writeFileSync } from 'node:fs';
import { colors } from '../src/design/tokens/colors.ts';
import { shadows } from '../src/design/tokens/elevation.ts';
import { radius } from '../src/design/tokens/radius.ts';
import { spacingUnit } from '../src/design/tokens/spacing.ts';

const kebab = (value) => value.replace(/[A-Z]/g, (match) => `-${match.toLowerCase()}`);

const declarations = (entries, indent) =>
  entries.map(([name, value]) => `${indent}${name}: ${value};`).join('\n');

const colorVariables = Object.entries(colors).map(([token, value]) => [`--color-${kebab(token)}`, value]);

const staticVariables = [
  ['--spacing', `${spacingUnit}px`],
  ...Object.entries(radius).map(([token, value]) => [`--radius-${token}`, `${value}px`]),
  ...Object.entries(shadows).map(([token, value]) => [`--shadow-${token}`, value]),
];

const css = `@theme {
${declarations(staticVariables, '  ')}
}

@layer theme {
  :root {
${declarations(colorVariables, '    ')}
  }
}
`;

writeFileSync(new URL('../src/design/theme.css', import.meta.url), css);
