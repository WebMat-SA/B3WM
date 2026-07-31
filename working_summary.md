## Objective
- Make the Flutter `MapFlowChart` functionally equivalent to Blazor `NewMapFlow.razor` in zoom, volume profile, agent filters, tooltips, and configuration.

## Important Details
- Flutter chart uses `CustomPainter` (not ECharts like Blazor), so pixel-level rendering must match behavior, not library.
- Volume filter in Blazor has complex fallback logic (nearest bars with VolumeLevel → live data); Flutter now mirrors that.
- Blazor uses `InsideDataZoom { YAxisIndex }` for independent Y zoom; Flutter uses gesture‑based `_yZoom` factor with center‑fixed price range scaling.
- `PointerSignalEvent` / `PointerScrollEvent` / `PointerHoverEvent` are **not available** as explicit type annotations in the user's Flutter SDK — `onHover` callbacks work via inferred type `(e) => …`.
- App runs on Windows (`PS C:\Users\…`); Flutter SDK on WSL has CRLF shebang issues, but Windows `flutter analyze` works.
- `withOpacity` is deprecated in this Flutter version; should use `.withValues()` — low priority (info only).

## Work State
### Completed
- **Structure mapping**: `buildChartData` now maps each structure to its candle by `dd/MM HH:mm` date‑keyed lookup (Blazor parity).
- **Forecast mapping**: Forecast values from `bars[i].forecastPrice` and `forecastHistory` are mapped by date key instead of sequential index.
- **Volume filter fallback**: `_applyVolumeFilter` in `StateService` now searches nearby bars for `VolumeLevel` snapshots, and falls back to live `_volumeLevels` as a last resort.
- **Y‑axis independent zoom**: Added `_yZoom` parameter to `ChartPainter` and `ChartFixedPainter`; `_priceToY` scales price range centered on midpoint, keeping labels in sync.
- **Delta Profile**: Added `_drawDeltaProfile` in `ChartPainter` (green/red horizontal bars); state fields `_deltaVisible/Opacity/SizeH/SizeV` in `StateService`; UI in `ConfigDrawer`.
- **Bubble size clamping**: Bubble radius clamped to `[bubbleSizeMin, bubbleSizeMax]` (default 20–100 px); sliders added to `ConfigDrawer`.
- **Candle timer**: `calcRemainingSeconds()` computes seconds until current candle closes; passed as `remainingSeconds` in `ChartData`.
- **ConfigDrawer**: Added Delta section and bubble Min/Max size sliders; Indicators section entirely removed.
- **Zoom controls**: Replaced scroll‑wheel approach with on‑screen `+` / `–` / reset‑percentage buttons in top‑right corner of chart.
- **Bollinger / indicator system removed entirely**: Removed `IndicatorLineData`, `IndicatorMarkerData` classes, `indicatorLines`/`indicatorMarkers` fields, `_drawIndicatorLines`/`_drawIndicatorMarkers`, `_parseHex` from `chart_painter.dart`. Removed `indicatorData`, `indicatorPlotType`, `indicatorVisible`, `indicatorOpacity`, `_handleIndicatorValue`, `onIndicatorToggled` from `state_service.dart`. Removed `_indicatorSection` and its call from `config_drawer.dart`. Removed `indicator_value` import from `chart_data.dart` and `state_service.dart`.
- **Vertical‑pan locked**: `TransformationController` listener in `_onMatrixChanged` zeros Y translation and Y scale each frame, keeping fixed‑layer aligning with InteractiveViewer.
- **Bubble hover tooltip**: `MouseRegion` wraps `GestureDetector`; `_handleHover` uses inverse matrix to convert screen → virtual canvas coordinates, finds nearest bubble within radius, shows agent/qty/price/tipo tooltip with `_tooltipSource = 'hover'`. Exiting hides hover tooltip.
- **Tap tooltip corrected**: Now uses inverse matrix for accurate candle‑index lookup after horizontal scroll; sets `_tooltipSource = 'tap'` with 4‑second auto‑dismiss (only if source still `'tap'`).
- **Syntax error fixed**: Missing `)` closing the `Positioned` wrapping the candle area caused formatter/analyzer to report spurious errors on spread‑ternary and collection-if. Added the missing bracket; `flutter analyze` now passes with **0 errors, 0 warnings**.

### Active
- *(none)*

### Blocked
- *(none)*

## Next Move
*(waiting for user direction)*

## Relevant Files
- `/mnt/c/Users/webma/b3wm/B3WM.Flutter/lib/ui/widgets/chart/chart_data.dart` — `buildChartData`, `ChartData` class (indicators removed)
- `/mnt/c/Users/webma/b3wm/B3WM.Flutter/lib/services/state_service.dart` — `_applyVolumeFilter`, delta state, process loop (indicator state removed)
- `/mnt/c/Users/webma/b3wm/B3WM.Flutter/lib/ui/widgets/chart/chart_painter.dart` — `_priceToY` with `yZoom`, `_drawDeltaProfile`, bubble clamping (no indicators)
- `/mnt/c/Users/webma/b3wm/B3WM.Flutter/lib/ui/widgets/chart/chart_fixed_painter.dart` — `_priceToY` / `_yToPrice` with `yZoom`
- `/mnt/c/Users/webma/b3wm/B3WM.Flutter/lib/ui/widgets/chart/map_flow_chart.dart` — Y‑zoom buttons, `_onMatrixChanged` (vertical‑pan lock), `MouseRegion` hover, tap tooltip with inverse matrix, Positioned fix
- `/mnt/c/Users/webma/b3wm/B3WM.Flutter/lib/ui/widgets/config_drawer.dart` — Delta, bubble range sections (no indicators)
- `/mnt/c/Users/webma/b3wm/B3WM/B3WM.Client/Pages/NewMapFlow.razor` — Reference Blazor page (target parity)
