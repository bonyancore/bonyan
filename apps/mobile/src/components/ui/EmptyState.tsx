import { View } from 'react-native';

import { Icon, type IconName } from '@/components/ui/Icon';
import { ElevatedCard } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type EmptyStateProps = {
  icon?: IconName;
  title: string;
  subtitle?: string;
};

export function EmptyState({
  icon = 'checkCircle',
  title,
  subtitle,
}: EmptyStateProps) {
  return (
    <ElevatedCard style={{ padding: 28, alignItems: 'center', justifyContent: 'center' }}>
      <View className="items-center gap-2">
        <View className="h-12 w-12 items-center justify-center rounded-full bg-sunken">
          <Icon name={icon} size={22} color={colors.foregroundMuted} />
        </View>
        <Text variant="headline" tone="default" className="text-center">
          {title}
        </Text>
        {subtitle ? (
          <Text variant="footnote" tone="muted" className="text-center">
            {subtitle}
          </Text>
        ) : null}
      </View>
    </ElevatedCard>
  );
}
