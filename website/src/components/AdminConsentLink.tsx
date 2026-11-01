import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import styles from './AdminConsentLink.module.css';

const CLIENT_ID = 'b2006916-b557-4280-ae66-773f28e16941';
// Must stay registered as a web redirect URI on the shared app registration.
const REDIRECT_URI = 'https://oncall.busy.homotechsual.dev/admin-consent/';

// /.default asks for everything the app registration lists: Presence.Read and Calendars.ReadBasic,
// plus the standard sign-in permissions (User.Read, openid, profile, offline_access).
export const ADMIN_CONSENT_URL =
  'https://login.microsoftonline.com/organizations/v2.0/adminconsent' +
  `?client_id=${CLIENT_ID}` +
  `&scope=${encodeURIComponent('https://graph.microsoft.com/.default')}` +
  `&redirect_uri=${encodeURIComponent(REDIRECT_URI)}`;

export default function AdminConsentLink(): ReactNode {
  return (
    <div className={styles.wrap}>
      <Link href={ADMIN_CONSENT_URL} className={styles.button}>
        Grant consent for your organization
      </Link>
    </div>
  );
}
