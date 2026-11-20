import { colors } from '../src/design/tokens/colors.ts';

const pairs = [
  ['foreground', 'background'],
  ['foreground', 'surface'],
  ['foreground', 'elevated'],
  ['foregroundSoft', 'background'],
  ['foregroundSoft', 'surface'],
  ['foregroundMuted', 'background'],
  ['foregroundMuted', 'surface'],
  ['onAction', 'action'],
  ['success', 'background'],
  ['success', 'surface'],
  ['info', 'background'],
  ['info', 'surface'],
  ['warning', 'background'],
  ['warning', 'surface'],
  ['danger', 'background'],
  ['danger', 'surface'],
];

const channel = (value) => {
  const unit = value / 255;
  return unit <= 0.03928 ? unit / 12.92 : ((unit + 0.055) / 1.055) ** 2.4;
};

const luminance = (hex) => {
  const [r, g, b] = [1, 3, 5].map((index) => channel(parseInt(hex.slice(index, index + 2), 16)));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};

const ratio = (foreground, background) => {
  const [lighter, darker] = [luminance(foreground), luminance(background)].sort((a, b) => b - a);
  return (lighter + 0.05) / (darker + 0.05);
};

const minimum = 4.5;
let failures = 0;

for (const [foreground, background] of pairs) {
  const value = ratio(colors[foreground], colors[background]);
  const passed = value >= minimum;
  if (!passed) failures += 1;
  console.log(
    `${passed ? 'pass' : 'FAIL'}  ${foreground.padEnd(16)} on ${background.padEnd(12)}  ${value.toFixed(2)}:1`,
  );
}

if (failures > 0) {
  console.error(`${failures} pair(s) below ${minimum}:1`);
  process.exit(1);
} else {
  console.log(`All color pairs pass WCAG AA (>= ${minimum}:1)`);
}
