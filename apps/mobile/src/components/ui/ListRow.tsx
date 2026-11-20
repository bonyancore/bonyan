import type { ReactNode } from 'react';
import { View } from 'react-native';

import { Icon } from '@/components/ui/Icon';
import { PressableScale } from '@/components/ui/PressableScale';
import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type ListRowProps = {
  title: string;
  subtitle?: string;
  leading?: ReactNode;
  trailing?: ReactNode;
  destructive?: boolean;
  onPress?: () => void;
};

export function ListRow({ title, subtitle, leading, trailing, destructive = false, onPress }: ListRowProps) {
  return (
    <PressableScale
      onPress={onPress}
      disabled={!onPress}
      accessibilityRole={onPress ? 'button' : 'text'}
      accessibilityLabel={subtitle ? `${title}, ${subtitle}` : title}
      className="min-h-16 flex-row items-center gap-3 px-3.5 py-2.5"
    >
      {leading}
      <View className="flex-1">
        <Text variant="bodyStrong" tone={destructive ? 'danger' : 'default'} numberOfLines={2}>
          {title}
        </Text>
        {subtitle ? (
          <Text variant="caption" tone="soft">
            {subtitle}
          </Text>
        ) : null}
      </View>
      {trailing ?? (onPress ? (
        <View className="rtl:scale-x-[-1]">
          <Icon name="chevronRight" size={18} color={colors.foregroundMuted} />
        </View>
      ) : null)}
    </PressableScale>
  );
}
