# XTimelineViewer Kotsume Edition downloads

## Current release

Download the latest Windows packages from the
[GitHub Releases page](https://github.com/kotao-boop/xtimelineviewer-kotsume/releases/latest).

## Microsoft Store status

The initial x64 and ARM64 Microsoft Store submission was certified and became publicly available on
August 27, 2026. Store packages are signed and updated through Microsoft Store.

The Store MSIX and GitHub EXE/ZIP packages are separate distribution channels. Store updates are delivered
through Microsoft Store. GitHub packages do not become signed merely because the Store version is signed.

## Code-signing status

The GitHub v2.3.1 release is **not code-signed**. Windows may therefore show an "Unknown publisher" or
Microsoft Defender SmartScreen warning. The release includes `SHA256SUMS.txt` and GitHub Artifact Attestations
for its final packages. These help verify that a file is the exact file produced by the repository's release
workflow; they do not replace Authenticode code signing and do not by themselves prove that software is safe.

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

The SignPath application is currently deferred while Microsoft Store is the primary signed distribution route.
Each release states its own signing status. Do not treat GitHub packages as approved or signed SignPath artifacts.

## Package formats

| Package | Purpose |
|---|---|
| `XTimelineViewer-Kotsume-vX.Y.Z-Setup.exe` | Windows installer |
| `XTimelineViewer-Kotsume-vX.Y.Z-win-x64-Portable.zip` | Portable package for x64 Windows |
| `XTimelineViewer-Kotsume-vX.Y.Z-win-arm64-Portable.zip` | Portable package for arm64 Windows |

Every package must include `LICENSE`, `THIRD-PARTY-NOTICES.md` and the generated `licenses` directory.
SHA-256 checksums and GitHub Artifact Attestations are generated from the final files after all applicable gates
have passed. For an unsigned release, the signing gate is explicitly marked as not applicable instead of being
presented as successful.

See the [privacy policy](PRIVACY.md) before using the optional translation feature.
