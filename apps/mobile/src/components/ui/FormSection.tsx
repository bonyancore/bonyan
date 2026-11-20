import { View, type ViewProps } from 'react-native';

import { SolidSurface } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';

export type FormSectionProps = ViewProps & {
  title: string;
  statusLabel?: string;
};

export function FormSection({
  title,
  statusLabel,
  children,
  style,
  ...props
}: FormSectionProps) {
  return (
    <SolidSurface style={[{ padding: 16 }, style]} {...props}>
      <View className="gap-3">
        <View className="flex-row items-center justify-between border-b border-line pb-2">
          <Text variant="headline">{title}</Text>
          {statusLabel ? (
            <Text variant="caption" tone="muted">
              {statusLabel}
            </Text>
          ) : null}
        </View>
        <View className="gap-3">{children}</View>
      </View>
    </SolidSurface>
  );
}
