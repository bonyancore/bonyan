import { LinearGradient } from 'expo-linear-gradient';
import { StyleSheet, View } from 'react-native';
import Animated, { LinearTransition, useReducedMotion } from 'react-native-reanimated';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon, type IconName } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { isRTL } from '@/core/i18n/direction';
import { colors, layout, shadows, spring } from '@/design/tokens';

export type TabItem = {
  key: string;
  label: string;
  icon: IconName;
  badge?: number;
};

export type FloatingTabBarProps = {
  tabs: readonly TabItem[];
  activeKey: string;
  createLabel: string;
  onSelect: (key: string) => void;
  onCreate: () => void;
};

export function FloatingTabBar({
  tabs,
  activeKey,
  createLabel,
  onSelect,
  onCreate,
}: FloatingTabBarProps) {
  const insets = useSafeAreaInsets();
  const reduced = useReducedMotion();
  const rtl = isRTL();

  const renderedTabs = rtl ? [...tabs].reverse() : tabs;

  return (
    <View
      pointerEvents="box-none"
      style={[
        styles.root,
        {
          bottom: insets.bottom + layout.tabBarOffset,
          flexDirection: rtl ? 'row-reverse' : 'row',
        },
      ]}
    >
      <GlassSurface
        material="navigation"
        radius={layout.tabBarHeight / 2}
        style={styles.bar}
      >
        <View style={styles.row} accessibilityRole="tablist">
          {renderedTabs.map((tab) => {
            const active = tab.key === activeKey;
            return (
              <Animated.View
                key={tab.key}
                layout={
                  reduced
                    ? undefined
                    : LinearTransition.springify()
                        .damping(spring.nav.damping)
                        .stiffness(spring.nav.stiffness)
                }
              >
                <PressableScale
                  haptic
                  onPress={() => onSelect(tab.key)}
                  accessibilityRole="tab"
                  accessibilityLabel={tab.label}
                  accessibilityState={{ selected: active }}
                  style={[styles.tab, active && styles.active]}
                >
                  <Icon
                    name={tab.icon}
                    size={layout.icon.lg}
                    color={active ? colors.action : colors.foregroundSoft}
                    strokeWidth={active ? 2.2 : 1.8}
                  />
                  {active ? (
                    <Text
                      variant="label"
                      tone="action"
                      numberOfLines={1}
                      style={{ fontWeight: '600' }}
                    >
                      {tab.label}
                    </Text>
                  ) : null}
                  {tab.badge ? (
                    <View style={styles.badge}>
                      <Text
                        variant="caption"
                        tone="onAction"
                        style={{ fontSize: 10, fontWeight: '700' }}
                      >
                        {tab.badge}
                      </Text>
                    </View>
                  ) : null}
                </PressableScale>
              </Animated.View>
            );
          })}
        </View>
      </GlassSurface>

      <PressableScale
        haptic
        onPress={onCreate}
        accessibilityRole="button"
        accessibilityLabel={createLabel}
        style={styles.fab}
      >
        <LinearGradient
          colors={[colors.actionHighlight, colors.actionStrong]}
          style={StyleSheet.absoluteFill}
          pointerEvents="none"
        />
        <Icon name="plus" size={layout.icon.xl} color={colors.onAction} strokeWidth={2.4} />
      </PressableScale>
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    position: 'absolute',
    start: layout.gutter,
    end: layout.gutter,
    alignItems: 'center',
    gap: 12,
  },
  bar: {
    flex: 1,
    height: layout.tabBarHeight,
    borderRadius: layout.tabBarHeight / 2,
    overflow: 'hidden',
  },
  row: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-around',
    paddingHorizontal: 6,
  },
  tab: {
    minWidth: layout.touchTarget,
    height: layout.touchTarget,
    borderRadius: layout.touchTarget / 2,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
    paddingHorizontal: 12,
  },
  active: {
    backgroundColor: 'rgba(255, 255, 255, 0.10)',
    borderWidth: 1,
    borderColor: colors.lineFocus,
    paddingHorizontal: 16,
  },
  badge: {
    position: 'absolute',
    top: 4,
    end: 4,
    minWidth: 18,
    height: 18,
    borderRadius: 9,
    paddingHorizontal: 4,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.action,
    borderWidth: 1.5,
    borderColor: '#10161D',
  },
  fab: {
    width: layout.fab,
    height: layout.fab,
    borderRadius: layout.fab / 2,
    alignItems: 'center',
    justifyContent: 'center',
    overflow: 'hidden',
    boxShadow: shadows.ember,
  },
});
