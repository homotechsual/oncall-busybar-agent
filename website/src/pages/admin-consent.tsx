import {useEffect, useState, type ReactNode} from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import AdminConsentLink from '@site/src/components/AdminConsentLink';
import styles from './admin-consent.module.css';

// Microsoft's admin consent endpoint sends the admin back here with either
// ?admin_consent=True&tenant=... or ?error=...&error_description=...
type Result =
  | {kind: 'pending'}
  | {kind: 'none'}
  | {kind: 'granted'; tenant: string | null}
  | {kind: 'failed'; error: string; description: string | null};

function readResult(): Result {
  const params = new URLSearchParams(window.location.search);
  const error = params.get('error');
  if (error) {
    return {kind: 'failed', error, description: params.get('error_description')};
  }
  if (params.get('admin_consent')?.toLowerCase() === 'true') {
    return {kind: 'granted', tenant: params.get('tenant')};
  }
  return {kind: 'none'};
}

export default function AdminConsent(): ReactNode {
  const [result, setResult] = useState<Result>({kind: 'pending'});

  useEffect(() => setResult(readResult()), []);

  return (
    <Layout title="Admin consent" description="Grant BUSY Bar on call agent's permissions for your whole organization.">
      <main className={styles.page}>
        {result.kind === 'granted' && (
          <>
            <h1>Consent granted</h1>
            <p>
              Everyone in your organization can now sign in to BUSY Bar on call agent without being asked to
              approve its permissions.
            </p>
            {result.tenant && (
              <p className={styles.detail}>
                Tenant: <code>{result.tenant}</code>
              </p>
            )}
            <p>
              <Link to="/getting-started">Back to Getting started</Link>
            </p>
          </>
        )}

        {result.kind === 'failed' && (
          <>
            <h1>Consent wasn’t granted</h1>
            <p>
              {result.error === 'access_denied'
                ? 'The request was declined, or the account you used can’t grant consent for your organization.'
                : 'Microsoft returned an error, so nothing was changed.'}{' '}
              Granting consent needs a Global Administrator, Privileged Role Administrator, Cloud Application
              Administrator or Application Administrator.
            </p>
            {result.description && (
              <p className={styles.detail}>
                <code>{result.error}</code>: {result.description}
              </p>
            )}
            <AdminConsentLink />
          </>
        )}

        {result.kind === 'none' && (
          <>
            <h1>Admin consent</h1>
            <p>
              An admin can approve BUSY Bar on call agent’s permissions for the whole organization in one step, so
              nobody gets asked when they sign in. It asks for Presence.Read and Calendars.ReadBasic, both
              delegated and read-only, plus the standard sign-in permissions.
            </p>
            <AdminConsentLink />
            <p className={styles.detail}>
              Microsoft brings you back to this page afterwards. See <Link to="/privacy">Privacy</Link> for what the
              app does with these permissions.
            </p>
          </>
        )}
      </main>
    </Layout>
  );
}
