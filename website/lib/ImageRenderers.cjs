// OpenGraph images for @homotechsual/docusaurus-og, drawn with Satori at build time. Same approach
// as the dotMARC site: a navy gradient, the page's own title and description, and for the landing
// and other standalone pages, a real captured frame of the bar.
const React = require('react');
const {readFileSync} = require('fs');
const {join} = require('path');

const WIDTH = 1200;
const HEIGHT = 630;
const SITE_NAME = 'BUSY Bar on call agent';
const DOMAIN = 'oncall.busy.homotechsual.dev';

// Satori reads WOFF, not the WOFF2 the site itself loads, so these come from @fontsource/inter.
const fontFile = weight =>
  readFileSync(join(process.cwd(), 'node_modules', '@fontsource', 'inter', 'files', `inter-latin-${weight}-normal.woff`));

const options = {
  width: WIDTH,
  height: HEIGHT,
  fonts: [400, 600, 800].map(weight => ({name: 'Inter', data: fontFile(weight), weight, style: 'normal'})),
};

const dataUrl = (file, mime) =>
  `data:${mime};base64,${readFileSync(join(process.cwd(), 'static', 'img', file)).toString('base64')}`;

const background = dataUrl('og/background.svg', 'image/svg+xml');
const logo = dataUrl('logo.svg', 'image/svg+xml');
// og_bar output: the on-call capture at 1928 x 472, bezel included.
const bar = dataUrl('og/bar-on-call.png', 'image/png');
const BAR_RATIO = 1928 / 472;

const h = React.createElement;

function div(style, ...children) {
  return h('div', {style: {display: 'flex', ...style}}, ...children.flat().filter(c => c !== null && c !== undefined && c !== false));
}

function clamp(text, max) {
  const normalized = String(text ?? '').replace(/\s+/g, ' ').trim();
  return normalized.length <= max ? normalized : `${normalized.slice(0, max - 1).trimEnd()}…`;
}

function frame(...content) {
  return div(
    {width: WIDTH, height: HEIGHT, fontFamily: 'Inter', position: 'relative', overflow: 'hidden', color: '#fff'},
    h('img', {src: background, width: WIDTH, height: HEIGHT, style: {position: 'absolute', inset: 0}}),
    div(
      {flexDirection: 'column', justifyContent: 'space-between', width: '100%', height: '100%', padding: 60},
      ...content,
    ),
  );
}

function header() {
  return div(
    {alignItems: 'center', gap: 16},
    h('img', {src: logo, width: 40, height: 40}),
    div({fontSize: 26, fontWeight: 600, color: '#dbe2ea'}, SITE_NAME),
  );
}

function title(text, size) {
  return div({fontSize: size, lineHeight: 1.06, fontWeight: 800, letterSpacing: -1.2}, text);
}

const docs = data => [
  frame(
    div(
      {flexDirection: 'column', gap: 30},
      header(),
      title(clamp(data.metadata.title, 80), 68),
      data.metadata.description
        ? div({maxWidth: 1000, fontSize: 30, lineHeight: 1.3, color: '#c7d0dc'}, clamp(data.metadata.description, 170))
        : null,
    ),
    div(
      {alignItems: 'flex-end', justifyContent: 'space-between'},
      div({fontSize: 26, fontWeight: 600, color: '#f2bab6'}, DOMAIN),
      h('img', {src: bar, width: Math.round(96 * BAR_RATIO), height: 96}),
    ),
  ),
  options,
];

const pages = data => {
  // Docusaurus adds " | BUSY Bar on call agent" to page titles; the header already says that.
  const text = clamp(String(data.metadata.title || SITE_NAME).replace(/\s*\|\s*BUSY Bar on call agent\s*$/, ''), 60);
  const barHeight = 220;
  return [
    frame(
      div({flexDirection: 'column', gap: 30}, header(), title(text, 60)),
      h('img', {src: bar, width: Math.round(barHeight * BAR_RATIO), height: barHeight}),
    ),
    options,
  ];
};

module.exports = {docs, pages};
