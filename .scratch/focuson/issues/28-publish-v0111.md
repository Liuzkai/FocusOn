# 28: Publish the verified v0.1.11 countdown release

**What to build:** Publish the existing verified v0.1.11 countdown and timer companion draft on explicit user instruction. Preserve its immutable source, twelve Windows/Linux attachments, all older releases/tags and license notices.

**Blocked by:** 27 (closed)

**Status:** closed

## Acceptance criteria

- Record the user's authorization, exact source, successful six-target native build and every existing asset size/digest.
- Reuse guarded publication with the built-in Actions token. Verify the native jobs and downloaded assets/checksums/source manifests before making only v0.1.11 public/latest. No rebuild, replacement or tag rewrite.
- Independently verify the twelve public downloads, six checksum files, four archive source manifests and latest redirect. Preserve older release metadata/assets/tags, MIT and third-party notices.
- Update current download references, release evidence and handoff. Windows remains unsigned; Mac attachments remain excluded without Developer ID/notarization/stapling. Physical desktop/network/trust and ticket08/parent acceptance remain open. Future feature versions default to drafts.

## Comments

- 2026-10-15 (Asia/Shanghai): User explicitly requested “发布release” after the verified v0.1.11 draft. Rechecked source `af95946991389b05cd930a9b9e4ec8ad629e13ca`, immutable tag object `64a26aef7a1f4ac9ce1fe60801dd9a33c3939664`, successful native build38549545926 (six targets197 tests each), all12 uploaded/downloaded asset digests and historical release/tag/notice preservation. All18 offline publication guards pass. Authorization is scoped to existing v0.1.11 Windows/Linux attachments; no other draft is authorized.

- Initial publication run38668363060 safely failed before mutation: GitHub reports the same approved draft (release ID412592911, sourceaf95946) under temporary tag_name `untagged-6cc6551e254de0992d3b`, without a temporary Git ref. Read-only diagnosis confirmed immutable v0.1.11 tag/native build and all earlier refs remain present. Publication now binds this explicit draft ID and verifies all approved source/asset checks before attaching the already existing tag during publication. Historical drafts without refs preserve absence; public missing refs, concurrent ref appearance/disappearance and non-404 failures remain blocked. Added meaningful guard regressions; no application source or package bytes change.

- 2026-10-15 engineering complete: reviewed/committed authorization `dae8ccccafde20884458daf7cc441489230a9fd3` and guarded alias repair `2ff98eed955565e55eb56c0025ace148515c8207`. All27 offline publication regressions PASS. [Publication38668806947](https://github.com/Liuzkai/FocusOn/actions/runs/38668806947) Success; [v0.1.11](https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.11) public/latest, exact original sourceaf95946/tag64a26ae/release ID412592911/twelve asset bytes preserved. Native build38549545926 six targets197 tests each and desktop/package/installer gates checked before publication. Independent public download/size/SHA, six checksum files, four archive source manifests, latest redirect, all historical release/tag/asset snapshots and original MIT/third-party notices PASS. README/current Windows/usage references and release/delivery/verification/handoff updated; no application changes or rebuild. Temporary diagnosis branch deleted. Windows unsigned; Mac no Developer ID/notarization/stapling and excluded. Physical permissions/power/DPI/menu/network/older-version upgrade/Wayland placement and08/parent remain open; future drafts unchanged. Completion documentation is committed after the immutable package source.

- Post-publication maintenance CI38668363049/38668806884 exposed two existing countdown UI assertions after the real date advanced. The fixture always returned2026-10-14 even when MainWindow requested2026-10-15, so ReviewProjection correctly rejected that day's data and timer snapshot. Original tagged main/release builds really did pass197 tests per target on14October; binaries remain unchanged. Reopened this maintenance ticket to return the actual requested date and valid local-day bounds in the synthetic reader, retaining every original countdown/completion/pointer assertion. No application fix or replacement Release is needed. Local reproduction and corrected six-target CI follow.

- Local2026-10-15 reproduction of the unchanged test class:2 failed /3 passed, matching the native maintenance CI failures. Review confirms only the synthetic day-reader contract was wrong, not runtime date or countdown persistence. The correction returns the requested date/local bounds and keeps all assertions intact; original public artifacts remain af95946.

- Maintenance CI completion: test-only fix `5be29761ef8408adfc58d5c95bb1a637b9da8457` reviewed/committed, local197 PASS and [native38669311948](https://github.com/Liuzkai/FocusOn/actions/runs/38669311948) Success. All six targets each197 tests plus native desktop/package checks and both Windows installer lifecycles PASS. No assertion removed; all publicaf95946 assets/tag and older releases remain unchanged. Subsequent validation artifacts are seven-day main outputs, never Release replacements. Ticket closed again after this follow-up verification;08/parent and Mac trust remain open.
