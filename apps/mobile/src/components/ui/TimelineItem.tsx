import { View } from 'react-native';

import { Icon } from '@/components/ui/Icon';
import { Text } from '@/components/ui/Text';
import { isRTL } from '@/core/i18n/direction';
import { colors } from '@/design/tokens';

export type TimelineStepState = 'completed' | 'active' | 'pending';

export type TimelineItemProps = {
  title: string;
  subtitle?: string;
  state: TimelineStepState;
  isLast?: boolean;
};

export function TimelineItem({
  title,
  subtitle,
  state,
  isLast = false,
}: TimelineItemProps) {
  const isCompleted = state === 'completed';
  const isActive = state === 'active';
  const rtl = isRTL();

  const node = (
    <View className="items-center">
      <View
        className="h-5 w-5 items-center justify-center rounded-full"
        style={{
          backgroundColor: isCompleted
            ? colors.success
            : isActive
              ? 'rgba(240, 160, 75, 0.14)'
              : colors.sunken,
          borderWidth: isActive ? 2 : 1,
          borderColor: isActive
            ? colors.action
            : isCompleted
              ? colors.success
              : colors.lineStrong,
        }}
      >
        {isCompleted ? (
          <Icon name="check" size={11} color="#10161D" strokeWidth={2.6} />
        ) : isActive ? (
          <View className="h-2 w-2 rounded-full bg-action" />
        ) : null}
      </View>

      {!isLast ? (
        <View
          className="w-0.5 my-1"
          style={{
            height: 32,
            backgroundColor: isCompleted ? colors.success : colors.line,
          }}
        />
      ) : null}
    </View>
  );

  const textContent = (
    <View className="flex-1 pb-2">
      <Text
        variant="footnote"
        tone={isActive ? 'action' : isCompleted ? 'default' : 'muted'}
        style={{ fontWeight: isActive ? '600' : '400' }}
      >
        {title}
      </Text>
      {subtitle ? (
        <Text variant="caption" tone="muted">
          {subtitle}
        </Text>
      ) : null}
    </View>
  );

  return (
    <View
      className="flex-row items-start gap-3"
      style={{ flexDirection: rtl ? 'row-reverse' : 'row' }}
    >
      {node}
      {textContent}
    </View>
  );
}
