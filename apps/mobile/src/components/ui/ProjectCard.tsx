import { View } from 'react-native';

import { PressableScale } from '@/components/ui/PressableScale';
import { ProgressBar } from '@/components/ui/ProgressBar';
import { StatusBadge, type StatusName } from '@/components/ui/StatusBadge';
import { ElevatedCard } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';

export type ProjectCardProps = {
  name: string;
  typeSubtitle: string;
  status?: StatusName;
  statusLabel?: string;
  progress: number;
  plan?: number;
  budgetLabel?: string;
  tasksCount?: number;
  approvalsCount?: number;
  onPress?: () => void;
  onTasksPress?: () => void;
  onApprovalsPress?: () => void;
};

export function ProjectCard({
  name,
  typeSubtitle,
  status = 'atRisk',
  statusLabel = 'At risk',
  progress,
  plan,
  tasksCount = 12,
  approvalsCount = 3,
  onPress,
  onTasksPress,
  onApprovalsPress,
}: ProjectCardProps) {
  const CardContent = (
    <View className="gap-3">
      <View className="flex-row items-center justify-between">
        <View className="flex-1 gap-0.5 pe-2">
          <Text variant="headline" numberOfLines={1}>
            {name}
          </Text>
          <Text variant="caption" tone="muted" numberOfLines={1}>
            {typeSubtitle}
          </Text>
        </View>
        <StatusBadge status={status} label={statusLabel} size="compact" />
      </View>

      <View className="gap-1.5">
        <View className="flex-row items-center justify-between">
          <Text variant="caption" tone="muted">
            Progress
          </Text>
          <Text variant="caption" tone="soft">
            {plan !== undefined ? `${progress}% · plan ${plan}%` : `${progress}%`}
          </Text>
        </View>
        <ProgressBar
          value={progress}
          plan={plan}
          tone={plan && progress < plan ? 'warning' : progress >= 100 ? 'success' : 'info'}
          label={`Progress ${progress}%`}
        />
      </View>

      <View className="flex-row items-center justify-between border-t border-line pt-2.5">
        <PressableScale
          onPress={onTasksPress ?? onPress}
          disabled={!onTasksPress && !onPress}
          accessibilityRole="button"
          accessibilityLabel={`${tasksCount} open tasks`}
        >
          <Text variant="footnote" tone="soft">
            {`${tasksCount} open tasks`}
          </Text>
        </PressableScale>

        <PressableScale
          onPress={onApprovalsPress ?? onPress}
          disabled={!onApprovalsPress && !onPress}
          accessibilityRole="button"
          accessibilityLabel={`${approvalsCount} approvals`}
        >
          <Text variant="footnote" tone="action">
            {`${approvalsCount} approvals`}
          </Text>
        </PressableScale>
      </View>
    </View>
  );

  return (
    <ElevatedCard style={{ padding: 16 }}>
      {onPress ? (
        <PressableScale onPress={onPress} accessibilityRole="button" accessibilityLabel={name}>
          {CardContent}
        </PressableScale>
      ) : (
        CardContent
      )}
    </ElevatedCard>
  );
}
