import { StyleSheet, TextInput, View } from 'react-native';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius } from '@/design/tokens';

export type SearchFilter = {
  key: string;
  label: string;
};

export type SearchFieldProps = {
  value: string;
  onChangeText: (text: string) => void;
  placeholder?: string;
  filters?: readonly SearchFilter[];
  activeFilter?: string;
  onFilterChange?: (key: string) => void;
};

const defaultFilters: readonly SearchFilter[] = [
  { key: 'all', label: 'All' },
  { key: 'overdue', label: 'Overdue' },
  { key: 'mine', label: 'Mine' },
];

export function SearchField({
  value,
  onChangeText,
  placeholder = 'Search tasks, POs, people',
  filters = defaultFilters,
  activeFilter = 'all',
  onFilterChange,
}: SearchFieldProps) {
  return (
    <View className="gap-2.5">
      <GlassSurface material="navigation" style={styles.capsule}>
        <View style={styles.inputRow}>
          <Icon name="search" size={layout.icon.md} color={colors.foregroundMuted} />
          <TextInput
            value={value}
            onChangeText={onChangeText}
            placeholder={placeholder}
            placeholderTextColor={colors.foregroundMuted}
            style={styles.input}
          />
          {value ? (
            <PressableScale
              onPress={() => onChangeText('')}
              accessibilityRole="button"
              accessibilityLabel="Clear"
              style={styles.clearBtn}
            >
              <Icon name="close" size={14} color={colors.foregroundMuted} />
            </PressableScale>
          ) : null}
        </View>
      </GlassSurface>

      {filters && filters.length > 0 ? (
        <View className="flex-row items-center gap-2">
          {filters.map((f) => {
            const active = f.key === activeFilter;
            return (
              <PressableScale
                key={f.key}
                onPress={() => onFilterChange?.(f.key)}
                accessibilityRole="tab"
                accessibilityLabel={f.label}
                accessibilityState={{ selected: active }}
                style={styles.filterHitArea}
              >
                <View
                  style={[
                    styles.filterChip,
                    active ? styles.activeChip : styles.inactiveChip,
                  ]}
                >
                  <Text
                    variant="footnote"
                    tone={active ? 'action' : 'soft'}
                    style={{ fontWeight: active ? '600' : '500' }}
                  >
                    {f.label}
                  </Text>
                </View>
              </PressableScale>
            );
          })}
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  capsule: {
    height: 48,
    borderRadius: 24,
    overflow: 'hidden',
  },
  inputRow: {
    height: 48,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    gap: 10,
  },
  input: {
    flex: 1,
    color: colors.foreground,
    fontSize: 15,
    padding: 0,
  },
  clearBtn: {
    padding: 4,
  },
  filterHitArea: {
    minHeight: 44,
    justifyContent: 'center',
  },
  filterChip: {
    height: 36,
    paddingHorizontal: 14,
    borderRadius: radius.chip,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
  },
  activeChip: {
    backgroundColor: colors.actionTint,
    borderColor: colors.action,
  },
  inactiveChip: {
    backgroundColor: colors.surface,
    borderColor: colors.lineStrong,
  },
});
