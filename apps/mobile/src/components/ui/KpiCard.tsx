import { View } from 'react-native';

import { MetricTrend } from '@/components/ui/MetricTrend';
import { ProgressBar } from '@/components/ui/ProgressBar';
import { StatusBadge, type StatusName } from '@/components/ui/StatusBadge';
import { ElevatedCard } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type KpiCardProps = {
  label: string;
  value: string;
  delta?: { label: string; status: StatusName };
  stale?: string;
  trendData?: readonly number[];
  progress?: { value: number; plan?: number; label?: string };
  skeleton?: boolean;
  onPress?: () => void;
  children?: React.ReactNode;
};

export function KpiCard({
  label,
  value,
  delta,
  stale,
  trendData,
  progress,
  skeleton,
  children,
}: KpiCardProps) {
  if (skeleton) {
    return (
      <ElevatedCard style={{ padding: 16 }}>
        <View className="gap-2.5">
          <View className="h-3.5 w-20 rounded bg-sunken opacity-60" />
          <View className="h-8 w-28 rounded bg-sunken opacity-80" />
          <View className="h-5 w-16 rounded bg-sunken opacity-50" />
        </View>
      </ElevatedCard>
    );
  }

  const trendColor =
    delta?.status === 'danger' || delta?.status === 'blocked'
      ? colors.danger
      : delta?.status === 'warning' || delta?.status === 'atRisk'
        ? colors.warning
        : colors.chartMint;

  return (
    <ElevatedCard
      accessible
      accessibilityRole="text"
      accessibilityLabel={[label, value, delta?.label, stale].filter(Boolean).join(', ')}
      style={{ padding: 16 }}
    >
      <View className="gap-2">
        <Text variant="footnote" tone="soft">
          {label}
        </Text>
        <Text variant="kpi" tone={stale ? 'muted' : 'default'}>
          {value}
        </Text>

        <View className="flex-row items-center justify-between pt-0.5">
          {delta ? (
            <StatusBadge status={delta.status} label={delta.label} size="compact" />
          ) : (
            <View />
          )}
          {trendData && trendData.length > 1 ? (
            <MetricTrend data={trendData} color={trendColor} width={80} height={26} />
          ) : null}
        </View>

        {progress ? (
          <View className="gap-1.5 pt-1">
            <View className="flex-row items-center justify-between">
              <Text variant="caption" tone="muted">
                {progress.label ?? 'Project progress'}
              </Text>
              <Text variant="caption" tone="soft">
                {progress.value}%
              </Text>
            </View>
            <ProgressBar
              value={progress.value}
              plan={progress.plan}
              tone={
                progress.plan && progress.value < progress.plan
                  ? 'warning'
                  : progress.value >= 100
                    ? 'success'
                    : 'info'
              }
              label={progress.label ?? 'Project progress'}
            />
          </View>
        ) : null}

        {stale ? (
          <Text variant="caption" tone="muted">
            {stale}
          </Text>
        ) : null}

        {children}
      </View>
    </ElevatedCard>
  );
}
