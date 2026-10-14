# 25: Add responsive statistics hover motion

**What to build:** Add tasteful pointer-hover animation to the existing statistics page, retaining its scope, exact usage values, themes, bilingual details and keyboard access. Deliver a fresh v0.1.10 draft after publishing the immutable v0.1.9 repair.

**Blocked by:** 20 (closed), 23 (closed)

**Status:** closed

## Acceptance criteria

- Donut slices gently lift/highlight on hover, usage bars and trend markers gain smooth emphasis, and cards gain subtle depth. Short finite transitions do not animate recorded heights/durations or continuously pulse.
- Immediate pointer-following hints, keyboard details/focus, empty/error/filtered scopes and persisted app colors stay usable in both themes/languages.
- A saved statistics-animation switch under Settings → Appearance allows motion to be disabled. High contrast uses immediate static emphasis. Hidden pages/windows, lost ownership and shutdown clear hints and stop motion; no perpetual background animation.
- Verify meaningful preference/interaction/lifecycle regressions and native pointer visuals, capture actual synthetic-data preview evidence, then run standard native targets and create a fresh immutable version draft.
- Preserve public v0.1.9 source/tag/assets, all older releases/notices and user data. Mac release/trust and physical acceptance remain pending with08; future publication requires explicit version approval.

## Comments

- 2026-10-14: User requests “统计页面制作一些鼠标浮空交互动画，让画面感更加灵动一些” during v0.1.9 publication. Keep that already validated/public version immutable and use next v0.1.10 draft for this feature. Implement bounded hover motion and a relevant opt-out, without introducing a chart dependency or altering statistics allocations.

- 2026-10-14 local verification: implemented170ms finite easing, lifted donut slices, unchanged duration heights, column/marker emphasis and card tint/border/shadow; saved statistics-animation opt-out defaults on for old settings. High contrast uses static emphasis, inactive page/window/detach/shutdown clears motion. Real native checks found and fixed non-hittable empty plot space, manually hosted Fluent tooltip opacity and stale selected-language captions; a regression protects period inputs/applied scope/validation on language change.144 core/platform +40 UI (184 total) pass; Release build zero warnings/errors. Native Linux X11 synthetic preview asserts actual intermediate/settled slice frames, four-pixel hint following, bar/trend details, card enter/leave, persisted opt-out, both themes/languages and unchanged137040-second scope. Actual GIF and two hover screenshots captured; no production recorder/startup/data used. Six-target v0.1.10 draft still pending.

- 2026-10-14 engineering complete: reviewed/committed source `614749315efc25b0da7e6bebeb62a358a4bcb846`, immutable v0.1.10 object `53bd8d6d6264fb1e0bce6a42cd935494d42139d3`; main build 38525817037 and [tagged build](https://github.com/Liuzkai/FocusOn/actions/runs/38526833375) Success. Six targets 144 + 40 = 184 regressions/no failures/skips, native/package checks and both Windows installer lifecycle checks PASS. [Draft](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.10) has 12 uploaded/downloaded SHA256-verified assets. Native 129-frame hover GIF, two-theme/two-language screenshots and visible tooltip positioning/frame stability checked; saved opt-out, high-contrast keyboard/window cleanup and scope-preserving translated caption regressions pass. All historical release metadata/asset digests/tag objects/MIT/notices preserved; public/latest remains v0.1.9, new draft not published. Mac unsigned/not notarized/stapled and excluded. Physical device/privacy/trust/upgrade/network acceptance remains with ticket 08/parent specification. Documentation is later than source tag.
