# 28: Publish the verified v0.1.11 countdown release

**What to build:** Publish the existing verified v0.1.11 countdown and timer companion draft on explicit user instruction. Preserve its immutable source, twelve Windows/Linux attachments, all older releases/tags and license notices.

**Blocked by:** 27 (closed)

**Status:** ready-for-agent

## Acceptance criteria

- Record the user's authorization, exact source, successful six-target native build and every existing asset size/digest.
- Reuse guarded publication with the built-in Actions token. Verify the native jobs and downloaded assets/checksums/source manifests before making only v0.1.11 public/latest. No rebuild, replacement or tag rewrite.
- Independently verify the twelve public downloads, six checksum files, four archive source manifests and latest redirect. Preserve older release metadata/assets/tags, MIT and third-party notices.
- Update current download references, release evidence and handoff. Windows remains unsigned; Mac attachments remain excluded without Developer ID/notarization/stapling. Physical desktop/network/trust and ticket08/parent acceptance remain open. Future feature versions default to drafts.

## Comments

- 2026-10-15 (Asia/Shanghai): User explicitly requested “发布release” after the verified v0.1.11 draft. Rechecked source `af95946991389b05cd930a9b9e4ec8ad629e13ca`, immutable tag object `64a26aef7a1f4ac9ce1fe60801dd9a33c3939664`, successful native build38549545926 (six targets197 tests each), all12 uploaded/downloaded asset digests and historical release/tag/notice preservation. All18 offline publication guards pass. Authorization is scoped to existing v0.1.11 Windows/Linux attachments; no other draft is authorized.
