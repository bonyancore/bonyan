import { StyleSheet, View } from 'react-native';

import { Icon } from '@/components/ui/Icon';
import { Text } from '@/components/ui/Text';
import { colors, radius } from '@/design/tokens';

export type OfflineStateProps = {
  queueCount?: number;
  message?: string;
  subMessage?: string;
};

export function OfflineState({
  queueCount = 3,
  message,
  subMessage = 'Approvals and reports will move on the device and send when the connection returns. Core documents remain readable offline.',
}: OfflineStateProps) {
  const displayTitle = message || `Offline. ${queueCount} changes queued`;

  return (
    <View style={styles.banner}>
      <View className="flex-row items-start gap-3">
        <View className="mt-0.5">
          <Icon name="cloudOff" size={18} color={colors.action} />
        </View>
        <View className="flex-1 gap-1">
          <Text variant="footnote" tone="action" style={{ fontWeight: '600' }}>
            {displayTitle}
          </Text>
          <Text variant="caption" tone="soft">
            {subMessage}
          </Text>
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    borderRadius: radius.card,
    backgroundColor: colors.actionTint,
    borderWidth: 1,
    borderColor: colors.actionRing,
    padding: 14,
  },
});
