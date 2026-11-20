export const duration = {
  fast: 120,
  base: 220,
  slow: 360,
  chart: 480,
  skeleton: 1200,
} as const;

export const stagger = 40;

export const easing = {
  standard: [0.2, 0, 0, 1],
} as const;

export const spring = {
  press: { damping: 22, stiffness: 320, mass: 0.8 },
  nav: { damping: 20, stiffness: 240, mass: 1 },
  sheet: { damping: 28, stiffness: 260, mass: 1 },
} as const;

export const pressScale = 0.97;
export const pullToRefreshThreshold = 72;
