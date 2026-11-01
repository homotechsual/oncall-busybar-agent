import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import BusyBarDisplay from '@site/src/components/BusyBarDisplay';
import styles from './index.module.css';

const STORE_URL = 'https://apps.microsoft.com/detail/9P1XRPJT3126';
const RELEASES_URL = 'https://github.com/homotechsual/oncall-busybar-agent/releases/latest';

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
              <Link className={styles.primary} href={STORE_URL}>
                Get it from Microsoft Store
              </Link>
              <Link className={styles.secondary} to="/getting-started">
                Read the docs
              </Link>
            </div>
            <p className={styles.requirements}>
              Free and open source. Needs Windows 10 or later, a BUSY Bar, and a Microsoft 365 work or school
              account.
            </p>
          </div>
          <div className={styles.heroDevice}>
            <BusyBarDisplay />
            <p className={styles.caption}>
              Captured from a real BUSY Bar during a scheduled meeting, with the time left sliding in every few
              seconds.
            </p>
          </div>
        </header>

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
                <Link href={item.href}>{item.action}</Link>
              </div>
            ))}
          </div>
        </section>
      </main>
    </Layout>
  );
}
