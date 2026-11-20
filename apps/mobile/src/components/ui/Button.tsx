import { LinearGradient } from 'expo-linear-gradient';
import { StyleSheet, View } from 'react-native';

import { PressableScale, type PressableScaleProps } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius, shadows } from '@/design/tokens';

type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'destructive';

export type ButtonProps = Omit<PressableScaleProps, 'children'> & {
  label: string;
  variant?: ButtonVariant;
  size?: 'standard' | 'compact';
  loading?: boolean;
};

export function Button({
  label,
  variant = 'primary',
  size = 'standard',
  disabled,
  loading,
  style,
  ...props
}: ButtonProps) {
  const primary = variant === 'primary';
  const destructive = variant === 'destructive';
  const inactive = disabled || loading;
  const isCompact = size === 'compact';

  return (
    <PressableScale
      {...props}
      haptic
      disabled={inactive}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ disabled: inactive, busy: loading }}
      style={[
        styles.base,
        isCompact && styles.compact,
        variant === 'secondary' && styles.secondary,
        destructive && styles.destructive,
        primary && styles.primary,
        inactive && styles.inactive,
        style,
      ]}
    >
      {primary ? (
        <LinearGradient
          colors={[colors.actionHighlight, colors.actionStrong]}
          style={StyleSheet.absoluteFill}
          pointerEvents="none"
        />
      ) : null}
      <View>
        <Text
          variant={isCompact ? 'label' : 'bodyStrong'}
          tone={primary ? 'onAction' : destructive ? 'danger' : 'default'}
          style={{ fontWeight: '600' }}
        >
          {label}
        </Text>
      </View>
    </PressableScale>
  );
}

const styles = StyleSheet.create({
  base: {
    height: layout.controlHeight,
    minHeight: layout.touchTarget,
    paddingHorizontal: 20,
    borderRadius: radius.full,
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
  },
  compact: {
    height: 38,
    minHeight: 38,
    paddingHorizontal: 14,
  },
  primary: {
    boxShadow: shadows.ember,
  },
  secondary: {
    backgroundColor: 'rgba(255, 255, 255, 0.05)',
    borderWidth: 1,
    borderColor: colors.lineFocus,
  },
  destructive: {
    backgroundColor: colors.dangerTint,
    borderWidth: 1,
    borderColor: colors.danger,
  },
  inactive: {
    opacity: 0.4,
  },
});
