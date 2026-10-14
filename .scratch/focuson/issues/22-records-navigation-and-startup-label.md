# 22: Records navigation and visible startup option

**What to build:** Rename the Review/回看 navigation to Records/记录 and explicitly expose 开机启动 / Start at login in Settings, retaining its working saved per-user registration behavior.

**Blocked by:** 21 (closed)

**Status:** ready-for-agent

## Acceptance criteria

- Records/记录 navigation, date accessibility labels and list-refresh feedback consistently use the new page name. Preserve recorder/history/timer behavior and recorded user text.
- Clearly labeled startup option and sign-in/tray description in Settings. Existing preference defaults, save rollback and registration enable/disable remain intact; isolated QA mode does not affect production startup.
- Build/existing regression/native bilingual UI checks, current Settings screenshots/English README, fresh immutable v0.1.8 draft. Preserve all earlier releases/tags/data/notices. Physical login/device/Mac trust acceptance remains08.

## Comments

- 2026-10-14: User requests Records/记录 and startup option. Existing v0.1.7 already has a saved login-to-tray checkbox wired to platform registration; clarify its label and actual sign-in behavior rather than creating a machine-wide service or a duplicate preference. No backend/data/registration schema change is needed.

- 2026-10-14: Navigation and date/accessibility/list-refresh feedback now Records/记录; Settings checkbox reads Start at login/开机启动 with sign-in/background/window-fallback and immediate-saving guidance. Existing registration enable/disable/save rollback/isolation behavior retained. Local124 core/platform +36 UI regressions and18 publication guards pass. Real native synthetic Linux UI verifies both names in both languages, visibly enabled normal-mode startup control and off/on preference persistence with restoration; its injected callbacks never alter system startup. Updated native screenshots/English README/usage. No new tests mirroring the wording change. Six-native-target v0.1.8 draft pending; actual sign-in/device acceptance remains08.
