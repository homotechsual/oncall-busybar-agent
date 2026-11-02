import {useEffect, useState} from 'react';

const GITHUB_LATEST = 'https://api.github.com/repos/homotechsual/oncall-busybar-agent/releases/latest';
// The Store's catalog API allows cross-origin requests and lists each package's full name, which
// carries the version: MJCOLtd.OnCallBusyBarAgent_1.0.1.0_neutral_~_8xpef1ps6j1qr.
const STORE_CATALOG =
  'https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=9P1XRPJT3126&market=US&languages=en-us';

export type LatestVersions = {
  github?: {version: string; published: Date};
  store?: string;
};

function compareVersions(a: string, b: string): number {
  const pa = a.split('.').map(Number);
  const pb = b.split('.').map(Number);
  for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
    const diff = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

async function fetchGitHub(signal: AbortSignal): Promise<LatestVersions['github']> {
  const response = await fetch(GITHUB_LATEST, {signal, headers: {Accept: 'application/vnd.github+json'}});
  if (!response.ok) return undefined;
  const release = (await response.json()) as {tag_name?: string; published_at?: string};
  if (!release.tag_name || !release.published_at) return undefined;
  return {version: release.tag_name.replace(/^v/, ''), published: new Date(release.published_at)};
}

async function fetchStore(signal: AbortSignal): Promise<string | undefined> {
  const response = await fetch(STORE_CATALOG, {signal});
  if (!response.ok) return undefined;
  // Read the package names out of the text rather than walking the catalog's deep object tree.
  const text = await response.text();
  const versions = [...text.matchAll(/"PackageFullName"\s*:\s*"[^"_]+_(\d+\.\d+\.\d+)\.\d+_/g)].map(m => m[1]);
  return versions.sort(compareVersions).at(-1);
}

/** The latest GitHub release and the version live in the Store, fetched in the browser so the
 * site stays current without a rebuild. Either is left undefined if its request fails. */
export default function useLatestVersions(): LatestVersions {
  const [versions, setVersions] = useState<LatestVersions>({});

  useEffect(() => {
    const controller = new AbortController();
    const ignore = () => undefined;
    fetchGitHub(controller.signal)
      .then(github => github && setVersions(v => ({...v, github})))
      .catch(ignore);
    fetchStore(controller.signal)
      .then(store => store && setVersions(v => ({...v, store})))
      .catch(ignore);
    return () => controller.abort();
  }, []);

  return versions;
}
