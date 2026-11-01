# Signed release pipeline

This folder holds the WiX v7 project for the per-machine MSI installers. On each release,
`.github/workflows/release.yml` builds an MSI per architecture plus a sideload MSIX bundle and
signs them all with Azure Artifact Signing. The Store package (`build-msix.yml`) stays unsigned,
because the Store signs it after certification.

You only need the .NET SDK to build the MSI (see the Development docs page). The project accepts
the WiX v7 Open Source Maintenance Fee EULA with `<AcceptEula>wix7</AcceptEula>`; the sponsorship
terms are at <https://docs.firegiant.com/wix/osmf/>.

Signing needs the one-time Azure setup below.

## One-time Azure setup

1. Register the resource provider (once per subscription):

   ```bash
   az provider register --namespace "Microsoft.CodeSigning"
   ```

2. Create an Artifact Signing account and a **Public Trust** certificate profile using the
   **Organization** identity validation (MJCO's is already approved):

   ```bash
   az artifact-signing create -n <account-name> -l <region> -g <resource-group> --sku Basic
   az artifact-signing certificate-profile create -g <resource-group> --account-name <account-name> -n <profile-name> --profile-type PublicTrust --identity-validation-id <identity-validation-id>
   ```

   This needs a paid subscription; free, trial and sponsored ones don't work. In the Portal, leave
   **Program Type** as **None**.

3. Note the account's regional endpoint (e.g. `https://weu.codesigning.azure.net`) and the
   certificate profile's subject name (e.g. `CN=MJCO, O=MJCO, L=Cardiff, S=Cardiff, C=GB`). The
   MSIX `Publisher` has to match the subject exactly or the bundle won't install.

4. Create an app registration for CI signing, separate from the app's Graph sign-in registration:

   ```bash
   az ad app create --display-name "CI Signing"
   az ad sp create --id <app-id>
   ```

5. Add a federated credential to it, scoped to the `release` GitHub Environment:

   ```bash
   az ad app federated-credential create --id <app-id> --parameters credential.json
   ```

   ```json
   {
       "name": "GitHubCI",
       "issuer": "https://token.actions.githubusercontent.com",
       "subject": "repo:homotechsual@2288257/oncall-busybar-agent@1331290728:environment:release",
       "audiences": ["api://AzureADTokenExchange"]
   }
   ```

   GitHub sends subjects in the immutable format, with the owner and repo IDs in them. The plain
   `repo:homotechsual/oncall-busybar-agent:...` form fails with `AADSTS700213`. To check the
   current prefix:
   `gh api repos/homotechsual/oncall-busybar-agent/actions/oidc/customization/sub`.

   Then create the environment in GitHub: **Settings → Environments → New environment**, named
   `release`.

6. Give the app's service principal the **Artifact Signing Certificate Profile Signer** role on the
   certificate profile:

   ```bash
   az role assignment create --assignee <app-id> --role "Artifact Signing Certificate Profile Signer" --scope <certificate-profile-resource-id>
   ```

7. Add these Actions secrets, on the repository or the `release` environment. None of them is a
   credential (OIDC has no client secret), but secrets keep them out of the logs.

   | Secret | Value |
   | --- | --- |
   | `AZURE_CLIENT_ID` | Application (client) ID from step 4 |
   | `AZURE_TENANT_ID` | Entra tenant ID |
   | `AZURE_SUBSCRIPTION_ID` | Subscription holding the signing account |
   | `AZURE_ARTIFACT_SIGNING_ENDPOINT` | Endpoint from step 3 |
   | `AZURE_ARTIFACT_SIGNING_ACCOUNT` | Account name from step 2 |
   | `AZURE_ARTIFACT_SIGNING_PROFILE` | Certificate profile name from step 2 |
   | `AZURE_ARTIFACT_SIGNING_PUBLISHER` | Subject name from step 3 |

After that, pushing a `v*.*.*` tag runs the release. A manual run (workflow_dispatch) builds and
signs everything without publishing, which is handy for testing.
