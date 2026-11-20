import { Platform } from 'react-native';
import Svg, { Path } from 'react-native-svg';

import { colors } from '@/design/tokens';

export type MetricTrendProps = {
  data: readonly number[];
  width?: number;
  height?: number;
  color?: string;
  strokeWidth?: number;
};

export function MetricTrend({
  data,
  width = 80,
  height = 28,
  color = colors.chartMint,
  strokeWidth = 2,
}: MetricTrendProps) {
  if (data.length < 2) return null;

  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;
  const padding = 2;
  const usableHeight = height - padding * 2;
  const step = (width - padding * 2) / (data.length - 1);

  const points = data.map((val, i) => {
    const x = padding + i * step;
    const y = padding + usableHeight - ((val - min) / range) * usableHeight;
    return { x, y };
  });

  const path = points.reduce((acc, point, index) => {
    if (index === 0) return `M ${point.x} ${point.y}`;
    const prev = points[index - 1];
    const cpX = (prev.x + point.x) / 2;
    return `${acc} C ${cpX} ${prev.y}, ${cpX} ${point.y}, ${point.x} ${point.y}`;
  }, '');

  return (
    <Svg
      width={width}
      height={height}
      viewBox={`0 0 ${width} ${height}`}
      {...(Platform.OS === 'web' ? { 'aria-hidden': true } : { accessibilityElementsHidden: true })}
    >
      <Path
        d={path}
        fill="none"
        stroke={color}
        strokeWidth={strokeWidth}
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </Svg>
  );
}
