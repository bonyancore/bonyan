import { StyleSheet, View } from 'react-native';

import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { isRTL } from '@/core/i18n/direction';
import { colors, radius } from '@/design/tokens';

export type ScopeOption = 'company' | 'project' | 'mine';

export type ScopeItem = {
  key: ScopeOption;
  label: string;
};

export type ScopeSelectorProps = {
  options?: readonly ScopeItem[];
  activeScope: ScopeOption;
  onSelectScope: (scope: ScopeOption) => void;
};

const defaultOptions: readonly ScopeItem[] = [
  { key: 'company', label: 'Company' },
  { key: 'project', label: 'Project' },
  { key: 'mine', label: 'Mine' },
];

export function ScopeSelector({
  options = defaultOptions,
  activeScope,
  onSelectScope,
}: ScopeSelectorProps) {
  const rtl = isRTL();

  return (
    <View
      accessibilityRole="tablist"
      accessibilityLabel="Scope selection"
      style={[styles.container, rtl && styles.rtlContainer]}
    >
      {options.map((opt) => {
        const active = opt.key === activeScope;
        return (
          <PressableScale
            key={opt.key}
            haptic
            onPress={() => onSelectScope(opt.key)}
            accessibilityRole="tab"
            accessibilityLabel={opt.label}
            accessibilityState={{ selected: active }}
            style={[styles.segment, active && styles.activeSegment]}
          >
            <Text
              variant="footnote"
              tone={active ? 'default' : 'muted'}
              style={{ fontWeight: active ? '600' : '500' }}
            >
              {opt.label}
            </Text>
          </PressableScale>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    height: 44,
    backgroundColor: colors.surface,
    borderRadius: radius.control,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.07)',
    flexDirection: 'row',
    alignItems: 'center',
    padding: 3,
  },
  rtlContainer: {
    flexDirection: 'row-reverse',
  },
  segment: {
    flex: 1,
    height: '100%',
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: radius.control - 3,
  },
  activeSegment: {
    backgroundColor: colors.raised,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.12)',
  },
});
