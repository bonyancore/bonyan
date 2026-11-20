import { StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon, type IconName } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { isRTL } from '@/core/i18n/direction';
import { colors, layout } from '@/design/tokens';

export type GlassIconButtonProps = {
  icon: IconName;
  label: string;
  onPress: () => void;
  mirror?: boolean;
};

export function GlassIconButton({ icon, label, onPress, mirror = false }: GlassIconButtonProps) {
  const rtl = isRTL();
  const shouldMirror = mirror || (rtl && (icon === 'chevronLeft' || icon === 'chevronRight'));

  return (
    <PressableScale haptic onPress={onPress} accessibilityRole="button" accessibilityLabel={label}>
      <GlassSurface
        material="navigation"
        radius={layout.appBarControl / 2}
        noHighlight
        style={styles.iconGlass}
      >
        <View
          style={[styles.iconButtonInner, shouldMirror ? { transform: [{ scaleX: -1 }] } : undefined]}
        >
          <Icon name={icon} color={colors.foreground} size={layout.icon.lg} />
        </View>
      </GlassSurface>
    </PressableScale>
  );
}

export type AppBarProps = {
  variant?: 'large' | 'compact';
  title?: string;
  subtitle?: string;
  companyName?: string;
  companyAvatar?: string;
  projectName?: string;
  onCompanyPress?: () => void;
  onSearchPress?: () => void;
  onNotificationPress?: () => void;
  hasUnreadNotifications?: boolean | number;
  onBackPress?: () => void;
  onOptionsPress?: () => void;
  children?: React.ReactNode;
};

export function AppBar({
  variant = 'large',
  title,
  subtitle,
  companyName = 'Delta Build',
  companyAvatar = 'DB',
  projectName,
  onCompanyPress,
  onSearchPress,
  onNotificationPress,
  hasUnreadNotifications = true,
  onBackPress,
  onOptionsPress,
  children,
}: AppBarProps) {
  const insets = useSafeAreaInsets();

  if (variant === 'compact') {
    return (
      <View style={[styles.compactContainer, { paddingTop: insets.top + 8 }]}>
        <View className="flex-row items-center justify-between">
          <View className="flex-row items-center gap-3">
            {onBackPress ? (
              <GlassIconButton
                icon="chevronLeft"
                label="Back"
                onPress={onBackPress}
                mirror
              />
            ) : null}
            <View className="gap-0.5">
              <Text variant="headline" numberOfLines={1}>
                {title}
              </Text>
              {subtitle ? (
                <Text variant="caption" tone="muted" numberOfLines={1}>
                  {subtitle}
                </Text>
              ) : null}
            </View>
          </View>

          {onOptionsPress ? (
            <GlassIconButton
              icon="dots"
              label="More options"
              onPress={onOptionsPress}
            />
          ) : null}
        </View>
      </View>
    );
  }

  const notificationCount =
    typeof hasUnreadNotifications === 'number' ? hasUnreadNotifications : undefined;
  const showUnread = Boolean(hasUnreadNotifications);

  return (
    <View style={[styles.largeContainer, { paddingTop: insets.top + 8 }]}>
      <View className="flex-row items-center justify-between">
        <GlassSurface
          material="navigation"
          radius={layout.appBarControl / 2}
          noHighlight
          style={styles.companyPill}
        >
          <PressableScale
            onPress={onCompanyPress}
            accessibilityRole="button"
            accessibilityLabel={`Context: ${companyName}${projectName ? ` / ${projectName}` : ''}`}
            style={styles.companyPillInner}
          >
            <View className="h-6 w-6 items-center justify-center rounded-full bg-action">
              <Text variant="caption" tone="onAction" style={{ fontWeight: '700', fontSize: 10 }}>
                {companyAvatar}
              </Text>
            </View>
            <Text variant="label" tone="default" numberOfLines={1}>
              {companyName}
              {projectName ? (
                <Text variant="label" tone="muted">
                  {' / '}
                  <Text variant="label" tone="default">
                    {projectName}
                  </Text>
                </Text>
              ) : null}
            </Text>
            <Icon name="chevronDown" size={14} color={colors.foregroundSoft} />
          </PressableScale>
        </GlassSurface>

        <View className="flex-row items-center gap-2">
          {onSearchPress ? (
            <GlassSurface
              material="navigation"
              radius={layout.appBarControl / 2}
              noHighlight
              style={styles.iconGlass}
            >
              <PressableScale
                onPress={onSearchPress}
                accessibilityRole="button"
                accessibilityLabel="Search"
                style={styles.iconButtonInner}
              >
                <Icon name="search" size={layout.icon.md} color={colors.foregroundSoft} />
              </PressableScale>
            </GlassSurface>
          ) : null}

          {onNotificationPress ? (
            <GlassSurface
              material="navigation"
              radius={layout.appBarControl / 2}
              noHighlight
              style={styles.iconGlass}
            >
              <PressableScale
                onPress={onNotificationPress}
                accessibilityRole="button"
                accessibilityLabel={
                  notificationCount
                    ? `${notificationCount} unread notifications`
                    : 'Notifications'
                }
                style={styles.iconButtonInner}
              >
                <Icon name="bell" size={layout.icon.md} color={colors.foregroundSoft} />
                {showUnread ? (
                  notificationCount !== undefined ? (
                    <View style={styles.unreadBadge}>
                      <Text
                        variant="caption"
                        tone="onAction"
                        style={{ fontSize: 9, fontWeight: '700', color: '#FFFFFF' }}
                      >
                        {notificationCount}
                      </Text>
                    </View>
                  ) : (
                    <View style={styles.unreadDot} />
                  )
                ) : null}
              </PressableScale>
            </GlassSurface>
          ) : null}
        </View>
      </View>

      {title ? (
        <View className="mt-4 gap-0.5">
          {subtitle ? (
            <Text variant="footnote" tone="muted">
              {subtitle}
            </Text>
          ) : null}
          <Text variant="display">{title}</Text>
        </View>
      ) : null}

      {children ? <View className="mt-4">{children}</View> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  largeContainer: {
    paddingHorizontal: layout.gutter,
    paddingBottom: 12,
  },
  compactContainer: {
    paddingHorizontal: layout.gutter,
    paddingBottom: 12,
  },
  companyPill: {
    height: layout.appBarControl,
    borderRadius: layout.appBarControl / 2,
    maxWidth: '72%',
    overflow: 'hidden',
  },
  companyPillInner: {
    height: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 12,
    borderRadius: layout.appBarControl / 2,
    overflow: 'hidden',
  },
  iconGlass: {
    width: layout.appBarControl,
    height: layout.appBarControl,
    borderRadius: layout.appBarControl / 2,
    overflow: 'hidden',
    alignItems: 'center',
    justifyContent: 'center',
  },
  iconButtonInner: {
    width: '100%',
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: layout.appBarControl / 2,
    overflow: 'hidden',
  },
  unreadDot: {
    position: 'absolute',
    top: 10,
    end: 11,
    width: 6,
    height: 6,
    borderRadius: 3,
    backgroundColor: colors.danger,
  },
  unreadBadge: {
    position: 'absolute',
    top: 7,
    end: 7,
    minWidth: 14,
    height: 14,
    borderRadius: 7,
    paddingHorizontal: 3,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.danger,
  },
});
