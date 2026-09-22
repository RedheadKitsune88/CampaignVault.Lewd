# CampaignVault.Lewd

Opt-in **Lewd Handbook** plugin for CampaignVault: adult `lewd_encounter` interaction mode for `ActiveSystem=dnd5e`, plus YAML data overlays and LLM-client skill sidecars.

> **Adult content.** See [NOTICE](NOTICE). Operator and table consent required. Full-trust DLL (same trust model as any CampaignVault code plugin).

This repository is intentionally **separate** from the main CampaignVault tree so adult mechanics and prose never land in core.

## Status

**Phases 0–4 in progress.** Mode `lewd_encounter` with consent gates, stimulation/numbing/arousal math, `lewd_advance` + `lewd_climax_check`, and a curated RulesetData pack (pools/conditions/feats/spells). See [RULES_NOTES.md](RULES_NOTES.md) and [lewd-handbook-plugin-plan.md](lewd-handbook-plugin-plan.md).

## Requirements

- .NET SDK that targets `net10.0`
- A CampaignVault host with engine version ≥ `0.2.0` (`minEngineVersion` in `plugin.json`)
- `CampaignVault.PluginSdk` `0.1.1` (nuget.org when published; local feed until then)

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

Enable on a dnd5e campaign via `campaign_update` → `EnabledModeIds` including `lewd_encounter`, set campaign `SystemOptions.intimacyTone` to `consensual` | `fade` | `grimdark`, then enter/exit with core `mode_transition`.

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

## Plugin identity

| Field | Value |
|-------|-------|
| Manifest id | `com.campaignvault.lewd-handbook` |
| Mode id | `lewd_encounter` |
| Compatible systems | `dnd5e` |
| Custom `$type` (stub) | `lewd_advance` |

## Layout

```
src/LewdHandbook/          plugin assembly + plugin.json + RulesetData + skills
tests/LewdHandbook.Tests/  unit tests against Sdk types
pack/                      install notes
scripts/                   local Sdk pack + plugin zip helpers
local-packages/            gitignored nupkgs for pre-nuget.org Sdk
```

## Consent model (engine is source of truth)

Per-participant scratch state on mode enter includes `consent` (`willing` | `selective` | `unwilling` | `revoked`). Hard-limit / revoked advances fail the commit. LLM skill sidecars must not invent consent that contradicts engine state.

## License

MIT — see [LICENSE](LICENSE). Adult-content and redistribution caveats — see [NOTICE](NOTICE).

## Git / account separation

This folder is initialized as a git repo **without** commits and **without** local `user.name` / `user.email`, so your normal GitHub identity is not baked in. Before the first commit on the lewd account:

```bash
cd /path/to/CampaignVault.Lewd
git config user.name "your-lewd-account-name"
git config user.email "your-lewd-account@example.com"
# create empty repo under the lewd GitHub account, then:
git remote add origin git@github.com:<lewd-account>/CampaignVault.Lewd.git
git add .
git commit -m "Initial Phase 0 scaffold"
git push -u origin main
```

Do not use your primary account credentials or `gh` session for this remote.
