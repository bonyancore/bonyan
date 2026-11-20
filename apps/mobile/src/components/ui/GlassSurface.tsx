import { BlurView } from 'expo-blur';
import { StyleSheet, View, type ViewProps } from 'react-native';

import { useReduceTransparency } from '@/design/hooks/useReduceTransparency';
import { colors, materials, shadows } from '@/design/tokens';

type GlassMaterial = 'navigation' | 'overlay';

export type GlassSurfaceProps = ViewProps & {
  material?: GlassMaterial;
  topOnlyRadius?: boolean;
  noHighlight?: boolean;
  radius?: number;
};

const config = {
  navigation: {
    ...materials.glassNavigation,
    fill: colors.glass,
    shadow: shadows.glassNav,
    highlight: 'rgba(255, 255, 255, 0.18)',
    intensity: 60,
  },
  overlay: {
    ...materials.glassOverlay,
    fill: colors.glassOverlay,
    shadow: shadows.glassOverlay,
    highlight: 'rgba(255, 255, 255, 0.14)',
    intensity: 80,
  },
} as const;

export function GlassSurface({
  material = 'navigation',
  topOnlyRadius = false,
  noHighlight = false,
  radius: propRadius,
  style,
  children,
  ...props
}: GlassSurfaceProps) {
  const solid = useReduceTransparency();
  const spec = config[material];
  const flatStyle = StyleSheet.flatten(style) || {};
  const effectiveRadius = propRadius ?? flatStyle.borderRadius ?? spec.radius;

  const radiusStyles = topOnlyRadius
    ? {
        borderTopLeftRadius: effectiveRadius,
        borderTopRightRadius: effectiveRadius,
        borderBottomLeftRadius: 0,
        borderBottomRightRadius: 0,
      }
    : {
        borderRadius: effectiveRadius,
      };

  const isSmallControl =
    (typeof flatStyle.width === 'number' && flatStyle.width <= 48) ||
    (typeof flatStyle.height === 'number' && flatStyle.height <= 44);
  const showHighlight = !noHighlight && !isSmallControl;

  return (
    <View
      {...props}
      style={[
        {
          borderWidth: 1,
          borderColor: colors.lineStrong,
          boxShadow: spec.shadow,
          overflow: 'hidden',
          backgroundColor: solid ? colors.solidGlass : spec.fill,
        },
        radiusStyles,
        style,
      ]}
    >
      {solid ? null : (
        <BlurView
          intensity={spec.intensity}
          tint="dark"
          style={StyleSheet.absoluteFill}
          pointerEvents="none"
        />
      )}
      {showHighlight && (
        <View
          pointerEvents="none"
          style={[styles.highlight, { backgroundColor: spec.highlight }]}
        />
      )}
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  highlight: {
    position: 'absolute',
    top: 0,
    left: 16,
    right: 16,
    height: StyleSheet.hairlineWidth,
  },
});
