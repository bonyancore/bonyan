import { useEffect } from 'react';
import { View } from 'react-native';
import Animated, {
  Easing,
  useAnimatedProps,
  useReducedMotion,
  useSharedValue,
  withTiming,
} from 'react-native-reanimated';
import Svg, { Circle } from 'react-native-svg';

import { Text } from '@/components/ui/Text';
import { colors, duration, easing } from '@/design/tokens';

const AnimatedCircle = Animated.createAnimatedComponent(Circle);

export type ProgressRingProps = {
  value: number; // 0 to 100
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
  const reduced = useReducedMotion();
  const radius = (size - strokeWidth) / 2;
  const circumference = 2 * Math.PI * radius;

  const progress = useSharedValue(reduced ? value : 0);

  useEffect(() => {
    progress.set(
      reduced
        ? value
        : withTiming(value, {
            duration: duration.chart,
            easing: Easing.bezier(...easing.standard),
          }),
    );
  }, [value, reduced, progress]);

  const animatedProps = useAnimatedProps(() => {
    const strokeDashoffset = circumference - (circumference * progress.value) / 100;
    return {
      strokeDashoffset,
    };
  });

  return (
    <View
      accessibilityRole="progressbar"
      accessibilityLabel={label || `${value}%`}
      accessibilityValue={{ min: 0, max: 100, now: value }}
      className="items-center justify-center"
      style={{ width: size, height: size }}
    >
      <Svg width={size} height={size} style={{ transform: [{ rotate: '-90deg' }] }}>
        <Circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={colors.sunken}
          strokeWidth={strokeWidth}
          fill="none"
        />
        <AnimatedCircle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          stroke={color}
          strokeWidth={strokeWidth}
          strokeDasharray={`${circumference} ${circumference}`}
          strokeLinecap="round"
          fill="none"
          animatedProps={animatedProps}
        />
      </Svg>
      <View className="absolute items-center justify-center">
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
