import { useEffect } from 'react';
import { StyleSheet, View } from 'react-native';
import Animated, {
  Easing,
  useAnimatedStyle,
  useReducedMotion,
  useSharedValue,
  withTiming,
} from 'react-native-reanimated';

import { colors, duration, easing, radius } from '@/design/tokens';

export type ProgressTone = 'info' | 'warning' | 'success';

export type ProgressBarProps = {
  value: number;
  plan?: number;
  tone?: ProgressTone;
  label?: string;
  height?: number;
};

const fill = {
  info: colors.info,
  warning: colors.warning,
  success: colors.success,
} as const;

export function ProgressBar({
  value,
  plan,
  tone,
  label = 'Progress',
  height = 8,
}: ProgressBarProps) {
  const reduced = useReducedMotion();
  const progress = useSharedValue(reduced ? value : 0);

  const resolvedTone: ProgressTone =
    tone ?? (plan !== undefined && value < plan ? 'warning' : value >= 100 ? 'success' : 'info');

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

  const animated = useAnimatedStyle(() => ({ width: `${Math.min(100, Math.max(0, progress.value))}%` }));

  return (
    <View
      accessibilityRole="progressbar"
      accessibilityLabel={`${label}: ${value}%${plan !== undefined ? `, plan ${plan}%` : ''}`}
      accessibilityValue={{ min: 0, max: 100, now: value }}
      style={[styles.track, { height }]}
    >
      <Animated.View
        style={[
          styles.fill,
          { height, backgroundColor: fill[resolvedTone] },
          animated,
        ]}
      />
      {plan !== undefined ? (
        <View
          style={[
            styles.planMarker,
            { start: `${Math.min(100, Math.max(0, plan))}%` },
          ]}
        />
      ) : null}
    </View>
  );
}

export type SegmentedProgressProps = {
  total: number;
  current: number;
  tone?: ProgressTone;
  label?: string;
};

export function SegmentedProgress({
  total,
  current,
  tone = 'info',
  label,
}: SegmentedProgressProps) {
  const segments = Array.from({ length: total }, (_, i) => i < current);

  return (
    <View
      accessibilityRole="progressbar"
      accessibilityLabel={label ?? `Step ${current} of ${total}`}
      accessibilityValue={{ min: 0, max: total, now: current }}
      style={styles.segmentedRow}
    >
      {segments.map((filled, idx) => (
        <View
          key={idx}
          style={[
            styles.segment,
            {
              backgroundColor: filled ? fill[tone] : colors.sunken,
              borderColor: filled ? 'transparent' : colors.line,
            },
          ]}
        />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  track: {
    width: '100%',
    borderRadius: radius.chip,
    backgroundColor: colors.sunken,
    overflow: 'hidden',
    position: 'relative',
    justifyContent: 'center',
  },
  fill: {
    borderRadius: radius.chip,
  },
  planMarker: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    width: 2,
    backgroundColor: '#FFFFFF',
    borderRadius: 1,
    zIndex: 2,
  },
  segmentedRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    width: '100%',
  },
  segment: {
    flex: 1,
    height: 8,
    borderRadius: radius.chip,
    borderWidth: 1,
  },
});
