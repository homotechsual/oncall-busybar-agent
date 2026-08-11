# Administrative template for oncall-busybar-agent

`OncallBusyBarAgent.admx` (+ its language resource, `en-US/OncallBusyBarAgent.adml`) gives IT a
point-and-click UI — in Intune's Settings Catalog or the local/domain Group Policy Editor — for the
same registry policy the app already reads from `HKLM\SOFTWARE\Policies\OncallBusyBarAgent`. It
doesn't add any new capability the app doesn't already have; see the main
[README](../README.md#managing-settings-via-intune-or-group-policy) for what each setting does and
the registry layout it maps to.

## Import into Intune

1. **Devices** → **Manage devices** → **Configuration** → **Import ADMX** tab → **Import**.
2. Upload `OncallBusyBarAgent.admx` as the ADMX file, and `en-US/OncallBusyBarAgent.adml` as the
   ADML file for the `en-US` language.
3. Once imported, create a **Settings Catalog** configuration profile and search for
   "BUSY Bar on call agent" to find the six settings, organized under **Microsoft Graph sign-in**,
   **Microphone fallback**, and **BUSY Bar device** categories.
4. Set only the settings you want to lock down and assign the profile to your target devices —
   anything left "Not configured" falls through to the user's own `appsettings.json`, exactly as
   documented in the main README.

**Updating this file later:** re-uploading a changed ADMX with the same settings fails with a
namespace error unless you first delete any configuration profiles using it and delete the old
ADMX, or give the new version's `policyNamespaces` a different namespace (e.g. append a version
number). Plan for this before making a breaking change to the policy definitions.

## Use with Group Policy instead

Copy `OncallBusyBarAgent.admx` to `%SystemRoot%\PolicyDefinitions` and
`en-US\OncallBusyBarAgent.adml` to `%SystemRoot%\PolicyDefinitions\en-US` (or your domain's central
policy store, if you have one) on a machine with the Group Policy Management Console. The settings
then appear under **Computer Configuration → Administrative Templates → BUSY Bar on call agent**.

## What this does not cover

This template only exposes the six settings already documented in the main README's registry
table (`Graph:ClientId`, `Graph:TenantId`, `Graph:PollIntervalSeconds`, `Mic:Enabled`,
`Mic:PollIntervalSeconds`, `BusyBar:Address`). If a future version of the app adds new
config keys, this template needs a matching update to expose them the same way — the app itself
will still read a manually-set registry value under the same `HKLM\SOFTWARE\Policies\
OncallBusyBarAgent` key even if this template hasn't been updated to add a friendly control for it
yet.
