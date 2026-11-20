import { useState } from 'react';
import { Modal, Pressable, StyleSheet, TextInput, View } from 'react-native';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { SolidSurface } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius } from '@/design/tokens';

export type ProjectItem = {
  id: string;
  name: string;
  progress: number;
  statusColor?: string;
};

export type ProjectSwitcherProps = {
  projects: readonly ProjectItem[];
  selectedId: string;
  onSelect: (id: string) => void;
  onViewAll?: () => void;
};

export function ProjectSwitcher({
  projects,
  selectedId,
  onSelect,
  onViewAll,
}: ProjectSwitcherProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');

  const current = projects.find((p) => p.id === selectedId) || projects[0];
  const filtered = projects.filter((p) =>
    p.name.toLowerCase().includes(query.toLowerCase()),
  );

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
          accessibilityLabel={`Selected project: ${current?.name}`}
          style={styles.triggerInner}
        >
          <View
            className="h-2 w-2 rounded-full"
            style={{ backgroundColor: current?.statusColor || colors.action }}
          />
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
          accessibilityLabel="Close project switcher"
        >
          <Pressable style={styles.sheetContainer} onPress={(e) => e.stopPropagation()}>
            <SolidSurface style={styles.sheetContent}>
              <View className="gap-3 p-4">
                <View className="h-10 flex-row items-center gap-2 rounded-control bg-sunken px-3">
                  <Icon name="search" size={16} color={colors.foregroundMuted} />
                  <TextInput
                    value={query}
                    onChangeText={setQuery}
                    placeholder="Search projects..."
                    placeholderTextColor={colors.foregroundMuted}
                    style={styles.input}
                  />
                  {query ? (
                    <PressableScale onPress={() => setQuery('')}>
                      <Icon name="close" size={14} color={colors.foregroundMuted} />
                    </PressableScale>
                  ) : null}
                </View>

                <View className="gap-1">
                  {filtered.map((item) => {
                    const isSelected = item.id === selectedId;
                    return (
                      <PressableScale
                        key={item.id}
                        onPress={() => {
                          onSelect(item.id);
                          setOpen(false);
                        }}
                        className={`flex-row items-center justify-between rounded-control p-3 ${
                          isSelected ? 'bg-raised' : 'bg-transparent'
                        }`}
                      >
                        <View className="flex-row items-center gap-2.5">
                          <View
                            className="h-2 w-2 rounded-full"
                            style={{ backgroundColor: item.statusColor || colors.chartMint }}
                          />
                          <Text variant="body" tone={isSelected ? 'default' : 'soft'}>
                            {item.name}
                          </Text>
                        </View>
                        <View className="flex-row items-center gap-2">
                          <Text variant="footnote" tone="muted">
                            {`${item.progress}%`}
                          </Text>
                          {isSelected ? (
                            <Icon name="check" size={16} color={colors.action} />
                          ) : null}
                        </View>
                      </PressableScale>
                    );
                  })}
                </View>

                {onViewAll ? (
                  <PressableScale
                    onPress={() => {
                      setOpen(false);
                      onViewAll();
                    }}
                    className="border-t border-line pt-2"
                  >
                    <Text variant="label" tone="action" className="text-center">
                      All projects
                    </Text>
                  </PressableScale>
                ) : null}
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
  input: {
    flex: 1,
    color: colors.foreground,
    fontSize: 14,
    padding: 0,
  },
});
