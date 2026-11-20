import { Modal, Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors, radius } from '@/design/tokens';

export type BottomSheetProps = {
  visible: boolean;
  onClose: () => void;
  title?: string;
  footerNote?: string;
  children: React.ReactNode;
};

export function BottomSheet({
  visible,
  onClose,
  title,
  footerNote,
  children,
}: BottomSheetProps) {
  const insets = useSafeAreaInsets();

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <Pressable style={styles.scrim} onPress={onClose}>
        <Pressable
          style={[styles.sheet, { paddingBottom: insets.bottom + 20 }]}
          onPress={(e) => e.stopPropagation()}
        >
          <GlassSurface material="overlay" style={StyleSheet.absoluteFill} topOnlyRadius />

          <View className="items-center pt-3 pb-2">
            <View className="h-1 w-10 rounded-full bg-foreground-muted opacity-40" />
          </View>

          {title ? (
            <View className="flex-row items-center justify-between px-5 pt-1 pb-3">
              <Text variant="title2">{title}</Text>
              <PressableScale
                onPress={onClose}
                accessibilityRole="button"
                accessibilityLabel="Close"
                style={styles.closeBtn}
              >
                <Icon name="close" size={16} color={colors.foregroundSoft} />
              </PressableScale>
            </View>
          ) : null}

          <View className="px-5">{children}</View>

          {footerNote ? (
            <View className="px-5 pt-3">
              <Text variant="caption" tone="muted">
                {footerNote}
              </Text>
            </View>
          ) : null}
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  scrim: {
    flex: 1,
    backgroundColor: colors.scrim,
    justifyContent: 'flex-end',
  },
  sheet: {
    borderTopLeftRadius: radius.sheet,
    borderTopRightRadius: radius.sheet,
    overflow: 'hidden',
    width: '100%',
    maxWidth: 500,
    alignSelf: 'center',
  },
  closeBtn: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    alignItems: 'center',
    justifyContent: 'center',
  },
});
