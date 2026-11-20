import { useState } from 'react';
import {
  StyleSheet,
  TextInput,
  View,
  type TextInputProps,
} from 'react-native';

import { Icon } from '@/components/ui/Icon';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius } from '@/design/tokens';

export type TextFieldProps = Omit<TextInputProps, 'style'> & {
  label: string;
  error?: string;
  hint?: string;
  unit?: string;
  readOnly?: boolean;
};

export function TextField({
  label,
  value,
  placeholder,
  error,
  hint,
  unit,
  readOnly = false,
  ...props
}: TextFieldProps) {
  const [focused, setFocused] = useState(false);

  return (
    <View className="gap-1.5">
      <Text variant="footnote" tone={error ? 'danger' : 'soft'}>
        {label}
      </Text>

      <View
        style={[
          styles.container,
          readOnly && styles.readOnly,
          focused && styles.focused,
          Boolean(error) && styles.error,
        ]}
      >
        <TextInput
          {...props}
          value={value}
          placeholder={placeholder}
          placeholderTextColor={colors.foregroundMuted}
          editable={!readOnly}
          onFocus={() => setFocused(true)}
          onBlur={() => setFocused(false)}
          style={styles.input}
        />
        {unit ? (
          <Text variant="footnote" tone="muted">
            {unit}
          </Text>
        ) : null}
      </View>

      {error ? (
        <View className="flex-row items-center gap-1.5 pt-0.5">
          <Icon name="warning" size={14} color={colors.danger} />
          <Text variant="caption" tone="danger">
            {error}
          </Text>
        </View>
      ) : hint ? (
        <Text variant="caption" tone="muted">
          {hint}
        </Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    height: layout.controlHeight,
    borderRadius: radius.control,
    backgroundColor: colors.sunken,
    borderWidth: 1,
    borderColor: colors.lineStrong,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 14,
  },
  input: {
    flex: 1,
    color: colors.foreground,
    fontSize: 15,
    padding: 0,
  },
  focused: {
    borderColor: colors.action,
    boxShadow: `0 0 0 1px ${colors.actionRing}`,
  },
  error: {
    borderColor: colors.danger,
    boxShadow: `0 0 0 1px ${colors.dangerTint}`,
  },
  readOnly: {
    borderStyle: 'dashed',
    borderColor: colors.lineFocus,
    backgroundColor: 'rgba(12, 17, 23, 0.4)',
  },
});
