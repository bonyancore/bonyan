import { useState } from 'react';
import { Modal, Pressable, StyleSheet, View } from 'react-native';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { SolidSurface } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius } from '@/design/tokens';

export type CompanyItem = {
  id: string;
  name: string;
  initials: string;
  role: string;
};

export type CompanySwitcherProps = {
  companies: readonly CompanyItem[];
  selectedId: string;
  onSelect: (id: string) => void;
};

export function CompanySwitcher({
  companies,
  selectedId,
  onSelect,
}: CompanySwitcherProps) {
  const [open, setOpen] = useState(false);
  const current = companies.find((c) => c.id === selectedId) || companies[0];

  return (
    <>
      <GlassSurface
        material="navigation"
        noHighlight
        radius={layout.appBarControl / 2}
        style={styles.triggerGlass}
      >
        <PressableScale
          onPress={() => setOpen(true)}
          accessibilityRole="button"
          accessibilityLabel={`Company: ${current?.name}`}
          style={styles.triggerInner}
        >
          <View className="h-6 w-6 items-center justify-center rounded-full bg-action">
            <Text variant="caption" tone="onAction" style={{ fontWeight: '700', fontSize: 10 }}>
              {current?.initials}
            </Text>
          </View>
          <Text variant="label" tone="default">
            {current?.name}
          </Text>
          <Icon name="chevronDown" size={14} color={colors.foregroundSoft} />
        </PressableScale>
      </GlassSurface>

      <Modal
        visible={open}
        transparent
        animationType="fade"
        onRequestClose={() => setOpen(false)}
      >
        <Pressable
          style={styles.scrim}
          onPress={() => setOpen(false)}
          accessibilityRole="button"
          accessibilityLabel="Close company switcher"
        >
          <Pressable style={styles.sheetContainer} onPress={(e) => e.stopPropagation()}>
            <SolidSurface style={styles.sheetContent}>
              <View className="gap-3 p-4">
                <Text variant="overline" tone="muted">
                  Companies on this device
                </Text>

                <View className="gap-1.5">
                  {companies.map((c) => {
                    const isSelected = c.id === selectedId;
                    return (
                      <PressableScale
                        key={c.id}
                        onPress={() => {
                          onSelect(c.id);
                          setOpen(false);
                        }}
                        className={`flex-row items-center justify-between rounded-control p-3 ${
                          isSelected ? 'bg-raised' : 'bg-transparent'
                        }`}
                      >
                        <View className="flex-row items-center gap-3">
                          <View
                            className={`h-9 w-9 items-center justify-center rounded-full ${
                              isSelected ? 'bg-action' : 'bg-sunken'
                            }`}
                          >
                            <Text
                              variant="footnote"
                              tone={isSelected ? 'onAction' : 'soft'}
                              style={{ fontWeight: '600' }}
                            >
                              {c.initials}
                            </Text>
                          </View>
                          <View className="gap-0.5">
                            <Text variant="bodyStrong" tone={isSelected ? 'default' : 'soft'}>
                              {c.name}
                            </Text>
                            <Text variant="caption" tone="muted">
                              {c.role}
                            </Text>
                          </View>
                        </View>
                        {isSelected ? (
                          <Icon name="check" size={18} color={colors.action} />
                        ) : null}
                      </PressableScale>
                    );
                  })}
                </View>
              </View>
            </SolidSurface>
          </Pressable>
        </Pressable>
      </Modal>
    </>
  );
}

const styles = StyleSheet.create({
  triggerGlass: {
    height: layout.appBarControl,
    borderRadius: layout.appBarControl / 2,
    alignSelf: 'flex-start',
    overflow: 'hidden',
  },
  triggerInner: {
    height: '100%',
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    paddingHorizontal: 12,
    borderRadius: layout.appBarControl / 2,
    overflow: 'hidden',
  },
  scrim: {
    flex: 1,
    backgroundColor: colors.scrim,
    justifyContent: 'center',
    paddingHorizontal: layout.gutter,
  },
  sheetContainer: {
    width: '100%',
    maxWidth: 400,
    alignSelf: 'center',
  },
  sheetContent: {
    borderRadius: radius.card,
  },
});
