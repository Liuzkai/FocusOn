# macOS installation, permissions and release trust

FocusOn uses native macOS foreground and session observers, software rendering in a native Cocoa window, with the shared Avalonia interface and local SQLite storage. macOS 15+ is the minimum supported .NET 10 platform. Intel (`osx-x64`) and Apple Silicon (`osx-arm64`) are separate self-contained app bundles; no Rosetta dependency is intended.

1. Check the build/release's recorded trust status and the ZIP SHA256 before downloading. A cross-compiled archive or ad-hoc signature does not establish Mac compatibility or a trusted distribution.
2. Extract the complete ZIP. Move `FocusOn.app` to `/Applications` or another stable folder before running; keep the included MIT/third-party notices for redistribution.
3. Open the app. For foreground app/window/title recording, enable **FocusOn** under **System Settings → Privacy & Security → Accessibility**, then quit and reopen. Stable app identity/signing helps keep permission attached to the same app. A different development signature or path can require granting access again.
4. Screen Recording, camera, microphone, Full Disk Access and Input Monitoring are not requested. FocusOn reads active app/window metadata; it does not capture screen pixels or keystrokes. Without Accessibility, foreground recording reports unavailable; title timers and history still work. Restricted apps can omit titles.
5. Close hides the window when the menu bar icon is available, keeping the same process and timer alive. Use **Open timeline** in the menu bar, click the Dock icon, or launch again to restore. Use **Exit FocusOn**, the window's **Exit**, or native **Quit** to save the final boundary and release the writer lock/observers/icon.
6. Normal first launch writes the current user's LaunchAgent pointing to the fixed executable with `--startup`. An isolated `--data-dir <directory>` skips this registration. Data remains under `~/Library/Application Support/FocusOn`; quit before upgrading/backing up. No background-service or administrator installation is required.

## Native backend and verification

NSWorkspace identifies the active app; Accessibility reads its focused window and title. Samples arrive about every 200ms, so shorter switches may be missed. IOKit receives sleep/wake events; CoreGraphics queries console/lock/display state. A sampling gap is preserved as unrecorded rather than attributing sleep to an old window. Titled timers pause conservatively after an interrupted sample stream. Real sleep, lock, fast user switching and multi-monitor behavior require physical acceptance.

Native standard GitHub runners build and test each architecture separately. The desktop smoke check opens a real Avalonia window, exercises close/restore, timer persistence, language selection, single writer and resource release; Mac checks call the real continuous clock/session APIs and record whether Accessibility is granted. A separate check starts the actual self-contained `.app`, confirms SQLite initialization and sends native Quit. When Accessibility is granted, the check requires a positive real foreground usage segment; a denial **does not verify authorized foreground capture**. Evidence/run links are recorded in the delivery record.

## Developer ID and notarization

No membership is purchased or assumed. A maintainer must already have an active Apple Developer Program account with an unexpired **Developer ID Application** certificate/private key and App Store Connect API access permitted for notarization. Configure these in **repository Settings → Secrets and variables → Actions → Repository secrets**; do not commit them or paste them in chat:

| Secret | Required material |
| --- | --- |
| `MACOS_CERTIFICATE_P12_BASE64` | Base64 of the exported Developer ID Application certificate **with its private key**, in password-protected P12 format. |
| `MACOS_CERTIFICATE_PASSWORD` | Password protecting that P12. |
| `MACOS_SIGNING_IDENTITY` | Exact Developer ID Application identity name shown by Keychain Access. |
| `APPLE_TEAM_ID` | Team ID matching the certificate. |
| `APPLE_API_KEY_ID` | Notarization API key ID. |
| `APPLE_API_ISSUER_ID` | API issuer ID. |
| `APPLE_API_PRIVATE_KEY` | Complete P8 API private key; preserve its actual newlines. |

The release workflow opts into signing. Manual desktop validation can opt in with `sign_mac`; ordinary pushes build validation bundles. The pipeline verifies all secret bindings exist, imports the P12 into a temporary keychain, checks a valid matching identity/team, signs nested Mach-O libraries and then the app, and cleans up the keychain/P12/P8 in `finally`. Sensitive tool output is withheld on failures.

The app bundle keeps its single executable in `Contents/MacOS` and managed/runtime resources in `Contents/Resources/runtime`; the pinned SDK creates an apphost bound to that relative assembly path. This avoids treating JSON and managed resources as unsigned nested executable code. Native libraries are signed explicitly, and the app is signed last.

The app uses hardened runtime. Entitlements are limited to `com.apple.security.cs.allow-jit` and `com.apple.security.cs.allow-unsigned-executable-memory` for .NET's JIT/executable code paths, including Intel. All bundled native code is signed with the same identity, so library-validation disabling is not requested. There is no App Sandbox, automation, device or file-access entitlement; Accessibility still requires the user's separate TCC consent.

Apple must return **Accepted**. The pipeline then staples the **app** (ZIP itself cannot be stapled), validates the ticket, verifies deep/strict signatures and runs Gatekeeper assessment. Only after those succeed are the final stapled bundle's file manifest, distributable ZIP and SHA256 created. The native verifier repeats trust checks, and the Release upload excludes unsigned/unnotarized Mac packages. A failed signing/notary check fails the build and cannot produce a release attachment.

`mac-signing.json` records ad-hoc or Developer ID/notarized status, without credentials. `formalReady` remains false until physical permission/power acceptance is completed. Neither missing credentials nor successful cross-compilation is reported as signing/notarization success. Drafts remain pending verification; do not disable Gatekeeper or remove quarantine to turn an unverified build into a trusted release.
