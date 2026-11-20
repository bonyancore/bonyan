import { StyleSheet, View } from 'react-native';

import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { isRTL } from '@/core/i18n/direction';
import { colors, radius } from '@/design/tokens';

export type CriticalAlertCardProps = {
  title: string;
  subtitle: string;
  onPress?: () => void;
};

export function CriticalAlertCard({ title, subtitle, onPress }: CriticalAlertCardProps) {
  const rtl = isRTL();

  return (
    <PressableScale
      haptic
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`Critical alert: ${title}. ${subtitle}`}
      style={styles.card}
    >
      <View className="flex-row items-center justify-between">
        <View className="flex-1 flex-row items-center gap-3 pe-2">
          <View style={styles.iconBox}>
            <Icon name="warning" size={18} color={colors.danger} strokeWidth={2.2} />
          </View>
          <View className="flex-1 gap-0.5">
            <Text variant="headline" tone="default" numberOfLines={1}>
              {title}
            </Text>
            <Text variant="caption" tone="muted" numberOfLines={1}>
              {subtitle}
            </Text>
          </View>
        </View>

        <View style={rtl ? { transform: [{ scaleX: -1 }] } : undefined}>
          <Icon name="chevronRight" size={16} color={colors.foregroundSoft} />
        </View>
      </View>
    </PressableScale>
  );
}

const styles = StyleSheet.create({
  card: {
    borderRadius: radius.card,
    backgroundColor: 'rgba(242, 122, 109, 0.08)',
    borderWidth: 1,
    borderColor: 'rgba(242, 122, 109, 0.35)',
    padding: 16,
  },
  iconBox: {
    width: 36,
    height: 36,
    borderRadius: radius.chip,
    backgroundColor: 'rgba(242, 122, 109, 0.16)',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
