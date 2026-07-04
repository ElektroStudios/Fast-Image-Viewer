# Fast Image Viewer (FIV) Change Log 📋

## v1.0.1 **(current)** 🆕

My goal with this update is simple: keep turning **FIV** into the image viewer I'd actually want to use every day. ✅

It still lacks many features found in professional image viewers, though. That said, **FIV** has never aspired to be a professional image viewer.

### New Features
- Added 'Slideshow' option to enable automatic image transitioning in fullscreen with custom interval settings.
- Added 'Advanced' menu.
- Added option in 'Advanced' menu to set a millisecond delay between rapid forward and backward navigation events.
- Added option in 'Advanced' menu to disable image resizing on load for higher quality at the cost of loading speed.
- Added 'Remember window size and position' option to remember and restore application window size and position on program startup.
- Added 'Upscale small images to fit window' option to control whether small images are scaled up to fit the window.

### Enhancements & Improvements
- Rewrote zoom algorithm for a smoother experience that maintains focus on the center point.
- Improved "Actual Size" mode so images maintain rotation and remain centered dynamically during window resize.
- Preserved image rotation state when switching into Actual Size mode.

### Bug Fixes
- Fixed incorrect image dimensions (width x height) being reported in metadata.
- Fixed UI synchronization issues when deleting or moving the current image when in "Actual Size" mode.
- Implemented dimension caps when zooming-in high-resolution images to prevent GDI+ malfunctions and application freezes.

## v1.0 🔄
Initial Release.