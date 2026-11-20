import { View } from 'react-native';

import { Button } from '@/components/ui/Button';
import { Icon } from '@/components/ui/Icon';
import { ElevatedCard } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type ErrorStateProps = {
  title: string;
  subtitle?: string;
  retryLabel?: string;
  onRetry?: () => void;
};

export function ErrorState({
  title,
  subtitle,
  retryLabel = 'Try again',
  onRetry,
}: ErrorStateProps) {
  return (
    <ElevatedCard style={{ padding: 24, alignItems: 'center', justifyContent: 'center' }}>
      <View className="items-center gap-3">
        <View className="h-12 w-12 items-center justify-center rounded-full bg-danger-tint">
          <Icon name="warning" size={22} color={colors.danger} />
        </View>
        <View className="gap-1 items-center">
          <Text variant="headline" tone="default" className="text-center">
            {title}
          </Text>
          {subtitle ? (
            <Text variant="footnote" tone="muted" className="text-center">
              {subtitle}
            </Text>
          ) : null}
        </View>
        {onRetry ? (
          <View className="pt-2">
            <Button label={retryLabel} variant="secondary" onPress={onRetry} />
          </View>
        ) : null}
      </View>
    </ElevatedCard>
  );
}
