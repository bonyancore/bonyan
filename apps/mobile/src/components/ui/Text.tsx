import {
  Platform,
  Text as NativeText,
  type TextProps as NativeTextProps,
  type TextStyle,
} from 'react-native';

import { isRTL } from '@/core/i18n/direction';
import {
  arabicLineHeightBoost,
  arabicOverrides,
  colors,
  fontFamily,
  layout,
  typeScale,
  type FontWeight,
  type TextVariant,
  type TypeSpec,
} from '@/design/tokens';

export const toneColor = {
  default: colors.foreground,
  soft: colors.foregroundSoft,
  muted: colors.foregroundMuted,
  action: colors.action,
  onAction: colors.onAction,
  success: colors.success,
  info: colors.info,
  warning: colors.warning,
  danger: colors.danger,
} as const;

export type TextTone = keyof typeof toneColor;

const toneClassName = {
  default: 'text-foreground',
  soft: 'text-foreground-soft',
  muted: 'text-foreground-muted',
  action: 'text-action',
  onAction: 'text-on-action',
  success: 'text-success',
  info: 'text-info',
  warning: 'text-warning',
  danger: 'text-danger',
} as const;

export type TextProps = NativeTextProps & {
  variant?: TextVariant;
  tone?: TextTone;
};

const resolve = (variant: TextVariant, rtl: boolean): TypeSpec => {
  const base: TypeSpec = typeScale[variant];
  return rtl ? { ...base, ...arabicOverrides[variant] } : base;
};

const resolveFamily = (spec: TypeSpec, rtl: boolean) => {
  if (Platform.OS === 'web') {
    if (spec.family === 'mono') return 'GeistMono_500Medium, monospace';
    return rtl && !spec.numeric
      ? 'IBMPlexSansArabic_400Regular, "IBM Plex Sans Arabic", system-ui, sans-serif'
      : 'Geist_400Regular, Geist, system-ui, sans-serif';
  }
  if (spec.family === 'mono') return fontFamily.mono.medium;
  return rtl && !spec.numeric ? fontFamily.arabic[spec.weight] : fontFamily.latin[spec.weight];
};

const weightMap: Record<FontWeight, TextStyle['fontWeight']> = {
  regular: '400',
  medium: '500',
  semibold: '600',
};

export function Text({
  variant = 'body',
  tone = 'default',
  className,
  style,
  ...props
}: TextProps) {
  const rtl = isRTL();
  const spec = resolve(variant, rtl);
  const boost = rtl && spec.family === 'sans' && !spec.numeric ? arabicLineHeightBoost : 0;

  return (
    <NativeText
      maxFontSizeMultiplier={
        spec.scaleCap === 'kpi' ? layout.maxKpiFontScale : layout.maxFontScale
      }
      {...props}
      className={className ? `${toneClassName[tone]} ${className}` : toneClassName[tone]}
      style={[
        {
          color: toneColor[tone],
          fontFamily: resolveFamily(spec, rtl),
          fontWeight: weightMap[spec.weight],
          fontSize: spec.size,
          lineHeight: spec.lineHeight + boost,
          letterSpacing: rtl ? 0 : spec.tracking,
          textTransform: spec.uppercase ? 'uppercase' : 'none',
          fontVariant: ['tabular-nums'],
        },
        style,
      ]}
    />
  );
}
