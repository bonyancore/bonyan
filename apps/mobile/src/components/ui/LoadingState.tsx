import { useEffect } from 'react';
import { StyleSheet, View } from 'react-native';
import Animated, {
  Easing,
  useAnimatedStyle,
  useReducedMotion,
  useSharedValue,
  withRepeat,
  withTiming,
} from 'react-native-reanimated';

import { ElevatedCard } from '@/components/ui/Surface';
import { colors, duration, radius } from '@/design/tokens';

export function SkeletonBox({
  width = '100%',
  height = 16,
  borderRadius = 6,
}: {
  width?: number | `${number}%`;
  height?: number;
  borderRadius?: number;
}) {
  const reduced = useReducedMotion();
  const opacity = useSharedValue(0.4);

  useEffect(() => {
    if (!reduced) {
      opacity.set(
        withRepeat(
          withTiming(0.8, { duration: duration.skeleton / 2, easing: Easing.inOut(Easing.ease) }),
          -1,
          true,
        ),
      );
    }
  }, [reduced, opacity]);

  const animatedStyle = useAnimatedStyle(() => ({
    opacity: opacity.value,
  }));

  return (
    <Animated.View
      style={[
        {
          width: width as any,
          height,
          borderRadius,
          backgroundColor: colors.sunken,
        },
        animatedStyle,
      ]}
    />
  );
}

export function LoadingState({ count = 3 }: { count?: number }) {
  return (
    <View className="gap-3">
      {Array.from({ length: count }).map((_, index) => (
        <ElevatedCard key={index} style={styles.card}>
          <View className="gap-3">
            <View className="flex-row items-center justify-between">
              <SkeletonBox width="30%" height={14} />
              <SkeletonBox width="20%" height={18} borderRadius={radius.chip} />
            </View>
            <SkeletonBox width="75%" height={18} />
            <SkeletonBox width="50%" height={12} />
          </View>
        </ElevatedCard>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    padding: 16,
  },
});
