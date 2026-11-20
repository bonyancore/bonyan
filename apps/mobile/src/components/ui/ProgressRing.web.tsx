import { View } from 'react-native';

import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type ProgressRingProps = {
  value: number;
  size?: number;
  strokeWidth?: number;
  color?: string;
  label?: string;
  subtitle?: string;
};

export function ProgressRing({
  value,
  size = 72,
  strokeWidth = 7,
  color = colors.info,
  label,
  subtitle,
}: ProgressRingProps) {
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;
  const offset = circumference - (circumference * Math.min(100, Math.max(0, value))) / 100;

  return (
    <View
      accessibilityRole="progressbar"
      accessibilityLabel={label || `${value}%`}
      accessibilityValue={{ min: 0, max: 100, now: value }}
      style={{ width: size, height: size, alignItems: 'center', justifyContent: 'center' }}
    >
      <svg
        width={size}
        height={size}
        viewBox={`0 0 ${size} ${size}`}
        style={{ transform: 'rotate(-90deg)', position: 'absolute', top: 0, left: 0 }}
        aria-hidden="true"
      >
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={colors.sunken}
          strokeWidth={strokeWidth}
          fill="none"
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={color}
          strokeWidth={strokeWidth}
          strokeDasharray={`${circumference} ${circumference}`}
          strokeDashoffset={offset}
          strokeLinecap="round"
          fill="none"
          style={{ transition: 'stroke-dashoffset 480ms cubic-bezier(0.4,0,0.2,1)' }}
        />
      </svg>
      <View style={{ alignItems: 'center', justifyContent: 'center' }}>
        <Text variant="label" tone="default">
          {`${Math.round(value)}%`}
        </Text>
        {subtitle ? (
          <Text variant="caption" tone="muted" style={{ fontSize: 9, lineHeight: 11 }}>
            {subtitle}
          </Text>
        ) : null}
      </View>
    </View>
  );
}
