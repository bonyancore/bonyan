export const shadows = {
  elevated: '0 8px 24px rgba(0, 0, 0, 0.30)',
  glassNav: '0 16px 40px rgba(0, 0, 0, 0.45)',
  glassOverlay: '0 -16px 48px rgba(0, 0, 0, 0.50)',
  glassFloating: '0 16px 48px rgba(0, 0, 0, 0.50)',
  ember: '0 12px 28px rgba(240, 160, 75, 0.30)',
} as const;

export const materials = {
  glassNavigation: { blur: 28, saturation: 160, radius: 32 },
  glassOverlay: { blur: 40, saturation: 150, radius: 32 },
  elevated: { radius: 20 },
  solid: { radius: 16 },
} as const;

export type MaterialName = keyof typeof materials;
