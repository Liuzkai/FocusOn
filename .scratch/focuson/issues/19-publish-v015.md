# 19: Publish the verified v0.1.5 draft

**What to build:** Publish the existing v0.1.5 Windows/Linux draft on explicit user instruction; preserve the exact c0938e7 source, eight assets and all older releases/tags. Reuse the release workflow and its built-in token.

**Blocked by:** 18 (closed)

**Status:** closed

## Acceptance criteria

- Select the uniquely changed approval record or an explicitly approved manual version; ordinary code changes cannot publish. No fixed fallback to v0.1.1.
- Recheck native build 38342256279, immutable tag/source, eight existing download sizes/digests/checksum files/manifests. Publish without rebuilding or replacing any archive.
- Keep original feature release notes and all earlier release metadata/assets/tag objects. Mac ad-hoc outputs remain excluded. New builds continue to default to drafts.
- Confirm actual public/latest release and direct downloads, then update English README, handoff and delivery evidence. Physical acceptance and parent specification remain open.

## Comments

- 2026-10-13: User explicitly requested “发布0.1.5”. Recorded exact c0938e72afc53a063fc26e0ecc399e8d37b4e398 source, successful six-target build and eight verified digests. Source/tag/license baseline checks pass. Publication selector and all-history preservation guard maintenance under verification; no app rebuild or tag rewrite.

- 2026-10-13: Reviewed/committed publication maintenance `b36e077`; 15 offline tests and actionlint pass. https://github.com/Liuzkai/FocusOn/actions/runs/38371850633 Success publishes/public-latest https://github.com/Liuzkai/FocusOn/releases/tag/v0.1.5, keeping immutable package source `c0938e72afc53a063fc26e0ecc399e8d37b4e398` and all eight exact original assets. Public direct downloads rechecked for size/SHA256/checksum and all four manifest sources; older Release metadata/assets and cached tag objects match. New builds still default to drafts; Mac ad-hoc validation is excluded, physical acceptance remains with 08/parent specification.
