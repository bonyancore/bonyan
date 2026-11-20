import { useTranslation } from 'react-i18next';
import { View } from 'react-native';

import { Button } from '@/components/ui/Button';
import { StatusBadge, type StatusName } from '@/components/ui/StatusBadge';
import { ElevatedCard } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';

export type ApprovalCardProps = {
  code: string;
  urgencyLabel?: string;
  urgencyStatus?: StatusName;
  waitingTime?: string;
  title: string;
  amountLabel?: string;
  amount: string;
  approverName?: string;
  approveLabel?: string;
  rejectLabel?: string;
  canAct?: boolean;
  onApprove?: () => void;
  onReject?: () => void;
};

export function ApprovalCard({
  code,
  urgencyLabel,
  urgencyStatus,
  waitingTime = 'Waiting 2 days',
  title,
  amountLabel = 'Best quote',
  amount,
  approverName,
  approveLabel,
  rejectLabel,
  canAct = true,
  onApprove,
  onReject,
}: ApprovalCardProps) {
  const { t } = useTranslation();

  return (
    <ElevatedCard style={{ padding: 16 }}>
      <View className="gap-3">
        <View className="flex-row items-center justify-between">
          <Text variant="overline" tone="muted">
            {code}
          </Text>
          {urgencyLabel && urgencyStatus ? (
            <StatusBadge status={urgencyStatus} label={urgencyLabel} size="compact" />
          ) : waitingTime ? (
            <Text variant="caption" tone="muted">
              {waitingTime}
            </Text>
          ) : null}
        </View>

        <Text variant="headline" numberOfLines={2}>
          {title}
        </Text>

        <View className="flex-row items-baseline justify-between border-b border-line pb-3">
          <Text variant="footnote" tone="soft">
            {amountLabel}
          </Text>
          <Text variant="figure" tone="default">
            {amount}
          </Text>
        </View>

        {canAct ? (
          <View className="flex-row items-center gap-3">
            <View className="flex-1">
              <Button
                label={rejectLabel ?? t('action.reject', { defaultValue: 'Reject' })}
                variant="secondary"
                onPress={onReject}
              />
            </View>
            <View className="flex-1">
              <Button
                label={approveLabel ?? t('action.approve', { defaultValue: 'Approve' })}
                variant="primary"
                onPress={onApprove}
              />
            </View>
          </View>
        ) : approverName ? (
          <View className="flex-row items-center justify-between pt-1">
            <Text variant="caption" tone="muted">
              {t('status.awaiting', { defaultValue: 'Awaiting decision by' })}
            </Text>
            <Text variant="caption" tone="soft" style={{ fontWeight: '600' }}>
              {approverName}
            </Text>
          </View>
        ) : null}
      </View>
    </ElevatedCard>
  );
}
