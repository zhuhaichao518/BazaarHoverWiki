# Security Model

## Assets being protected

- The player's game account.
- Local files and credentials.
- Screenshots and on-screen information.
- The integrity of the installed game.

## Trust boundaries

### Trusted

- The local Windows installation and Windows OCR components.
- The published source corresponding to the downloaded release.

### External

- BazaarDB content loaded in WebView2.
- NuGet and GitHub Actions dependencies used during builds.

## Controls

- The manifest requests `asInvoker`; elevation is not required.
- Capture dimensions are bounded and screenshots are never persisted.
- No HTTP client uploads screenshots or OCR text.
- WebView2 disables host objects, password saving, autofill and developer tools.
- Navigation outside BazaarDB HTTPS origins is cancelled.
- GitHub Actions builds from a locked NuGet dependency graph.
- Releases include SHA-256 checksums.

## Residual risks

- Screen-capture overlays may still conflict with current or future game rules.
- OCR can select an incorrect line and open an irrelevant Wiki result.
- Web content is controlled by BazaarDB and Cloudflare.
- Unsigned early releases may produce a Windows SmartScreen warning.
- Global hotkeys may conflict with other software.

## Verification

Users can build the tagged source locally and compare the resulting behavior. Reproducible byte-for-byte Windows desktop builds are not currently guaranteed because the .NET and WebView2 toolchains may embed environment-dependent metadata.
