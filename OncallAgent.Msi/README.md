# Signed release pipeline

This folder contains the WiX v7 project that builds the per-machine MSI installers attached to
each GitHub Release. `.github/workflows/release.yml` builds those MSIs (one per architecture) plus
a sideload MSIX bundle from `OncallAgent.Package`, and signs all of them via Azure Artifact
Signing. The Store MSIX (`.github/workflows/build-msix.yml`) is separate and unsigned, since the
Store re-signs it after certification.

Building the MSI itself needs only the .NET SDK (see the Development docs page). The project
accepts the WiX v7 Open Source Maintenance Fee EULA via `<AcceptEula>wix7</AcceptEula>`; see
https://docs.firegiant.com/wix/osmf/ for the sponsorship terms that come with it.

Signing needs the one-time Azure setup below.

## One-time Azure setup

1. Register the resource provider (once per Azure subscription):

   ```bash
   az provider register --namespace "Microsoft.CodeSigning"
   ```

2. Create an Artifact Signing account and a **Public Trust** certificate profile, under the
   **Organization** identity validation path. MJCO's Organization identity validation is already
   approved, so this only needs the account and certificate profile themselves:

   ```bash
   az artifact-signing create -n <account-name> -l <region> -g <resource-group> --sku Basic
   az artifact-signing certificate-profile create -g <resource-group> --account-name <account-name> -n <profile-name> --profile-type PublicTrust --identity-validation-id <the approved MJCO identity validation's ID>
   ```

   Artifact Signing requires a **paid** Azure subscription; it does not work on free, trial or
   sponsored subscriptions. If creating the certificate profile in the Azure Portal instead, leave
   **Program Type** at its default of **None** (it only matters for ELAM/antimalware enrollment).

3. Note the account's regional endpoint URL (e.g. `https://weu.codesigning.azure.net` for West
   Europe), and the certificate profile's **subject name** as shown on the profile in the Portal
   (e.g. `CN=MJCO Ltd, O=MJCO Ltd, L=London, C=GB`). The MSIX manifest's `Publisher` must match
   that subject exactly, character for character, or the signed bundle won't install.

4. Create an Entra app registration for CI signing, separate from the app's own Graph sign-in
   registration:

   ```bash
   az ad app create --display-name "oncall-busybar-agent-ci-signing"
   az ad sp create --id <app-id>
   ```

5. Add a federated credential on that app registration, scoped to a GitHub Environment
   (federated credential subjects don't support wildcards, so a tag-based subject would need
   updating for every release):

   ```bash
   az ad app federated-credential create --id <app-id> --parameters credential.json
   ```

   where `credential.json` is:

   ```json
   {
       "name": "oncall-busybar-agent-release",
       "issuer": "https://token.actions.githubusercontent.com",
       "subject": "repo:homotechsual/oncall-busybar-agent:environment:release",
       "description": "GitHub Actions release workflow, signing jobs",
       "audiences": ["api://AzureADTokenExchange"]
   }
   ```

   Then create the matching GitHub Environment: **Settings → Environments → New environment**,
   named `release`. The workflow's signing jobs declare `environment: release`.

6. Grant the app registration's service principal the **Artifact Signing Certificate Profile
   Signer** role, scoped to the certificate profile:

   ```bash
   az role assignment create --assignee <app-id> --role "Artifact Signing Certificate Profile Signer" --scope <certificate-profile-resource-id>
   ```

7. Set these repository **variables** (Settings → Secrets and variables → Actions → Variables;
   not Secrets, since OIDC means there's no secret value to store):

   | Variable | Value |
   | --- | --- |
   | `AZURE_CLIENT_ID` | The app registration's Application (client) ID from step 4. |
   | `AZURE_TENANT_ID` | Your Entra tenant ID. |
   | `AZURE_SUBSCRIPTION_ID` | The subscription the Artifact Signing account lives in. |
   | `AZURE_ARTIFACT_SIGNING_ENDPOINT` | The regional endpoint URL from step 3. |
   | `AZURE_ARTIFACT_SIGNING_ACCOUNT` | The Artifact Signing account name from step 2. |
   | `AZURE_ARTIFACT_SIGNING_PROFILE` | The certificate profile name from step 2. |
   | `AZURE_ARTIFACT_SIGNING_PUBLISHER` | The certificate subject name from step 3, used as the sideload MSIX's `Publisher`. |

Once that's done, pushing a tag matching `v*.*.*` (for example `v1.0.0`) runs the release
workflow. Run it with **workflow_dispatch** first to check signing works without publishing a
release.
