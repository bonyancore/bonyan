import { useState } from 'react';
import { ScrollView, StyleSheet, TextInput, View } from 'react-native';

import { BottomSheet } from '@/components/ui/BottomSheet';
import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius } from '@/design/tokens';

export type SelectOption = {
  value: string;
  label: string;
};

export type SelectFieldProps = {
  label: string;
  value: string;
  options: readonly SelectOption[];
  onSelect: (value: string) => void;
  placeholder?: string;
};

export function SelectField({
  label,
  value,
  options,
  onSelect,
  placeholder = 'Select option...',
}: SelectFieldProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const current = options.find((o) => o.value === value);

  const hasSearch = options.length > 8;
  const filtered = hasSearch
    ? options.filter((o) => o.label.toLowerCase().includes(query.toLowerCase()))
    : options;

  return (
    <View className="gap-1.5">
      <Text variant="footnote" tone="soft">
        {label}
      </Text>

      <PressableScale
        onPress={() => setOpen(true)}
        accessibilityRole="combobox"
        accessibilityLabel={`${label}: ${current?.label || placeholder}`}
        style={styles.box}
      >
        <Text variant="body" tone={current ? 'default' : 'muted'}>
          {current?.label || placeholder}
        </Text>
        <Icon name="chevronDown" size={16} color={colors.foregroundSoft} />
      </PressableScale>

      <BottomSheet
        visible={open}
        onClose={() => {
          setOpen(false);
          setQuery('');
        }}
        title={label}
      >
        <View className="gap-3 pb-4">
          {hasSearch ? (
            <View className="h-10 flex-row items-center gap-2 rounded-control bg-sunken px-3 border border-line">
              <Icon name="search" size={16} color={colors.foregroundMuted} />
              <TextInput
                value={query}
                onChangeText={setQuery}
                placeholder="Search..."
                placeholderTextColor={colors.foregroundMuted}
                style={styles.searchInput}
              />
              {query ? (
                <PressableScale onPress={() => setQuery('')}>
                  <Icon name="close" size={14} color={colors.foregroundMuted} />
                </PressableScale>
              ) : null}
            </View>
          ) : null}

          <ScrollView style={{ maxHeight: 300 }}>
            <View className="gap-1">
              {filtered.map((opt) => {
                const isSelected = opt.value === value;
                return (
                  <PressableScale
                    key={opt.value}
                    onPress={() => {
                      onSelect(opt.value);
                      setOpen(false);
                      setQuery('');
                    }}
                    className={`flex-row items-center justify-between rounded-control p-3.5 ${
                      isSelected ? 'bg-action-tint border border-action' : 'bg-surface'
                    }`}
                  >
                    <Text
                      variant="body"
                      tone={isSelected ? 'action' : 'default'}
                      style={{ fontWeight: isSelected ? '600' : '400' }}
                    >
                      {opt.label}
                    </Text>
                    {isSelected ? (
                      <Icon name="check" size={18} color={colors.action} strokeWidth={2.4} />
                    ) : null}
                  </PressableScale>
                );
              })}
            </View>
          </ScrollView>
        </View>
      </BottomSheet>
    </View>
  );
}

const styles = StyleSheet.create({
  box: {
    height: layout.controlHeight,
    borderRadius: radius.control,
    backgroundColor: colors.sunken,
    borderWidth: 1,
    borderColor: colors.lineStrong,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 14,
  },
  searchInput: {
    flex: 1,
    color: colors.foreground,
    fontSize: 14,
    padding: 0,
  },
});
