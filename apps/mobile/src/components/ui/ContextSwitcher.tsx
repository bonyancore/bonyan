import { Modal, Pressable, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { GlassSurface } from '@/components/ui/GlassSurface';
import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { isRTL } from '@/core/i18n/direction';
import { colors, radius } from '@/design/tokens';

export type ContextCompany = {
  id: string;
  name: string;
  initials: string;
  role: string;
};

export type ContextProject = {
  id: string;
  name: string;
  progress?: number;
  statusDotColor?: string;
};

export type ContextSwitcherProps = {
  visible: boolean;
  onClose: () => void;
  companies: readonly ContextCompany[];
  selectedCompanyId: string;
  onSelectCompany: (id: string) => void;
  projects: readonly ContextProject[];
  selectedProjectId: string;
  onSelectProject: (id: string) => void;
};

export function ContextSwitcher({
  visible,
  onClose,
  companies,
  selectedCompanyId,
  onSelectCompany,
  projects,
  selectedProjectId,
  onSelectProject,
}: ContextSwitcherProps) {
  const insets = useSafeAreaInsets();
  const rtl = isRTL();

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <Pressable style={styles.scrim} onPress={onClose}>
        <Pressable
          style={[styles.sheet, { paddingBottom: insets.bottom + 20 }]}
          onPress={(e) => e.stopPropagation()}
        >
          <GlassSurface material="overlay" style={StyleSheet.absoluteFill} topOnlyRadius />

          {/* Grabber */}
          <View className="items-center pt-3 pb-2">
            <View className="h-1 w-10 rounded-full bg-foreground-muted opacity-40" />
          </View>

          {/* Header */}
          <View className="flex-row items-center justify-between px-5 pt-1 pb-4">
            <Text variant="title2">
              {rtl ? 'تبديل السياق' : 'Switch context'}
            </Text>
            <PressableScale
              onPress={onClose}
              accessibilityRole="button"
              accessibilityLabel="Close"
              style={styles.closeButton}
            >
              <Icon name="close" size={16} color={colors.foregroundSoft} />
            </PressableScale>
          </View>

          <View className="gap-6 px-5">
            {/* COMPANY Section */}
            <View className="gap-2">
              <Text variant="overline" tone="muted">
                {rtl ? 'الشركة' : 'COMPANY'}
              </Text>

              <View className="gap-1.5">
                {companies.map((c) => {
                  const isSelected = c.id === selectedCompanyId;
                  return (
                    <PressableScale
                      key={c.id}
                      onPress={() => onSelectCompany(c.id)}
                      accessibilityRole="radio"
                      accessibilityState={{ selected: isSelected }}
                      style={[
                        styles.companyCard,
                        isSelected && styles.selectedRow,
                      ]}
                    >
                      <View className="flex-row items-center gap-3">
                        <View className="h-10 w-10 items-center justify-center rounded-full bg-action">
                          <Text variant="footnote" tone="onAction" style={{ fontWeight: '700' }}>
                            {c.initials}
                          </Text>
                        </View>
                        <View className="gap-0.5">
                          <Text variant="bodyStrong" tone="default">
                            {c.name}
                          </Text>
                          <Text variant="caption" tone="muted">
                            {c.role}
                          </Text>
                        </View>
                      </View>
                      {isSelected ? (
                        <Icon name="check" size={18} color={colors.action} strokeWidth={2.4} />
                      ) : null}
                    </PressableScale>
                  );
                })}
              </View>
            </View>

            {/* PROJECT Section */}
            <View className="gap-2">
              <Text variant="overline" tone="muted">
                {rtl ? 'المشروع' : 'PROJECT'}
              </Text>

              <View className="gap-1">
                {projects.map((p) => {
                  const isSelected = p.id === selectedProjectId;
                  const dotColor = p.statusDotColor || (p.id === 'all' ? colors.foregroundMuted : colors.action);

                  return (
                    <PressableScale
                      key={p.id}
                      onPress={() => onSelectProject(p.id)}
                      accessibilityRole="radio"
                      accessibilityState={{ selected: isSelected }}
                      style={[
                        styles.projectRow,
                        isSelected && styles.selectedRow,
                      ]}
                    >
                      <View className="flex-row items-center gap-3">
                        <View
                          style={[
                            styles.statusDot,
                            { backgroundColor: dotColor },
                          ]}
                        />
                        <Text variant="body" tone={isSelected ? 'default' : 'soft'}>
                          {p.name}
                        </Text>
                      </View>

                      <View className="flex-row items-center gap-3">
                        {p.progress !== undefined ? (
                          <Text variant="footnote" tone="muted">
                            {`${p.progress}%`}
                          </Text>
                        ) : null}
                        {isSelected ? (
                          <Icon name="check" size={16} color={colors.action} strokeWidth={2.4} />
                        ) : null}
                      </View>
                    </PressableScale>
                  );
                })}
              </View>
            </View>
          </View>
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
    width: '100%',
    maxHeight: '85%',
    borderTopLeftRadius: radius.sheet,
    borderTopRightRadius: radius.sheet,
    overflow: 'hidden',
  },
  closeButton: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: 'rgba(255, 255, 255, 0.08)',
    alignItems: 'center',
    justifyContent: 'center',
  },
  companyCard: {
    height: 60,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 14,
    borderRadius: radius.control,
    backgroundColor: colors.surface,
    borderWidth: 1,
    borderColor: 'rgba(255, 255, 255, 0.07)',
  },
  projectRow: {
    height: 56,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 14,
    borderRadius: radius.control,
    backgroundColor: 'transparent',
  },
  selectedRow: {
    backgroundColor: colors.actionTint,
    borderWidth: 1,
    borderColor: 'rgba(240, 160, 75, 0.30)',
  },
  statusDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
  },
});
