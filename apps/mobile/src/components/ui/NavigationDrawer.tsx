import { Modal, Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon, type IconName } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { isRTL } from '@/core/i18n/direction';
import { colors, layout } from '@/design/tokens';

export type DrawerNavItem = {
  key: string;
  label: string;
  icon: IconName;
};

export type NavigationDrawerProps = {
  visible: boolean;
  onClose: () => void;
  userName: string;
  userRole: string;
  userAvatar: string;
  items: readonly DrawerNavItem[];
  activeKey: string;
  onSelect: (key: string) => void;
  onLanguageToggle?: () => void;
  currentLanguage?: 'en' | 'ar';
  onSettingsPress?: () => void;
};

export function NavigationDrawer({
  visible,
  onClose,
  userName,
  userRole,
  userAvatar,
  items,
  activeKey,
  onSelect,
  onLanguageToggle,
  currentLanguage = 'en',
  onSettingsPress,
}: NavigationDrawerProps) {
  const insets = useSafeAreaInsets();
  const rtl = isRTL();

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={styles.scrim} onPress={onClose}>
        <Pressable
          style={[
            styles.drawer,
            rtl ? styles.drawerRTL : styles.drawerLTR,
            { paddingTop: insets.top + 24, paddingBottom: insets.bottom + 24 },
          ]}
          onPress={(e) => e.stopPropagation()}
        >
          <GlassSurface material="overlay" style={StyleSheet.absoluteFill} />

          <View className="flex-1 justify-between p-5">
            <View className="gap-6">
              <View className="flex-row items-center gap-3 border-b border-line pb-4">
                <View className="h-12 w-12 items-center justify-center rounded-full bg-raised">
                  <Text variant="headline" tone="action">
                    {userAvatar}
                  </Text>
                </View>
                <View className="gap-0.5">
                  <Text variant="title2">{userName}</Text>
                  <Text variant="footnote" tone="muted">
                    {userRole}
                  </Text>
                </View>
              </View>

              <View className="gap-1">
                {items.map((item) => {
                  const active = item.key === activeKey;
                  return (
                    <PressableScale
                      key={item.key}
                      onPress={() => {
                        onSelect(item.key);
                        onClose();
                      }}
                      className={`flex-row items-center gap-3 rounded-control p-3 ${
                        active ? 'bg-action-tint' : 'bg-transparent'
                      }`}
                    >
                      <Icon
                        name={item.icon}
                        size={layout.icon.lg}
                        color={active ? colors.action : colors.foregroundSoft}
                      />
                      <Text
                        variant="bodyStrong"
                        tone={active ? 'action' : 'soft'}
                      >
                        {item.label}
                      </Text>
                    </PressableScale>
                  );
                })}
              </View>
            </View>

            <View className="gap-3 border-t border-line pt-4">
              <View className="flex-row items-center justify-between">
                {onLanguageToggle ? (
                  <PressableScale
                    onPress={onLanguageToggle}
                    className="flex-row items-center gap-1.5 rounded-chip bg-raised px-3 py-1.5"
                  >
                    <Text variant="caption" tone="soft" style={{ fontWeight: '500' }}>
                      {rtl ? 'English · العربية' : 'English · العربية'}
                    </Text>
                  </PressableScale>
                ) : null}

                {onSettingsPress ? (
                  <PressableScale
                    onPress={onSettingsPress}
                    accessibilityRole="button"
                    accessibilityLabel="Settings"
                    className="flex-row items-center gap-1.5 p-1.5"
                  >
                    <Icon name="settings" size={16} color={colors.foregroundSoft} />
                    <Text variant="caption" tone="muted">
                      {rtl ? 'الإعدادات' : 'Settings'}
                    </Text>
                  </PressableScale>
                ) : null}
              </View>
            </View>
          </View>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  scrim: {
    flex: 1,
    backgroundColor: colors.scrim,
    flexDirection: 'row',
  },
  drawer: {
    width: 300,
    height: '100%',
    overflow: 'hidden',
  },
  drawerLTR: {
    borderTopRightRadius: 24,
    borderBottomRightRadius: 24,
  },
  drawerRTL: {
    borderTopLeftRadius: 24,
    borderBottomLeftRadius: 24,
  },
});
