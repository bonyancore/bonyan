import { StyleSheet, View } from 'react-native';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors, radius } from '@/design/tokens';

export type ToastVariant = 'success' | 'error' | 'notice';

export type ToastProps = {
  variant?: ToastVariant;
  message: string;
  actionLabel?: string;
  onAction?: () => void;
};

export function Toast({
  variant = 'success',
  message,
  actionLabel,
  onAction,
}: ToastProps) {
  const isSuccess = variant === 'success';
  const isError = variant === 'error';

  return (
    <GlassSurface
      material="navigation"
      radius={radius.full}
      noHighlight
      style={styles.toast}
    >
      <View style={styles.toastInner}>
        <View className="flex-row items-center gap-2.5">
          {isSuccess ? (
            <Icon name="check" size={16} color={colors.success} strokeWidth={2.2} />
          ) : isError ? (
            <Icon name="warning" size={16} color={colors.danger} strokeWidth={2.2} />
          ) : null}
          <Text variant="footnote" tone="default">
            {message}
          </Text>
        </View>

        {actionLabel && onAction ? (
          <PressableScale onPress={onAction} className="ps-3">
            <Text variant="label" tone="action">
              {actionLabel}
            </Text>
          </PressableScale>
        ) : null}
      </View>
    </GlassSurface>
  );
}

const styles = StyleSheet.create({
  toast: {
    borderRadius: radius.full,
    alignSelf: 'center',
    maxWidth: '92%',
    overflow: 'hidden',
  },
  toastInner: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderRadius: radius.full,
    overflow: 'hidden',
  },
});
