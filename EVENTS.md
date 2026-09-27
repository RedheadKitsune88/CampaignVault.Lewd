# LewdHandbook domain events

Other plugins integrate via **string topics** — do not PackageReference `LewdHandbook`.
Subscribe with `IDomainEventHandler`, read fields with `e.TryGet<T>(...)`, return follow-up `WorldChange`s.

Manifest id / topic prefix: `com.campaignvault.lewd-handbook.`

| Topic | When | Fields |
|-------|------|--------|
| `…climax.v1` | Every resolved climax save, instant/auto climax from an advance, or forced climax | `characterId`, `outcome` (`climax` / `denied` / `ruined` / `held` = edge held, edging cleared / `edging` = still pending), `forced`, `inEncounter` |
| `…bad_end.v1` | A character is first marked Bad-Ended — by `lewd_bad_end` or by the engine (overstimulation 6, empty recovery dice, arousal max ≤ 0, capture impregnation) | `characterId`, `reason`, `consequence?`, `viceId?`, `imprintTrack?`, `imprintJump?` |
| `…brand_changed.v1` | Brand apply/remove | `characterId`, `brandId`, `action`, `tier` |
| `…vice_state.v1` | Addicted (save failed, Brand of Addiction, bad-end vice) / clean (verb or long-rest observer) | `characterId`, `viceId`, `state`, `dc?` |
| `…imprint_changed.v1` | Tick / accept / set / decondition | `characterId`, `category`, `level`, `action` |
| `…pregnancy.v1` | Conceived / birth / terminated (including a failed termination save) | `characterId`, `state`, `sourceId?`, `offspring`, `progress` |
| `…binding_changed.v1` | A binding put on (`bound`), taken off (`unbound`) or escaped (`escaped`) — in or out of a scene | `characterId`, `action`, `bindingId`, `kind`, `anchorId?`, `actorId?`, `method?` |

Mode enter/turn/exit use host `core.mode_entered.v1` / `core.mode_turn_started.v1` / `core.mode_exited.v1` with `modeId=lewd_encounter` — Lewd does not duplicate those; it reacts to the last two with `lewd_turn_start` / `lewd_scene_end`, to `core.rested.v1` with `lewd_rest`, and to its own `climax.v1` with `lewd_echo_check` (Brand of Echoes).

Topics are listed under `plugin.json` `"publishes"`.
