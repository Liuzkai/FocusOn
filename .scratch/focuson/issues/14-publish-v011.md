# 14: Publish the existing verified v0.1.1 draft

**What to build:** Publish the existing Windows/Linux v0.1.1 draft on the user's explicit request, preserving exact assets/source, v0.1.0 and Mac trust limitations.

**Blocked by:** None (13 engineering complete)

**Status:** ready-for-agent

## Acceptance criteria

- Require the recorded source, immutable tag, successful six-target native run and eight exact verified archive/checksum attachments.
- Download/check existing attachments before publication; publish existing assets without rebuilding or overwriting.
- Preserve v0.1.0 tag/assets and exclude Mac ad-hoc validation packages. Ordinary pushes and new version builds still default to drafts.
- Read back publication and verify public downloads; update handoff and report real links.

## Comments

- 2026-10-12: User explicitly requested “0.1.1 直接发布吧”. Direct GitHub API access from the cloud is blocked by its network proxy; use the existing repository Actions token and guarded publication record, without additional credentials.
- 2026-10-12: Added guarded existing-draft publication to the existing release workflow, retaining version-tag/manual build defaults and ordinary-push behavior. Offline publication guards pass nine scenarios: approved draft and already-public retry; changed tag, failed build, missing native job, altered asset, incorrect manifest source, corrupt download and unexpected Mac attachment all prevent publication. actionlint/Python compilation/diff checks pass; starting actual Actions publication.
