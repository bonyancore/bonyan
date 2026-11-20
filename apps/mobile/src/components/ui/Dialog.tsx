import { Modal, Pressable, StyleSheet, View } from 'react-native';

import { Button } from '@/components/ui/Button';
import { SolidSurface } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors, layout, radius } from '@/design/tokens';

export type DialogProps = {
  visible: boolean;
  onClose: () => void;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  onConfirm?: () => void;
  onCancel?: () => void;
  loading?: boolean;
  destructive?: boolean;
};

export function Dialog({
  visible,
  onClose,
  title,
  description,
  confirmLabel = 'Approve',
  cancelLabel = 'Cancel',
  onConfirm,
  onCancel,
  loading = false,
  destructive = false,
}: DialogProps) {
  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={onClose}>
      <Pressable style={styles.scrim} onPress={onClose}>
        <Pressable style={styles.container} onPress={(e) => e.stopPropagation()}>
          <SolidSurface style={styles.card}>
            <View className="gap-3 p-5">
              <Text variant="headline">{title}</Text>
              <Text variant="body" tone="soft">
                {description}
              </Text>

              <View className="flex-row items-center gap-3 pt-3">
                <View className="flex-1">
                  <Button
                    label={cancelLabel}
                    variant="secondary"
                    onPress={() => {
                      onCancel?.();
                      onClose();
                    }}
                  />
                </View>
                <View className="flex-1">
                  <Button
                    label={confirmLabel}
                    variant={destructive ? 'destructive' : 'primary'}
                    loading={loading}
                    onPress={() => {
                      onConfirm?.();
                      onClose();
                    }}
                  />
                </View>
              </View>
            </View>
          </SolidSurface>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  scrim: {
    flex: 1,
    backgroundColor: colors.scrim,
    justifyContent: 'center',
    paddingHorizontal: layout.gutter,
  },
  container: {
    width: '100%',
    maxWidth: 380,
    alignSelf: 'center',
  },
  card: {
    borderRadius: radius.card,
  },
});
