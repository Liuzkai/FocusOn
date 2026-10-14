# 26: Publish the verified v0.1.10 hover release

**What to build:** Publish the existing verified v0.1.10 Windows/Linux draft on explicit user instruction. Preserve its immutable source, twelve attachments and every earlier release/tag/notice. Also simplify the English README into a clear project landing page, linking detailed evidence and history instead of repeating it.

**Blocked by:** 25 (closed)

**Status:** ready-for-agent

## Acceptance criteria

- Record exact source, successful six-target native build and all original asset sizes/digests against the user's explicit publication authorization.
- Reuse the guarded publication workflow and built-in token, verify all downloads/checksums/source manifests and successful native jobs before making the existing draft public/latest. No package rebuild or tag/asset replacement.
- Recheck public/latest and all twelve public downloads. Preserve every historical release/tag/asset and original MIT/third-party notices; unnotarized Mac artifacts remain excluded.
- Simplify the English README to a concise project overview, feature list, real screenshots, clear installation/platform support, documentation, development and license links. Keep commercial MIT and separate dependency-license statements, explain current Mac trust/download limits without implying all binaries are ready.
- Update delivery/handoff and keep physical/network/trust/parent acceptance open. Future feature versions still default to drafts.

## Comments

- 2026-10-14 (Asia/Shanghai): User replied “正式发布” to v0.1.10, then requested “简化更新Readme，风格更偏向热门github库的。” Publication is authorized for unchanged source614749315efc25b0da7e6bebeb62a358a4bcb846, tag object53bd8d6d6264fb1e0bce6a42cd935494d42139d3 and native build38526833375 (six targets184 tests each). Existing draft/source/tag/notices rechecked;18 publication guards pass. README revision follows publication independently and does not change release package bytes.
