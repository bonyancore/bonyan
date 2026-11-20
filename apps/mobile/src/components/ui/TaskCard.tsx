import { useState } from 'react';
import { View } from 'react-native';

import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { StatusBadge, type StatusName } from '@/components/ui/StatusBadge';
import { ElevatedCard } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type TaskCardProps = {
  code: string;
  title: string;
  trade: string;
  progressText: string;
  status?: StatusName;
  statusLabel?: string;
  assigneeInitials?: string;
  assigneeName?: string;
  actionItemLabel?: string;
  onCompleteAction?: () => void;
  onPress?: () => void;
};

export function TaskCard({
  code,
  title,
  trade,
  progressText,
  status = 'atRisk',
  statusLabel = 'Due today',
  assigneeInitials = 'KA',
  assigneeName,
  actionItemLabel = 'Set out column grid',
  onCompleteAction,
  onPress,
}: TaskCardProps) {
  const [completed, setCompleted] = useState(false);

  return (
    <ElevatedCard style={{ padding: 16 }}>
      <PressableScale onPress={onPress} className="gap-3">
        <View className="flex-row items-center justify-between">
          <Text variant="overline" tone="muted">
            {code}
          </Text>
          <StatusBadge status={status} label={statusLabel} size="compact" />
        </View>

        <Text variant="headline" numberOfLines={2}>
          {title}
        </Text>

        <View className="flex-row items-center justify-between border-b border-line pb-3">
          <View className="flex-row items-center gap-2">
            {assigneeInitials ? (
              <View className="h-5 w-5 items-center justify-center rounded-full bg-raised">
                <Text variant="caption" tone="soft" style={{ fontSize: 9, fontWeight: '700' }}>
                  {assigneeInitials}
                </Text>
              </View>
            ) : null}
            <Text variant="caption" tone="soft">
              {assigneeName ? `${assigneeName} · ${trade}` : trade}
            </Text>
          </View>
          <Text variant="caption" tone="muted">
            {progressText}
          </Text>
        </View>

        <PressableScale
          haptic
          onPress={() => {
            setCompleted(!completed);
            onCompleteAction?.();
          }}
          className={`flex-row items-center gap-2.5 rounded-control p-2.5 ${
            completed ? 'bg-success-tint' : 'bg-sunken'
          }`}
        >
          <Icon
            name={completed ? 'checkCircle' : 'check'}
            size={16}
            color={completed ? colors.success : colors.foregroundMuted}
            strokeWidth={2.4}
          />
          <Text
            variant="footnote"
            tone={completed ? 'success' : 'soft'}
            style={completed ? { textDecorationLine: 'line-through' } : undefined}
          >
            {actionItemLabel}
          </Text>
        </PressableScale>
      </PressableScale>
    </ElevatedCard>
  );
}
