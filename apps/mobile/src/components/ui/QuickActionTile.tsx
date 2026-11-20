import { StyleSheet, View } from 'react-native';

import { Icon, type IconName } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { ElevatedCard } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors, radius } from '@/design/tokens';

export type QuickActionTileProps = {
  icon: IconName;
  label: string;
  onPress: () => void;
  iconColor?: string;
};

export function QuickActionTile({
  icon,
  label,
  onPress,
  iconColor = colors.action,
}: QuickActionTileProps) {
  return (
    <ElevatedCard style={styles.card}>
      <PressableScale
        haptic
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={label}
        style={styles.inner}
      >
        <View style={styles.iconContainer}>
          <Icon name={icon} size={22} color={iconColor} strokeWidth={2} />
        </View>
        <Text variant="footnote" tone="default" style={{ fontWeight: '600' }}>
          {label}
        </Text>
      </PressableScale>
    </ElevatedCard>
  );
}

const styles = StyleSheet.create({
  card: {
    flex: 1,
    height: 84,
    borderRadius: radius.card,
  },
  inner: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    padding: 8,
  },
  iconContainer: {
    alignItems: 'center',
    justifyContent: 'center',
  },
});
