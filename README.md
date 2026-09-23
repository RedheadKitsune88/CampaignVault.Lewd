# CampaignVault.Lewd

Opt-in **Lewd Handbook** plugin for CampaignVault: adult `lewd_encounter` interaction mode for `ActiveSystem=dnd5e`, plus YAML data overlays and LLM-client skill sidecars. What is this **Lewd Handbook**? It is a homebrew extension for fifth edition D&D system; you can [read more](https://www.patreon.com/Miss_Mycelia/posts/sex-dungeons-5e-79225409) at it's creator's patreon.

> **Adult content.** See [NOTICE](NOTICE). Operator and table consent required. Full-trust DLL (same trust model as any CampaignVault code plugin).

This repository is intentionally **separate** from the main CampaignVault tree so adult mechanics and prose never land in core.

## Requirements

- .NET SDK that targets `net10.0`
- A CampaignVault host with engine version ≥ `0.2.0` (`minEngineVersion` in `plugin.json`)
- `CampaignVault.PluginSdk` `0.1.2`+ (plugin assembly + host `campaign_update.systemOptions` / Item.DefinitionName) (nuget.org when published; local feed until then)

## Build

```bash
# Until Sdk is on nuget.org: pack from a sibling CampaignVault checkout
./scripts/pack-sdk-local.sh

dotnet restore
dotnet build
dotnet test
```

`nuget.config` lists **nuget.org first**, then `./local-packages`. After you publish the Sdk to nuget.org, delete the `local-packages` source (and this local pack step).

## Install into a host

```bash
./scripts/pack-plugin.sh
# Copies into artifacts/LewdHandbook/ — drop that folder under the host Plugins/ directory:
#
# Plugins/
#   LewdHandbook/
#     plugin.json
#     LewdHandbook.dll
#     RulesetData/   (optional)
#     skills/        (optional; host logs path only — point Claude/OpenCode at these files)
```

**Never** place `CampaignVault.PluginSdk.dll` in the plugin folder. The host already provides it.

### Enable the mode

On a `dnd5e` campaign, include `lewd_encounter` in `EnabledModeIds`, then enter/exit with core `mode_transition`. See **Switching intimacy / consent style** below for `intimacyTone`.

### LLM skills (operator install)

Host does **not** inject skills. Point your LLM client at the plugin skill pack:

`Plugins/LewdHandbook/skills/` (or repo `src/LewdHandbook/skills/` while developing)

| File | Role |
|------|------|
| `lewd-encounter.md` | Master mode — verbs, pools, consent |
| `lewd-intimacy-tone.md` | `intimacyTone` behavior |
| `lewd-implements-anatomy.md` | Items vs `anatomy.*` Traits |
| `lewd-bindings.md` | Structured `bindings[]` / bind verbs |
| `lewd-sexual-histories.md` | Eight histories, verbal caps |
| `lewd-catalog.md` | RulesetData spells/conditions |
| `lewd-brands.md` | Lustbrands: `lewd_apply_brand`, per-brand effects |
| `lewd-imprints-conditioning.md` | Imprint tracks + decondition |
| `lewd-vices.md` | Vices/addictions: `lewd_vice` |
| `lewd-pregnancy.md` | Pregnancy / fertility |
| `lewd-bad-ending.md` | Bad-end record + consequences |

## Plugin identity

| Field | Value |
|-------|-------|
| Manifest id | `com.campaignvault.lewd-handbook` |
| Mode id | `lewd_encounter` |
| Compatible systems | `dnd5e` |
| Custom `$type`s | `lewd_advance`, `lewd_climax_check`, `lewd_bind`, `lewd_unbind`, `lewd_pregnancy`, `lewd_bad_end`, `lewd_apply_brand`, `lewd_imprint`, `lewd_decondition`, `lewd_vice` |

## Layout

```
src/LewdHandbook/          plugin assembly + plugin.json + RulesetData + skills
tests/LewdHandbook.Tests/  unit tests against Sdk types
pack/                      install notes
scripts/                   local Sdk pack + plugin zip helpers
local-packages/            gitignored nupkgs for pre-nuget.org Sdk
```

## Consent model (engine is source of truth)

Two layers:

| Layer | Where | Purpose |
|-------|--------|---------|
| **Table tone** | Campaign `SystemOptions["intimacyTone"]` | How *unwilling* advances resolve for the whole campaign |
| **Scene consent** | Participant State `consent` / `hard_limits` / … | Per-character willingness this encounter |

Plugin install and enabling `lewd_encounter` are **operator** opt-in. They do **not** make any character willing.

Per-participant scratch on mode enter includes `consent` (`willing` | `selective` | `unwilling` | `revoked`). Hard-limit and revoked advances **always fail** the commit in every tone. Skill sidecars must not invent consent that contradicts engine State.

## Switching intimacy / consent style (`intimacyTone`)

Declared by this plugin in `plugin.json` → `campaignOptions` (schema ownership). Runtime value lives in campaign **SystemOptions**.

| Value | Unwilling / unwanted `lewd_advance` |
|-------|-------------------------------------|
| `consensual` (**default** if the key is missing) | Commit **fails** |
| `fade` | Commit succeeds; **no stimulation**; physical-state nudge |
| `grimdark` | Allowed; use Inhibition on AC / unwanted saves |

`hard_limits` and `revoked` stay fail-closed in all three tones.

### 1. Check whether the key exists

Call host MCP `get_config` for the campaign and look at `systemOptions`:

```json
{
  "systemOptions": {
    "intimacyTone": "consensual"
  }
}
```

If `intimacyTone` is absent, the plugin still behaves as **`consensual`**.

### 2. Add or change the key (merge — does not wipe other options)

Use a `take_turn` / commit batch with `campaign_update`. `systemOptions` **merges** listed keys only; other SystemOptions entries are left alone.

**Enable the mode and set grimdark in one update:**

```json
{
  "$type": "campaign_update",
  "enabledModeIds": ["lewd_encounter"],
  "systemOptions": {
    "intimacyTone": "grimdark"
  }
}
```

**Switch tone later without touching modes:**

```json
{
  "$type": "campaign_update",
  "systemOptions": {
    "intimacyTone": "fade"
  }
}
```

**Back to safe table default:**

```json
{
  "$type": "campaign_update",
  "systemOptions": {
    "intimacyTone": "consensual"
  }
}
```

Requires a CampaignVault host built with PluginSdk **0.1.2+** (`campaign_update.systemOptions` merge). After changing the key, confirm with `get_config` again.

### 3. Per-scene consent keys (participant State)

These are **not** SystemOptions. Set them on each `ModeParticipantState` when entering the encounter (or via whatever mode/state tooling you use before the first advance):

| Key | Values / shape |
|-----|----------------|
| `consent` | `willing` \| `selective` \| `unwilling` \| `revoked` |
| `allowed_partners` | string[] character ids (when `selective`) |
| `hard_limits` | string[] tags — always block matching stim |
| `soft_limits` | string[] tags — stim ×0.5 |
| `kinks` | string[] tags — stim ×1.5 |
| `inhibition` | int (locked Int/Wis/Cha mod) |

Example: PC is willing with one partner only:

```json
{
  "consent": "selective",
  "allowed_partners": ["chars/alice"],
  "hard_limits": ["noncon", "scat"],
  "inhibition": 2
}
```

Full tone behavior for the LLM: `skills/lewd-intimacy-tone.md`.

## License

MIT — see [LICENSE](LICENSE). Adult-content and redistribution caveats — see [NOTICE](NOTICE).
