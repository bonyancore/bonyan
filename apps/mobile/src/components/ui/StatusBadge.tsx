import { StyleSheet, View } from 'react-native';

import { Icon, type IconName } from '@/components/ui/Icon';
import { Text, type TextTone } from '@/components/ui/Text';
import { isRTL } from '@/core/i18n/direction';
import { colors, radius } from '@/design/tokens';

export type StatusName =
  | 'onTrack'
  | 'inProgress'
  | 'atRisk'
  | 'blocked'
  | 'draft'
  | 'success'
  | 'info'
  | 'warning'
  | 'danger'
  | 'neutral';

export type StatusBadgeVariant = 'standard' | 'compact' | 'dot' | 'count';

type StatusConfig = {
  bg: string;
  fg: string;
  tone: TextTone;
  icon?: IconName;
};

const statusMap: Record<string, StatusConfig> = {
  onTrack: { bg: colors.successTint, fg: colors.success, tone: 'success', icon: 'check' },
  success: { bg: colors.successTint, fg: colors.success, tone: 'success', icon: 'check' },
  inProgress: { bg: colors.infoTint, fg: colors.info, tone: 'info', icon: 'arrowRight' },
  info: { bg: colors.infoTint, fg: colors.info, tone: 'info', icon: 'arrowRight' },
  atRisk: { bg: colors.warningTint, fg: colors.warning, tone: 'warning', icon: 'target' },
  warning: { bg: colors.warningTint, fg: colors.warning, tone: 'warning', icon: 'target' },
  blocked: { bg: colors.dangerTint, fg: colors.danger, tone: 'danger', icon: 'warning' },
  danger: { bg: colors.dangerTint, fg: colors.danger, tone: 'danger', icon: 'warning' },
  draft: { bg: 'rgba(255, 255, 255, 0.08)', fg: colors.foregroundSoft, tone: 'soft' },
  neutral: { bg: 'rgba(255, 255, 255, 0.08)', fg: colors.foregroundSoft, tone: 'soft' },
};

export type StatusBadgeProps = {
  status: StatusName;
  label?: string;
  variant?: StatusBadgeVariant;
  size?: 'standard' | 'compact';
  count?: number;
  icon?: React.ReactNode;
};

export function StatusBadge({
  status,
  label = '',
  variant = 'standard',
  size = 'standard',
  count,
  icon,
}: StatusBadgeProps) {
  const rtl = isRTL();
  const spec = statusMap[status] ?? statusMap.neutral;
  const isCompact = size === 'compact' || variant === 'compact';

  if (variant === 'count' || count !== undefined) {
    const displayCount = count !== undefined ? String(count) : label;
    return (
      <View
        accessibilityRole="text"
        accessibilityLabel={`Count: ${displayCount}`}
        style={styles.countBadge}
      >
        <Text variant="caption" tone="onAction" style={{ fontWeight: '700' }}>
          {displayCount}
        </Text>
      </View>
    );
  }

  if (variant === 'dot') {
    return (
      <View
        accessibilityRole="text"
        accessibilityLabel={label}
        style={styles.dotRow}
      >
        <View style={[styles.dot, { backgroundColor: spec.fg }]} />
        {label ? (
          <Text variant={isCompact ? 'caption' : 'footnote'} tone="soft">
            {label}
          </Text>
        ) : null}
      </View>
    );
  }

  const iconName = spec.icon === 'arrowRight' && rtl ? 'arrowLeft' : spec.icon;
  const iconSize = isCompact ? 11 : 13;

  return (
    <View
      accessibilityRole="text"
      accessibilityLabel={label}
      style={[
        styles.base,
        isCompact && styles.compact,
        { backgroundColor: spec.bg },
      ]}
    >
      {icon ? (
        icon
      ) : iconName ? (
        <Icon name={iconName} size={iconSize} color={spec.fg} strokeWidth={2.4} />
      ) : null}
      <Text
        variant={isCompact ? 'caption' : 'label'}
        tone={spec.tone}
        style={{ fontWeight: '500' }}
      >
        {label}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  base: {
    height: 28,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    alignSelf: 'flex-start',
    borderRadius: radius.chip,
    paddingHorizontal: 10,
  },
  compact: {
    height: 22,
    gap: 4,
    paddingHorizontal: 7,
  },
  dotRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    alignSelf: 'flex-start',
  },
  dot: {
    width: 6,
    height: 6,
    borderRadius: 3,
  },
  countBadge: {
    minWidth: 20,
    height: 20,
    borderRadius: 10,
    paddingHorizontal: 6,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.action,
  },
});
