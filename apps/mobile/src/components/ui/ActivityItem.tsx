import { View } from 'react-native';

import { Icon, type IconName } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type ActivityItemProps = {
  icon: IconName;
  statusTone?: 'success' | 'info' | 'warning' | 'danger';
  title?: string;
  actor?: string;
  action?: string;
  target?: string;
  time: string;
  onPress?: () => void;
};

const tintBg = {
  success: colors.successTint,
  info: colors.infoTint,
  warning: colors.warningTint,
  danger: colors.dangerTint,
};

const iconColor = {
  success: colors.success,
  info: colors.info,
  warning: colors.warning,
  danger: colors.danger,
};

export function ActivityItem({
  icon,
  statusTone = 'info',
  title,
  actor,
  action,
  target,
  time,
  onPress,
}: ActivityItemProps) {
  const content = (
    <View className="flex-row items-center justify-between py-2.5">
      <View className="flex-1 flex-row items-center gap-3">
        <View
          className="h-8 w-8 items-center justify-center rounded-full"
          style={{ backgroundColor: tintBg[statusTone] }}
        >
          <Icon name={icon} size={15} color={iconColor[statusTone]} />
        </View>

        <View className="flex-1">
          {actor || action || target ? (
            <Text variant="footnote" tone="soft" numberOfLines={1}>
              {actor ? `${actor} ` : ''}
              {action ? `${action} ` : ''}
              {target ? (
                <Text variant="footnote" tone="default" style={{ fontWeight: '600' }}>
                  {target}
                </Text>
              ) : null}
            </Text>
          ) : (
            <Text variant="footnote" tone="default" numberOfLines={1}>
              {title}
            </Text>
          )}
        </View>
      </View>
      <Text variant="caption" tone="muted" className="ms-2">
        {time}
      </Text>
    </View>
  );

  if (onPress) {
    return (
      <PressableScale onPress={onPress} accessibilityRole="button">
        {content}
      </PressableScale>
    );
  }

  return content;
}
