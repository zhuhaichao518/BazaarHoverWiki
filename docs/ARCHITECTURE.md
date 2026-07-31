# Architecture

Bazaar Hover Wiki is deliberately small and process-isolated.

```text
The Bazaar window
      │
      │ F hotkey → Windows screen capture near cursor
      ▼
In-memory PNG ──► Windows.Media.Ocr
                       │
                       │ title, color and geometry scoring
                       ▼
             HTTPS BazaarDB search
                       │
                       ▼
              WebView2 overlay window
```

## Components

- `ScreenCapture`: captures a bounded rectangle around the current cursor. The bitmap remains in memory.
- `HoverOcrService`: uses installed Windows OCR language packs and ranks recognized lines by title-like visual and textual features.
- `FrameColorMap`: samples OCR bounding boxes and filters blue/cyan interface and effect text.
- `MainWindow`: owns the manual scan lifecycle and global hotkeys; no background OCR scheduler runs.
- `WikiWindow`: renders BazaarDB in a non-activating, draggable and resizable WebView2 window.
- `AppSettings`: loads the small, reviewable JSON configuration.

## Non-goals

- Game process injection or runtime patching.
- Game memory, log or network inspection.
- Automated gameplay or input.
- Bundling proprietary game assets or a copied card database.
- Match tracking, analytics, telemetry or accounts.

## Design trade-offs

OCR is less precise than reading internal game state, but it preserves a much clearer isolation boundary. The manual search field is intentional: recognition failures remain visible and correctable instead of being hidden behind opaque heuristics.
