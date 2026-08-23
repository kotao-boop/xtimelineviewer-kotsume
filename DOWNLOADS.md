# XTimelineViewer Kotsume Edition downloads

## Current release

Download the latest Windows packages from the
[GitHub Releases page](https://github.com/kotao-boop/xtimelineviewer-kotsume/releases/latest).

## Code-signing status

The current v2.2.0 release is **not code-signed**. Windows may therefore show an "Unknown publisher" or
Microsoft Defender SmartScreen warning. The release includes `SHA256SUMS.txt` and GitHub Artifact Attestations
for its final packages. These help verify that a file is the exact file produced by the repository's release
workflow; they do not replace Authenticode code signing and do not by themselves prove that software is safe.

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/).

The project is preparing an application to SignPath Foundation. Each release states its own signing status.
Do not treat v2.2.0 as an approved or signed SignPath artifact.

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
