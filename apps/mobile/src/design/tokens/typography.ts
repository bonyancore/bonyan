export const fontFamily = {
  latin: {
    regular: 'Geist_400Regular',
    medium: 'Geist_500Medium',
    semibold: 'Geist_600SemiBold',
  },
  arabic: {
    regular: 'IBMPlexSansArabic_400Regular',
    medium: 'IBMPlexSansArabic_500Medium',
    semibold: 'IBMPlexSansArabic_600SemiBold',
  },
  mono: {
    medium: 'GeistMono_500Medium',
  },
} as const;

export type FontWeight = keyof typeof fontFamily.latin;

export type TypeSpec = {
  family: 'sans' | 'mono';
  weight: FontWeight;
  size: number;
  lineHeight: number;
  tracking: number;
  uppercase?: boolean;
  numeric?: boolean;
  scaleCap?: 'text' | 'kpi';
};

const sans = (
  weight: FontWeight,
  size: number,
  lineHeight: number,
  tracking = 0,
  extra: Partial<TypeSpec> = {},
): TypeSpec => ({ family: 'sans', weight, size, lineHeight, tracking, ...extra });

export const typeScale = {
  display: sans('semibold', 34, 40, -0.68),
  title1: sans('semibold', 28, 34, -0.42),
  title2: sans('semibold', 22, 28, -0.22),
  headline: sans('semibold', 17, 24),
  body: sans('regular', 15, 22),
  bodyStrong: sans('semibold', 15, 22),
  callout: sans('regular', 14, 20),
  label: sans('medium', 13, 18),
  footnote: sans('regular', 13, 18),
  caption: sans('regular', 12, 16),
  overline: {
    family: 'mono',
    weight: 'semibold',
    size: 11,
    lineHeight: 14,
    tracking: 0.88,
    uppercase: true,
  },
  kpi: sans('semibold', 32, 36, -0.64, { numeric: true, scaleCap: 'kpi' }),
  figure: sans('semibold', 20, 24, 0, { numeric: true }),
} as const satisfies Record<string, TypeSpec>;

export type TextVariant = keyof typeof typeScale;

export const arabicOverrides: Partial<Record<TextVariant, Partial<TypeSpec>>> = {
  overline: { family: 'sans', weight: 'medium', size: 12, lineHeight: 18, uppercase: false },
};

export const arabicLineHeightBoost = 4;
