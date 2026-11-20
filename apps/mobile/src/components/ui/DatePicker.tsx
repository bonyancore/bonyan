import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { BottomSheet } from '@/components/ui/BottomSheet';
import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius } from '@/design/tokens';

export type DateQuickPreset = {
  key: string;
  label: string;
  dateStr: string;
};

export type DatePickerProps = {
  label: string;
  value: string;
  onChangeDate?: (dateStr: string) => void;
  presets?: readonly DateQuickPreset[];
  activePreset?: string;
};

const defaultPresets: DateQuickPreset[] = [
  { key: 'today', label: 'Today', dateStr: '18 Nov 2026' },
  { key: 'tomorrow', label: 'Tomorrow', dateStr: '19 Nov 2026' },
  { key: 'next_week', label: 'Next week', dateStr: '25 Nov 2026' },
];

const daysInMonth = Array.from({ length: 30 }, (_, i) => i + 1);

export function DatePicker({
  label,
  value,
  onChangeDate,
  presets = defaultPresets,
  activePreset = 'tomorrow',
}: DatePickerProps) {
  const [sheetOpen, setSheetOpen] = useState(false);
  const [selectedDay, setSelectedDay] = useState(24);

  return (
    <View className="gap-2">
      <Text variant="footnote" tone="soft">
        {label}
      </Text>

      <PressableScale
        onPress={() => setSheetOpen(true)}
        accessibilityRole="button"
        accessibilityLabel={`${label}: ${value}`}
        style={styles.box}
      >
        <Icon name="calendar" size={layout.icon.md} color={colors.foregroundSoft} />
        <Text variant="body" tone="default">
          {value}
        </Text>
      </PressableScale>

      {presets.length > 0 ? (
        <View className="flex-row items-center gap-2">
          {presets.map((preset) => {
            const active = preset.key === activePreset;
            return (
              <PressableScale
                key={preset.key}
                onPress={() => onChangeDate?.(preset.dateStr)}
                className={`h-8 flex-1 items-center justify-center rounded-chip ${
                  active ? 'bg-raised border border-action' : 'bg-surface border border-line'
                }`}
              >
                <Text
                  variant="caption"
                  tone={active ? 'action' : 'soft'}
                  style={{ fontWeight: active ? '600' : '500' }}
                >
                  {preset.label}
                </Text>
              </PressableScale>
            );
          })}
        </View>
      ) : null}

      <BottomSheet
        visible={sheetOpen}
        onClose={() => setSheetOpen(false)}
        title={label}
      >
        <View className="gap-4 pb-4">
          <View className="flex-row items-center justify-between px-2">
            <Text variant="headline">November 2026</Text>
            <View className="flex-row items-center gap-2">
              <Icon name="chevronLeft" size={16} color={colors.foregroundSoft} />
              <Icon name="chevronRight" size={16} color={colors.foregroundSoft} />
            </View>
          </View>

          <View style={styles.calendarGrid}>
            {daysInMonth.map((day) => {
              const isSelected = day === selectedDay;
              return (
                <PressableScale
                  key={day}
                  onPress={() => setSelectedDay(day)}
                  style={[
                    styles.dayCell,
                    isSelected && styles.selectedDayCell,
                  ]}
                >
                  <Text
                    variant="footnote"
                    tone={isSelected ? 'onAction' : 'default'}
                    style={{ fontWeight: isSelected ? '700' : '400' }}
                  >
                    {String(day)}
                  </Text>
                </PressableScale>
              );
            })}
          </View>

          <Button
            label="Confirm date"
            variant="primary"
            onPress={() => {
              onChangeDate?.(`${selectedDay} Nov 2026`);
              setSheetOpen(false);
            }}
          />
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
    gap: 10,
    paddingHorizontal: 14,
  },
  calendarGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    justifyContent: 'center',
    paddingVertical: 8,
  },
  dayCell: {
    width: 38,
    height: 38,
    borderRadius: 19,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.surface,
  },
  selectedDayCell: {
    backgroundColor: colors.action,
  },
});
