export const colors = {
  background: '#090C10',
  surface: '#10161D',
  elevated: '#161D26',
  raised: '#1F2833',
  sunken: '#0C1117',
  solidGlass: '#121820',

  line: 'rgba(255, 255, 255, 0.07)',
  lineStrong: 'rgba(255, 255, 255, 0.10)',
  lineFocus: 'rgba(255, 255, 255, 0.16)',
  highlight: 'rgba(255, 255, 255, 0.18)',
  glass: 'rgba(26, 34, 44, 0.58)',
  glassOverlay: 'rgba(18, 24, 32, 0.78)',
  scrim: 'rgba(0, 0, 0, 0.55)',

  foreground: '#EDF0F4',
  foregroundSoft: '#AEB6C1',
  foregroundMuted: '#8A95A3',

  action: '#F0A04B',
  actionStrong: '#EC9A3F',
  actionHighlight: '#F6B05C',
  onAction: '#1B1206',
  actionTint: 'rgba(240, 160, 75, 0.14)',
  actionRing: 'rgba(240, 160, 75, 0.55)',

  success: '#5ED1A1',
  successTint: 'rgba(94, 209, 161, 0.14)',
  info: '#84B6EB',
  infoTint: 'rgba(132, 182, 235, 0.14)',
  warning: '#F4D167',
  warningTint: 'rgba(244, 209, 103, 0.14)',
  danger: '#F27A6D',
  dangerTint: 'rgba(242, 122, 109, 0.14)',

  chartGlacier: '#84B6EB',
  chartMint: '#5ED1A1',
  chartEmber: '#F0A04B',
  chartIris: '#A9A4F4',
  chartRose: '#E595B8',
} as const;

export type ColorToken = keyof typeof colors;

export const ambient = {
  glacier: 'rgba(132, 182, 235, 0.22)',
  ember: 'rgba(240, 160, 75, 0.14)',
  iris: 'rgba(169, 164, 244, 0.18)',
} as const;
