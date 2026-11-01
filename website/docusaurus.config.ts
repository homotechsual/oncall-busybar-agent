import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';
import {DOCUSAURUS_VERSION} from '@docusaurus/utils';
import plausiblePlugin from '@homotechsual/docusaurus-plugin-plausible';
import type {PluginOptions as PlausiblePluginOptions} from '@homotechsual/docusaurus-plugin-plausible';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

const config: Config = {
  title: 'BUSY Bar on call agent',
  tagline: "Runs your BUSY Bar's on-call timer while you're on a Microsoft Teams call",
  favicon: 'img/favicon.svg',

  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  url: 'https://oncall.busy.homotechsual.dev',
  baseUrl: '/',

  organizationName: 'homotechsual',
  projectName: 'oncall-busybar-agent',

  onBrokenLinks: 'throw',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: '/',
          editUrl: 'https://github.com/homotechsual/oncall-busybar-agent/tree/main/website/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [
    [
      plausiblePlugin,
      {
        domain: 'oncall.busy.homotechsual.dev',
      } satisfies PlausiblePluginOptions,
    ],
  ],

  themeConfig: {
    colorMode: {
      defaultMode: 'dark',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    navbar: {
      title: 'BUSY Bar on call agent',
      logo: {
        alt: 'BUSY Bar on call agent Logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {
          href: 'https://github.com/homotechsual/oncall-busybar-agent',
          label: 'GitHub',
          position: 'right',
          target: '_blank',
          className: 'github-link',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Docs',
          items: [
            {label: 'Getting started', to: '/getting-started'},
            {label: 'Configuration', to: '/configuration'},
            {label: 'Intune / Group Policy', to: '/intune-group-policy'},
            {label: 'Running in production', to: '/running'},
            {label: 'Privacy', to: '/privacy'},
            {label: 'Terms of service', to: '/terms'},
          ],
        },
        {
          title: 'More',
          items: [
            {label: 'Microsoft Store', href: 'https://apps.microsoft.com/detail/9P1XRPJT3126'},
            {label: 'GitHub', href: 'https://github.com/homotechsual/oncall-busybar-agent'},
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} MJCO.<br />Built with <a href="https://docusaurus.io">Docusaurus v${DOCUSAURUS_VERSION}</a>.<br /><span class="designedBy">Designed with <svg xmlns="http://www.w3.org/2000/svg" class="heart" width="24" height="24" viewBox="0 0 24 24"><path d="M14 20.408c-.492.308-.903.546-1.192.709-.153.086-.308.17-.463.252h-.002a.75.75 0 01-.686 0 16.709 16.709 0 01-.465-.252 31.147 31.147 0 01-4.803-3.34C3.8 15.572 1 12.331 1 8.513 1 5.052 3.829 2.5 6.736 2.5 9.03 2.5 10.881 3.726 12 5.605 13.12 3.726 14.97 2.5 17.264 2.5 20.17 2.5 23 5.052 23 8.514c0 3.818-2.801 7.06-5.389 9.262A31.146 31.146 0 0114 20.408z"/></svg> by <a href="https://homotechsual.dev">homotechsual</a></span>`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
