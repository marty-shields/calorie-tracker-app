#!/usr/bin/env node
// No npm packages. Writes the app icon, splash and README icon SVGs from one flame outline.
// Colours come from src/CalorieTracker.App/Resources/AppIcon/palette.json, which AppIconColourTests also reads.
// The MAUI build rasterises these SVGs, so they use only plain paths, solid fills, linearGradient, fill-opacity and clipPath.
//
// Usage: app-icon.mjs
import { readFileSync, writeFileSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const resources = 'src/CalorieTracker.App/Resources';
const palette = JSON.parse(readFileSync(join(repo, resources, 'AppIcon/palette.json'), 'utf8'));

function colour(name) {
  const value = palette[name];
  if (!value) throw new Error(`palette.json has no "${name}"`);
  return value;
}

// Android's adaptive-icon canvas. Launchers show the central 72 units; the safe circle is 66 across.
const CANVAS = 108;
const CORNERS = 24;
const SHADOW_DY = 2.2;
const SHADOW_OPACITY = { 'shadow-dark': 0.5, 'shadow-light': 0.7 };

// The flame outline: cubic Béziers only, absolute coordinates, so a transform is applied to the numbers.
const FLAME = [
  ['M', [56, 21]],
  ['C', [58, 33], [75, 41], [76, 59]],
  ['C', [77, 73], [67, 85], [54, 85]],
  ['C', [41, 85], [31, 75], [32, 61]],
  ['C', [32, 51], [38, 45], [42, 37]],
  ['C', [44, 44], [47, 47], [50, 47]],
  ['C', [48, 37], [50, 28], [56, 21]],
  ['Z'],
];
// The inner flame: the outline at half size about its base, moved up.
const CUT_SCALE = 0.5;
const CUT_ORIGIN = [54, 85];
const CUT_DY = -7;

const num = (v) => String(Math.round(v * 100) / 100);

function path(segments, map) {
  return segments
    .map(([cmd, ...points]) => cmd + points.map((p) => map(p).map(num).join(',')).join(' '))
    .join(' ');
}

const identity = (p) => p;
const shift = ([x, y]) => [x, y + SHADOW_DY];
const inner = ([x, y]) => [
  CUT_ORIGIN[0] + (x - CUT_ORIGIN[0]) * CUT_SCALE,
  CUT_ORIGIN[1] + (y - CUT_ORIGIN[1]) * CUT_SCALE + CUT_DY,
];

// One evenodd path: the flame with the inner flame as a hole.
const cutout = (map = identity) => `${path(FLAME, map)} ${path(FLAME, (p) => map(inner(p)))}`;

const gradient = () => [
  '  <linearGradient id="flame" x1="0" y1="0" x2="0" y2="1">',
  `    <stop offset="0" stop-color="${colour('flame-top')}" />`,
  `    <stop offset="0.55" stop-color="${colour('flame-mid')}" />`,
  `    <stop offset="1" stop-color="${colour('flame-bottom')}" />`,
  '  </linearGradient>',
];

const shadowPath = (shadow, indent) =>
  `${indent}<path d="${cutout(shift)}" fill="${colour(shadow)}" fill-opacity="${SHADOW_OPACITY[shadow]}" fill-rule="evenodd" />`;
const flamePath = (indent) => `${indent}<path d="${cutout()}" fill="url(#flame)" fill-rule="evenodd" />`;
const background = (indent) => `${indent}<rect width="${CANVAS}" height="${CANVAS}" fill="${colour('icon-background')}" />`;

const OUTPUTS = [
  { file: `${resources}/AppIcon/appicon.svg`, lines: [background('  ')] },
  { file: `${resources}/AppIcon/appiconfg.svg`, lines: [...gradient(), shadowPath('shadow-dark', '  '), flamePath('  ')] },
  {
    file: `${resources}/AppIcon/appicon_monochrome.svg`,
    lines: [`  <path d="${cutout()}" fill="${colour('monochrome')}" fill-rule="evenodd" />`],
  },
  { file: `${resources}/Splash/splash.svg`, lines: [...gradient(), shadowPath('shadow-light', '  '), flamePath('  ')] },
  {
    file: 'docs/images/readme/icon.svg',
    lines: [
      ...gradient(),
      '  <clipPath id="corners">',
      `    <rect width="${CANVAS}" height="${CANVAS}" rx="${CORNERS}" />`,
      '  </clipPath>',
      '  <g clip-path="url(#corners)">',
      background('    '),
      shadowPath('shadow-dark', '    '),
      flamePath('    '),
      '  </g>',
    ],
  },
];

const open = `<svg xmlns="http://www.w3.org/2000/svg" width="${CANVAS}" height="${CANVAS}" viewBox="0 0 ${CANVAS} ${CANVAS}">`;

for (const { file, lines } of OUTPUTS) {
  writeFileSync(join(repo, file), [open, ...lines, '</svg>', ''].join('\n'));
  console.log(`wrote ${file}`);
}
