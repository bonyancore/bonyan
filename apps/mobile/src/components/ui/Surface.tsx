import { LinearGradient } from 'expo-linear-gradient';
import { StyleSheet, View, type ViewProps } from 'react-native';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { colors, radius, shadows } from '@/design/tokens';

type SurfaceProps = ViewProps & {
  selected?: boolean;
};

export function ElevatedCard({ selected = false, style, children, ...props }: SurfaceProps) {
  return (
    <View
      {...props}
      style={[
        styles.elevated,
        selected && styles.selected,
        style,
      ]}
    >
      <LinearGradient
        colors={['#1A222C', '#141B23']}
        style={StyleSheet.absoluteFill}
        pointerEvents="none"
      />
      <View pointerEvents="none" style={styles.topHighlight} />
      {children}
    </View>
  );
}

export function SolidSurface({ style, ...props }: ViewProps) {
  return <View {...props} style={[styles.solid, style]} />;
}

export function GlassCard({ style, children, ...props }: ViewProps) {
  return (
    <GlassSurface
      material="navigation"
      radius={radius.card}
      style={[styles.glassCard, style]}
      {...props}
    >
      {children}
    </GlassSurface>
  );
}

const styles = StyleSheet.create({
  elevated: {
    borderRadius: radius.card,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.07)',
    boxShadow: shadows.elevated,
    overflow: 'hidden',
  },
  topHighlight: {
    position: 'absolute',
    top: 0,
    left: 16,
    right: 16,
    height: StyleSheet.hairlineWidth * 2,
    backgroundColor: 'rgba(255, 255, 255, 0.06)',
  },
  selected: {
    borderColor: colors.actionRing,
    boxShadow: `0 0 0 3px ${colors.actionTint}`,
  },
  solid: {
    borderRadius: radius.solid,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.08)',
    backgroundColor: colors.surface,
    overflow: 'hidden',
  },
  glassCard: {
    borderRadius: radius.card,
  },
});
