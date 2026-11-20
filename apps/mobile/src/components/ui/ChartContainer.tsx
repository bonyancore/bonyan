import { useState } from 'react';
import { Pressable, View } from 'react-native';
import Svg, { Circle, Line, Path } from 'react-native-svg';

import { PressableScale } from '@/components/ui/PressableScale';
import { ElevatedCard } from '@/components/ui/Surface';
import { Text } from '@/components/ui/Text';
import { colors } from '@/design/tokens';

export type ChartSeries = {
  name: string;
  color: string;
  data: readonly number[];
};

export type ChartContainerProps = {
  title: string;
  subtitle?: string;
  series: readonly ChartSeries[];
  categories: readonly string[];
  ranges?: readonly string[];
  activeRange?: string;
  onRangeChange?: (range: string) => void;
  sourceNote?: string;
  screenReaderSummary?: string;
};

export function ChartContainer({
  title,
  subtitle,
  series,
  categories,
  ranges = ['4W', '12W', '26W'],
  activeRange = '12W',
  onRangeChange,
  sourceNote,
  screenReaderSummary,
}: ChartContainerProps) {
  const [selectedPoint, setSelectedPoint] = useState<number | null>(null);

  const allValues = series.flatMap((s) => s.data);
  const min = Math.min(...allValues, 0);
  const max = Math.max(...allValues, 1);
  const rangeVal = max - min || 1;

  const width = 310;
  const height = 130;
  const padStart = 32;
  const padEnd = 16;
  const padTop = 16;
  const padBottom = 24;

  const chartWidth = width - padStart - padEnd;
  const chartHeight = height - padTop - padBottom;
  const step = chartWidth / (categories.length - 1 || 1);

  const getPoints = (data: readonly number[]) =>
    data.map((val, i) => ({
      x: padStart + i * step,
      y: padTop + chartHeight - ((val - min) / rangeVal) * chartHeight,
      value: val,
    }));

  const buildPath = (points: { x: number; y: number }[]) =>
    points.reduce((acc, point, index) => {
      if (index === 0) return `M ${point.x} ${point.y}`;
      const prev = points[index - 1];
      const cpX = (prev.x + point.x) / 2;
      return `${acc} C ${cpX} ${prev.y}, ${cpX} ${point.y}, ${point.x} ${point.y}`;
    }, '');

  return (
    <ElevatedCard
      accessible
      accessibilityRole="summary"
      accessibilityLabel={screenReaderSummary || `${title}. ${subtitle || ''}`}
      style={{ padding: 16 }}
    >
      <View className="gap-3">
        <View className="flex-row items-start justify-between">
          <View className="gap-0.5">
            <Text variant="headline">{title}</Text>
            {subtitle ? (
              <Text variant="caption" tone="muted">
                {subtitle}
              </Text>
            ) : null}
          </View>

          {ranges.length > 0 ? (
            <View className="flex-row rounded-chip bg-sunken p-0.5">
              {ranges.map((range) => {
                const active = range === activeRange;
                return (
                  <PressableScale
                    key={range}
                    onPress={() => onRangeChange?.(range)}
                    className={`rounded px-2.5 py-1 ${active ? 'bg-raised' : ''}`}
                  >
                    <Text variant="caption" tone={active ? 'action' : 'muted'} style={{ fontWeight: active ? '600' : '400' }}>
                      {range}
                    </Text>
                  </PressableScale>
                );
              })}
            </View>
          ) : null}
        </View>

        {selectedPoint !== null && (
          <View className="self-center rounded-control bg-raised px-3 py-1.5 border border-line">
            <Text variant="caption" tone="muted" style={{ textAlign: 'center' }}>
              {categories[selectedPoint]}
            </Text>
            <Text variant="caption" tone="default" style={{ fontWeight: '600', textAlign: 'center' }}>
              {series.map((s) => `${s.name}: ${s.data[selectedPoint]}M`).join(' · ')}
            </Text>
          </View>
        )}

        <View className="items-center justify-center">
          <View style={{ width, height }}>
            <Svg width={width} height={height} viewBox={`0 0 ${width} ${height}`}>
              <Line
                x1={padStart}
                y1={padTop + chartHeight}
                x2={padStart + chartWidth}
                y2={padTop + chartHeight}
                stroke={colors.line}
                strokeWidth={1}
              />
              <Line
                x1={padStart}
                y1={padTop + chartHeight / 2}
                x2={padStart + chartWidth}
                y2={padTop + chartHeight / 2}
                stroke={colors.line}
                strokeDasharray="4 4"
                strokeWidth={1}
              />

              {selectedPoint !== null && (
                <Line
                  x1={padStart + selectedPoint * step}
                  y1={padTop}
                  x2={padStart + selectedPoint * step}
                  y2={padTop + chartHeight}
                  stroke={colors.lineFocus}
                  strokeWidth={1.5}
                />
              )}

              {series.map((s) => {
                const pts = getPoints(s.data);
                const pathD = buildPath(pts);
                return (
                  <Path
                    key={s.name}
                    d={pathD}
                    fill="none"
                    stroke={s.color}
                    strokeWidth={2.2}
                    strokeLinecap="round"
                  />
                );
              })}

              {series.map((s) => {
                const pts = getPoints(s.data);
                return pts.map((pt, i) => (
                  <Circle
                    key={`${s.name}-${i}`}
                    cx={pt.x}
                    cy={pt.y}
                    r={selectedPoint === i ? 5 : 2.5}
                    fill={s.color}
                  />
                ));
              })}
            </Svg>

            <View style={{ position: 'absolute', top: 0, left: 0, width, height, flexDirection: 'row' }}>
              {categories.map((_, i) => (
                <Pressable
                  key={i}
                  style={{ flex: 1, height }}
                  onPress={() => setSelectedPoint(i === selectedPoint ? null : i)}
                  accessibilityLabel={`Data point ${i + 1}`}
                />
              ))}
            </View>
          </View>
        </View>

        <View className="flex-row items-center justify-between border-t border-line pt-2">
          <View className="flex-row gap-4">
            {series.map((s) => (
              <View key={s.name} className="flex-row items-center gap-1.5">
                <View className="h-0.5 w-3 rounded-full" style={{ backgroundColor: s.color }} />
                <Text variant="caption" tone="soft">
                  {s.name}
                </Text>
              </View>
            ))}
          </View>
          {sourceNote ? (
            <Text variant="caption" tone="muted" style={{ fontSize: 9 }}>
              {sourceNote}
            </Text>
          ) : null}
        </View>
      </View>
    </ElevatedCard>
  );
}
