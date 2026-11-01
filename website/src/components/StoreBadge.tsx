import type {ReactNode} from 'react';
import Link from '@docusaurus/Link';
import ThemedImage from '@theme/ThemedImage';
import useBaseUrl from '@docusaurus/useBaseUrl';
import styles from './StoreBadge.module.css';

export const STORE_URL = 'https://apps.microsoft.com/detail/9P1XRPJT3126';

// Microsoft's official badge, unmodified. Their guidance is the dark badge on light backgrounds
// and the light one on dark backgrounds.
export default function StoreBadge({className}: {className?: string}): ReactNode {
  return (
    <Link href={STORE_URL} className={`${styles.badge} ${className ?? ''}`}>
      <ThemedImage
        alt="Download from the Microsoft Store"
        sources={{
          light: useBaseUrl('/img/ms-store-badge-dark.svg'),
          dark: useBaseUrl('/img/ms-store-badge-light.svg'),
        }}
        width={161}
        height={44}
      />
    </Link>
  );
}
