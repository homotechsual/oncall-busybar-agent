import {useState, type ReactNode} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import BusyBarDisplay from '@site/src/components/BusyBarDisplay';
import StoreBadge, {STORE_URL} from '@site/src/components/StoreBadge';
import styles from './index.module.css';

const RELEASES_URL = 'https://github.com/homotechsual/oncall-busybar-agent/releases/latest';

const previews = [
  {
    id: 'call',
    name: 'On a call',
    frames: '/img/busybar-on-call.png',
    stillFrame: 17,
    label: 'A BUSY Bar showing ON CALL, with the time left sliding in every few seconds',
    caption:
      'Captured from a real BUSY Bar during a scheduled meeting, with the time left sliding in every few seconds.',
  },
  {
    id: 'no-meeting',
    name: 'No meeting',
    frames: '/img/busybar-no-meeting.png',
    stillFrame: 0,
    label: 'A BUSY Bar showing ON CALL with no countdown',
    caption:
      'For a call with nothing on your calendar, or when you haven’t signed in and the agent goes by your microphone, the bar shows ON CALL without a countdown.',
  },
  {
    id: 'meeting-end',
    name: 'Meeting ends',
    frames: '/img/busybar-meeting-end.png',
    stillFrame: 59,
    label: 'A BUSY Bar counting down to zero, then showing Well done!',
    caption:
      'When a meeting’s time runs out, the bar says Well done! It goes back to its usual screen once the agent sees you’ve left the call. Shortened from a real capture.',
  },
  {
    id: 'focus',
    name: 'Focus time',
    frames: '/img/busybar-flow.png',
    stillFrame: 28,
    label: 'A BUSY Bar showing FLOW, with the time left sliding in every few seconds',
    caption:
      'When Teams shows you as Focusing, the bar switches to FLOW and counts down to the end of your focus time. Also captured from a real BUSY Bar.',
  },
];

function HeroDevice(): ReactNode {
  const [selected, setSelected] = useState(previews[0].id);
  const preview = previews.find(p => p.id === selected) ?? previews[0];

  return (
    <div className={styles.heroDevice}>
      <div className={styles.previewSwitch} role="group" aria-label="Show the bar">
        {previews.map(p => (
          <button
            key={p.id}
            type="button"
            className={styles.previewButton}
            aria-pressed={p.id === selected}
            onClick={() => setSelected(p.id)}>
            {p.name}
          </button>
        ))}
      </div>
      <BusyBarDisplay frames={preview.frames} stillFrame={preview.stillFrame} label={preview.label} />
      <p className={styles.caption} aria-live="polite">
        {preview.caption}
      </p>
    </div>
  );
}

const situations = [
  {
    when: 'You join a Teams call',
    then: 'The bar shows you’re on call. For a scheduled meeting, the timer counts down to its end.',
  },
  {
    when: 'You’re on a call in another app',
    then: 'Teams doesn’t report these, so the agent watches for your microphone being in use instead.',
  },
  {
    when: 'You leave a meeting early',
    then: 'The timer stops, even though the meeting is still on your calendar.',
  },
  {
    when: 'Teams shows you as Focusing',
    then: 'The bar switches to its FLOW status, with the same countdown for scheduled focus time.',
  },
];

const managed = [
  {
    text: 'Install from the Microsoft Store, or with a signed MSI or MSIX bundle.',
    link: {to: '/running', label: 'Installing'},
  },
  {
    text: 'Manage settings centrally with Intune or Group Policy, using the included ADMX template.',
    link: {to: '/intune-group-policy', label: 'Intune and Group Policy'},
  },
  {
    text: 'Read-only Microsoft 365 permissions. From your calendar, the agent only reads when meetings end.',
    link: {to: '/privacy', label: 'Privacy policy'},
  },
  {
    text: 'No telemetry, and the microphone check never listens to or records audio.',
    link: {to: '/configuration', label: 'Settings'},
  },
];

const installers = [
  {
    name: 'Microsoft Store',
    detail: 'Updates itself. Also available with winget, and as a Store app in Intune.',
    href: STORE_URL,
    action: 'Open the Store listing',
    badge: true,
  },
  {
    name: 'MSI',
    detail: 'One per architecture. Installs for everyone on the machine, so it suits Intune Win32 apps.',
    href: RELEASES_URL,
    action: 'Download from GitHub',
  },
  {
    name: 'MSIX bundle',
    detail: 'One file for x86, x64 and ARM64. Double-click to install, or deploy as a line-of-business app.',
    href: RELEASES_URL,
    action: 'Download from GitHub',
  },
];

export default function Home(): ReactNode {
  return (
    <Layout
      title="Your BUSY Bar, in sync with Teams"
      description="A Windows tray app that runs your BUSY Bar's on-call timer while you're on a Microsoft Teams call.">
      <main className={styles.page}>
        <header className={styles.hero}>
          <div className={styles.heroText}>
            <h1 className={styles.title}>Your BUSY Bar knows when you’re on a call.</h1>
            <p className={styles.lede}>
              A small Windows tray app that starts your BUSY Bar’s on-call timer when a Teams call starts,
              counts down to the end of scheduled meetings, and stops when you hang up.
            </p>
            <div className={styles.actions}>
              <StoreBadge />
              <Link className={styles.secondary} to="/getting-started">
                Read the docs
              </Link>
            </div>
            <p className={styles.requirements}>
              Free and open source, for Windows 10 or later. It works with just your microphone; sign in with a
              Microsoft 365 work or school account to add Teams presence, meeting countdowns and FLOW status.
            </p>
          </div>
          <HeroDevice />
        </header>

        <div className={styles.dividerWrap} aria-hidden="true">
          <div className={styles.divider} />
        </div>

        <section className={styles.situations} aria-labelledby="situations-heading">
          <h2 id="situations-heading" className={styles.sectionTitle}>
            What it does
          </h2>
          <dl className={styles.situationList}>
            {situations.map(item => (
              <div key={item.when} className={styles.situation}>
                <dt>{item.when}</dt>
                <dd>{item.then}</dd>
              </div>
            ))}
          </dl>
        </section>

        <section className={styles.managed} aria-labelledby="managed-heading">
          <div className={styles.managedInner}>
            <h2 id="managed-heading" className={styles.sectionTitle}>
              Ready for managed rollouts
            </h2>
            <ul className={styles.managedList}>
              {managed.map(item => (
                <li key={item.link.to}>
                  <p>{item.text}</p>
                  <Link to={item.link.to}>{item.link.label}</Link>
                </li>
              ))}
            </ul>
          </div>
        </section>

        <section className={styles.install} aria-labelledby="install-heading">
          <h2 id="install-heading" className={styles.sectionTitle}>
            Pick how to install it
          </h2>
          <p className={styles.installNote}>
            Use one of these. They’re separate installs, so having more than one gives you more than one copy.
          </p>
          <div className={styles.installGrid}>
            {installers.map(item => (
              <div key={item.name} className={styles.installer}>
                <h3>{item.name}</h3>
                <p>{item.detail}</p>
                {item.badge ? <StoreBadge /> : <Link href={item.href}>{item.action}</Link>}
              </div>
            ))}
          </div>
          <p className={styles.sponsor}>
            It’s free. If it saves you a few interruptions, you can{' '}
            <Link href="https://github.com/sponsors/homotechsual">sponsor its development on GitHub</Link>.
          </p>
        </section>
      </main>
    </Layout>
  );
}
