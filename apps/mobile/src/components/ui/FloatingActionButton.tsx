import { LinearGradient } from 'expo-linear-gradient';
import { StyleSheet, View } from 'react-native';

import { Icon, type IconName } from '@/components/ui/Icon';
import { PressableScale, type PressableScaleProps } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius, shadows } from '@/design/tokens';

export type FabSize = 'standard' | 'mini' | 'extended';

export type FloatingActionButtonProps = Omit<PressableScaleProps, 'children'> & {
  icon?: IconName;
  label?: string;
  size?: FabSize;
  accessibilityLabel: string;
};

export function FloatingActionButton({
  icon = 'plus',
  label,
  size = 'standard',
  accessibilityLabel,
  disabled,
  style,
  ...props
}: FloatingActionButtonProps) {
  const isMini = size === 'mini';
  const isExtended = size === 'extended';

  const diameter = isMini ? layout.fabMini : layout.fab;
  const iconSize = isMini ? layout.icon.md : isExtended ? layout.icon.lg : layout.icon.xl;

  return (
    <PressableScale
      {...props}
      haptic
      disabled={disabled}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityState={{ disabled }}
      style={[
        styles.base,
        isExtended ? styles.extended : { width: diameter, height: diameter, borderRadius: diameter / 2 },
        style,
      ]}
    >
      <LinearGradient
        colors={[colors.actionHighlight, colors.actionStrong]}
        style={StyleSheet.absoluteFill}
        pointerEvents="none"
      />
      <View className="flex-row items-center gap-2">
        <Icon name={icon} size={iconSize} color={colors.onAction} strokeWidth={2.2} />
        {isExtended && label ? (
          <Text variant="bodyStrong" tone="onAction">
            {label}
          </Text>
        ) : null}
      </View>
    </PressableScale>
  );
}

const styles = StyleSheet.create({
  base: {
    alignItems: 'center',
    justifyContent: 'center',
    boxShadow: shadows.ember,
    overflow: 'hidden',
  },
  extended: {
    height: layout.controlHeight,
    paddingHorizontal: 20,
    borderRadius: radius.full,
  },
});
