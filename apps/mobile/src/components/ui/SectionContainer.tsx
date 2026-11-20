import { useState } from 'react';
import { View, type ViewProps } from 'react-native';

import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { SolidSurface } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type SectionContainerProps = ViewProps & {
  title: string;
  actionLabel?: string;
  onAction?: () => void;
  badgeCount?: number;
  badgeTone?: 'danger' | 'action' | 'muted';
  collapsible?: boolean;
  defaultCollapsed?: boolean;
  solidBody?: boolean;
};

export function SectionContainer({
  title,
  actionLabel,
  onAction,
  badgeCount,
  badgeTone = 'danger',
  collapsible = false,
  defaultCollapsed = false,
  solidBody = false,
  children,
  className,
  style,
  ...props
}: SectionContainerProps) {
  const [collapsed, setCollapsed] = useState(defaultCollapsed);

  return (
    <View className={`gap-2.5 ${className || ''}`} style={style} {...props}>
      <View className="flex-row items-center justify-between">
        <PressableScale
          onPress={collapsible ? () => setCollapsed(!collapsed) : undefined}
          disabled={!collapsible}
          className="flex-row items-center gap-2"
        >
          <Text variant="title2">{title}</Text>
          {badgeCount !== undefined ? (
            <View
              className={`min-w-5 h-5 items-center justify-center rounded-full px-1.5 ${
                badgeTone === 'danger'
                  ? 'bg-danger'
                  : badgeTone === 'action'
                    ? 'bg-action'
                    : 'bg-raised'
              }`}
            >
              <Text
                variant="caption"
                tone={badgeTone === 'action' ? 'onAction' : 'default'}
                style={{ fontSize: 10, fontWeight: '700' }}
              >
                {badgeCount}
              </Text>
            </View>
          ) : null}
          {collapsible ? (
            <Icon
              name={collapsed ? 'chevronDown' : 'chevronUp'}
              size={16}
              color={colors.foregroundSoft}
            />
          ) : null}
        </PressableScale>

        {actionLabel && onAction ? (
          <PressableScale
            onPress={onAction}
            accessibilityRole="button"
            accessibilityLabel={actionLabel}
          >
            <Text variant="label" tone="action">
              {actionLabel}
            </Text>
          </PressableScale>
        ) : null}
      </View>

      {!collapsed && (
        solidBody ? (
          <SolidSurface style={{ overflow: 'hidden' }}>{children}</SolidSurface>
        ) : (
          children
        )
      )}
    </View>
  );
}
