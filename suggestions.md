# InfoPanel — Performance & Usability Suggestions

Analysis of the `1.4.x` branch (2026-10-30). Paths are relative to the repo root. Line numbers are approximate guides, not patch targets.

Items are grouped by type and ordered by expected impact within each group. Section 1 lists defects found during the review. They aren't strictly performance issues, but several of them hurt how reliable the app feels in long-running use.

---

## Summary

| # | Area | Item | Impact | Effort |
|---|------|------|--------|--------|
| 1.1 | Reliability | Beada / LianLi / Turing USB render loops die silently on any exception | High | S |
| 1.2 | Reliability | URL-image refresh disposes a `LockedImage` that another thread may still be drawing | High | S |
| 1.3 | Reliability | HWiNFO polling runs on overlapping `System.Timers.Timer` callbacks, not a dedicated thread | Medium | S |
| 1.4 | Correctness | Animation state (graph auto-range, gauge smoothing) advances per draw call, so it speeds up with each extra renderer | Medium | M |
| 1.5 | Correctness | "Wrap" toggle is inverted (`Wrap = true` → single line) | Medium | S |
| 1.6 | Correctness | Line graph clamps to `maxValue` instead of `maxValue - minValue` | Low | XS |
| 1.7 | Correctness | Image with zero size `return`s instead of `break`s, skipping the selection outline | Low | XS |
| 1.8 | Reliability | LibreMonitor loop exits permanently on the first exception | Medium | S |
| 2.1 | Perf | Re-render only when something changed (static profiles redraw at full FPS) | Very high | M–L |
| 2.2 | Perf | Render each profile once per tick and share the result between consumers | High | M |
| 2.3 | Perf | Cache RichTextKit `TextBlock` layouts between frames | High | M |
| 2.4 | Perf | Stop allocating full-frame `byte[]` / `SKBitmap` every frame (LOH churn) | High | S–M |
| 2.5 | Perf | Remember the adaptive JPEG quality instead of re-running the ladder every frame | Medium | XS |
| 2.6 | Perf | Charts: no per-frame bitmap, ring buffer instead of copying 4096 samples | Medium | S |
| 2.7 | Perf | Parse colors once, not per draw call | Medium | S |
| 2.8 | Perf | `SKImage.FromBitmap` copies pixels; draw bitmaps directly | Medium | XS |
| 2.9 | Perf | Video frames are copied 3× per frame through WPF `BitmapSource` | Medium | M |
| 2.10 | Perf | HWHash uses `Marshal.PtrToStructure` + string marshaling per sensor per poll | Medium | S |
| 2.11 | Perf | Web server: PNG-encodes a fresh render per request per client | Medium | S |
| 2.12 | Perf | Video overlay rebuilds typefaces/paints/bitmap every frame | Low | XS |
| 2.13 | Perf | Shared `HttpClient` for image downloads | Low | XS |
| 2.14 | Perf | Undo snapshot serializes the whole profile on every property change, including selection | Low | S |
| 3.x | Usability | Configurable poll rates, panel error surfacing, web server fixes, etc. | — | — |
| 4.x | Maintainability | De-duplicate panel device tasks, remove dead renderers | — | — |

---

## 1. Defects found during the review

### 1.1 Panel render loops have no error handling (Beada, LianLi, Turing USB)

`InfoPanel/Services/BeadaPanelDeviceTask.cs:202`, `LianLiPanelDeviceTask.cs:234`, `TuringPanelUsbDeviceTask.cs:166`

The render task is a bare `while` loop with no `try/catch`. If a single frame throws (a disposed image, a bad font, a plugin table with unexpected shape), the render task faults and the send loop keeps running. The send loop just waits on `_frameAvailable` forever. The panel freezes on its last frame with no log entry and no error in the UI, and it never recovers until the app restarts. `Task.WhenAll` never completes, so the outer reconnect logic never runs either.

JL, Jonsbo, Thermaltake and Vmax already do this correctly: catch, log, set `errorMessage`, and cancel `renderCts`. Port that pattern to the other three. Better still, catch per frame and continue, so one bad frame doesn't take the device offline.

Minor: Beada uses the global `Settings.TargetFrameRate` and `Task.Delay(..., token)` instead of the per-device rate and `renderToken` that the other vendors use.

### 1.2 URL image refresh races with rendering

`InfoPanel/Utils/Cache.cs:241`, `InfoPanel/Models/LockedImage.cs:641`

`RefreshImageSafe` calls `ImageCache.Set(path, refreshed)`. That evicts the old entry, and the eviction callback calls `Dispose()` on it right away. Any render thread that already holds the old `LockedImage` from `Cache.GetLocalImage` then calls `AccessSK`, which runs `ObjectDisposedException.ThrowIf(IsDisposed, this)`. On the overlay that's logged and the frame is dropped. On Beada/LianLi/Turing it kills the panel (see 1.1).

Fix options:
- Return silently from `AccessSK`/`AccessSVG` when disposed, instead of throwing. This is the simplest fix, and a dropped frame is harmless.
- Or defer disposal of evicted images by one tick, or reference-count them.

### 1.3 HWiNFO polling is not on a dedicated thread, and can overlap

`InfoPanel/HWHash/HWHash.cs:75-90, 175-180`

`Launch()` creates `CoreThread`, but that thread only subscribes `aTimer.Elapsed` and exits. Polling actually runs on thread-pool threads via `System.Timers.Timer`, so the `HighPriority` flag has no effect. `System.Timers.Timer` raises `Elapsed` again even if the previous callback is still running. If a poll takes longer than the interval (remote HWiNFO connections, a slow machine), two `PollSensorData` calls run at once. They then read and modify `SENSORHASH` entries non-atomically (read struct → modify → write back) and run `IndexOrder++`.

Suggested fix: use a real loop on the dedicated thread, or a `PeriodicTimer` in a `BackgroundTask` like the other monitors. At minimum, add an `Interlocked` re-entrancy guard in `PollSensorData`. CLAUDE.md also describes this as a dedicated thread and should be corrected.

### 1.4 Animation state advances per draw call, not per unit of time

- `InfoPanel/Drawing/GraphDraw.cs:37` (`ResolveAutoRange`): contracts the auto-range by 5% on every call, and the comment assumes 40 fps.
- `InfoPanel/Models/GaugeDisplayItem.cs:292-304`: gauge smoothing uses `_lastGaugeUpdate` stored on the item.

The same `DisplayItem` is drawn by the overlay window, every USB panel showing that profile, the profile preview, and the web server, each on its own thread. With N consumers:
- The graph auto-range contracts N times faster, and also faster or slower depending on frame rate.
- Gauge `deltaSeconds` becomes the time between *different* renderers' calls, so smoothing behaves differently depending on how many panels are attached. The state is also mutated from several threads without synchronization.

Make the contraction time-based (`1 - exp(-dt/τ)` from a stored timestamp, like the gauge already tries to do). Store the "last update" timestamp so it only advances on wall-clock time, not per call, and guard it. Suggestion 2.2 (render once, share the result) also fixes this as a side effect.

### 1.5 Text "Wrap" toggle is inverted

`InfoPanel/Drawing/SkiaGraphics.cs:259` (and `MeasureString` at :855)

```csharp
MaxLines = wrap ? 1 : null,
```

The toolbar toggle is labelled "Wrap Text (fixed width required)" (`Views/Components/Text/TextProperties.xaml:41`). Turning it **on** limits text to one line, and turning it **off** lets it wrap without limit. `Wrap` defaults to `true`, so existing profiles depend on the current behavior. Either relabel the toggle ("Single line") or fix the logic with a profile-version migration that flips the stored value.

### 1.6 Line graph value clamp is wrong when `MinValue != 0`

`InfoPanel/Drawing/GraphDraw.cs:278`

```csharp
var value = Math.Max(values[i] - minValue, 0);
value = Math.Min(value, maxValue);          // should be (maxValue - minValue)
```

With min=50, max=100 and a reading of 150, the normalized value is 2.0, so the line is drawn above the chart and clipped away instead of pinned to the top. Check the histogram path for the same pattern.

### 1.7 Zero-size image skips the selection rectangle

`InfoPanel/Drawing/PanelDraw.cs:262`: `return;` inside the `switch` exits `Draw()` before `selectedRectangles.Add(...)`. A selected image with zero evaluated size therefore gets no selection outline in the editor. Use `break`.

### 1.8 LibreMonitor stops for good on the first exception

`InfoPanel/Monitors/LibreMonitor.cs:71-108`

The `try/catch` wraps the whole `while` loop. One exception from a driver or `hardware.Update()` call (these do happen with some GPUs and storage controllers) ends polling, clears `SENSORHASH`, and every Libre sensor stays blank until restart. Catch inside the loop, log at a throttled rate, and continue.

The loop also only registers sensors two levels deep (hardware → subhardware). `UpdateVisitor` recurses through every level, but sensors on sub-sub-hardware are never added to `SENSORHASH`.

---

## 2. Performance

### Background: what one frame costs today

For every consumer (each overlay window, each USB panel, each preview, each web request), every frame does the following:

1. `PanelDrawTask.RenderSK` allocates a new full-size `SKBitmap`.
2. Each text item builds a new RichTextKit `TextBlock` (full shaping and layout) and parses its color string. With glow on, it also adds two `SaveLayer`s and a blur.
3. Each chart allocates a new `SKBitmap`, copies up to 4096 samples, draws, then copies the bitmap again via `SKImage.FromBitmap`.
4. The panel task may resize/rotate into *another* new bitmap, then calls `.Bytes` (another full copy), or JPEG-encodes and calls `.ToArray()`.

At 480×480 RGB565 that's about 460 KB per copy. Anything over 85 KB lands on the Large Object Heap, so the app drives frequent Gen2 GCs. Meanwhile, the underlying data typically changes once every 500–2000 ms.

### 2.1 Only re-render when something changed (biggest win)

HWiNFO is polled every 500 ms (`App.xaml.cs:378`) and Libre every 2000 ms (`LibreMonitor.cs:98`), but overlays and panels re-render at `TargetFrameRate` regardless. For a typical "sensor text + static background" profile, most frames are byte-identical.

Suggested approach:
- Give each profile a cheap **"is animated"** flag, recomputed when its items change. It's true if the profile has a video, an animated GIF (`Frames > 1`), marquee text, an animated gradient, a gauge with smoothing in progress, a plugin image, a clock with seconds, or an active selection blink in the editor.
- Keep a global **data version** counter that HWHash, LibreMonitor and PluginMonitor bump when they publish new values, plus a per-profile **edit version** that bumps on any display item property change.
- In each render loop: if the profile isn't animated and neither version has changed since the last frame, skip rendering and sending. Most panels still need a keep-alive; the JL keep-alive logic already shows how to handle that.

A cheaper partial step for USB panels: hash the final buffer, for example `XxHash64` over the RGB565 or JPEG bytes, and skip the USB write when it matches the last frame sent. This saves USB bandwidth and device-side decode, but not the render itself.

### 2.2 Render each profile once per tick and share the result

If two USB panels, the overlay window and the web server all show profile X, X is rendered four times per tick on four threads. That contends on the same `LockedImage.Lock`, which has a 50 ms `TryEnter` timeout, so images silently drop out under contention.

Add a small `ProfileFrameCache` keyed by `(profileGuid, width, height, colorType)` that holds the last rendered `SKImage` plus its timestamp and version. Consumers ask for "a frame no older than N ms". The first caller renders; the others reuse the result. Rotation and scaling stay per-device. This also fixes 1.4, because each item is evaluated once per tick.

### 2.3 Cache text layout

`InfoPanel/Drawing/SkiaGraphics.cs:254-292` (`DrawStringCore`) and `:848` (`MeasureString`)

A new `TextBlock` and `Style` are created and laid out for every string on every frame. Marquee text measures and lays out the same string up to three times per frame (`PanelDraw.cs:901, 934, 939`). Glow text lays it out twice.

Cache the laid-out `TextBlock` per display item, keyed by `(text, font, style, size, color, width, flags)`. Sensor values change at most every 500 ms, so the hit rate will be very high. A small `ConditionalWeakTable<DisplayItem, CachedLayout>` or a field on `TextDisplayItem` is enough.

For glow, consider rendering text+glow into a cached `SKPicture` or `SKImage` keyed the same way. Blur is one of the most expensive operations in the pipeline.

### 2.4 Remove per-frame large allocations

| Location | Allocation | Suggestion |
|---|---|---|
| `Services/PanelDrawTask.cs:14` | new `SKBitmap` per frame | Let callers pass in a reusable bitmap/surface, kept per device |
| `Extensions/SKBitmapExtensions.cs:33` | new result bitmap per frame when rotated/scaled | Reuse a per-device target bitmap |
| `Services/BeadaPanelDeviceTask.cs:44` (and similar) | `SKBitmap.Bytes` copies the whole frame | Copy into a pooled buffer (`ArrayPool<byte>.Shared`), or write directly from `GetPixelSpan()` if the USB API takes a span/pointer. Note: `frameBufferPool` on line ~198 is declared but never used |
| `Services/ThermalrightPanelDeviceTask.cs:355` | `new byte[w*h*2]` per frame for the RGB565 conversion | Reuse a per-device buffer |
| `*PanelDeviceTask` JPEG paths | `SKData.ToArray()` | Send `SKData.AsSpan()` directly where the transport allows |

After this change, also consider `GCSettings.LatencyMode = SustainedLowLatency` for the panel threads. Look at the GC behavior first.

### 2.5 Remember the adaptive JPEG quality

`InfoPanel/Services/ThermalrightPanelDeviceTask.cs:202-217`, `JlPanelDeviceTask.cs:243-252`, `JonsboPanelDeviceTask.cs:243`

When a frame is too big, these loops re-encode at lower quality until it fits, and they do that again on every frame starting from the configured quality. A busy profile can cost up to 5–7 full JPEG encodes per frame. Store the last quality that fitted per device. Start from it, and nudge it back up only occasionally (for example +5 every few seconds of frames comfortably under the limit).

### 2.6 Charts

`InfoPanel/Drawing/PanelDraw.cs:394-407`, `InfoPanel/Drawing/GraphDraw.cs:225`

- Each chart allocates an off-screen `SKBitmap`, draws into it, then `DrawBitmap` copies it again into an `SKImage`. Instead, draw directly onto the main canvas with `canvas.Save(); Translate(x,y); Scale(...); ClipRect(...)`, plus a rotate or flip matrix when needed. That needs no off-screen bitmap.
- `tempValues = [.. queue]` copies up to 4096 doubles (32 KB) per chart per frame, then `values = tempValues[^size..]` copies again. Replace `Queue<double>` with a fixed-size ring buffer and copy only the `size` samples actually drawn, into a pooled array or `stackalloc` span.
- Graph samples are only collected while something is rendering (`GraphDraw.Run`). History therefore has gaps whenever no panel or overlay is active, and the sample rate depends on render cadence. Consider sampling from the monitor threads instead.

### 2.7 Parse colors once

Colors are stored as strings and parsed with `SKColor.Parse` / `TryParse` on every draw call, for example in `SkiaGraphics.DrawStringCore`, `DrawLine`, `FillRectangle`, `FillDonut`, `FillPie`, the `ShapeDisplayItem` fill and frame paths, `GraphDraw`, and `PanelDraw.cs:112` (a constant `SKColor.Parse("#FFFFA500")`). Cache the parsed `SKColor` on the display item when the string property changes, using a partial `OnXxxChanged` hook from CommunityToolkit, or a small static `ConcurrentDictionary<string, SKColor>`. `WithAlpha` (`SkiaGraphics.cs:245`) also round-trips through string formatting every frame for glow text.

### 2.8 `SKImage.FromBitmap` copies pixels

`InfoPanel/Drawing/SkiaGraphics.cs:46`, `Extensions/SKBitmapExtensions.cs:55`, `PanelDraw.cs:333`

`SKImage.FromBitmap` copies the pixel data unless the bitmap is immutable. Use `Canvas.DrawBitmap(bitmap, destRect, paint)` directly, or call `bitmap.SetImmutable()` first when you need an `SKImage`.

### 2.9 Video frames: three copies per frame

`InfoPanel/Models/LockedImage.cs:622-637, 696-700`

The path is `TakeSnapshotToBitmapSource` (WPF bitmap) → `CopyPixels` into a new `byte[]` (on the LOH for any real video) → `SKImage.FromPixelCopy` (another copy), at render frame rate, per consumer. At minimum:
- Rent the `byte[]` from `ArrayPool`, and use `SKImage.FromPixels` with a release delegate, or a reused `SKBitmap` with `InstallPixels`.
- Better: check whether FlyleafLib exposes the decoded frame (D3D11 texture or mapped staging buffer) directly, and skip the WPF round-trip.

The constructor also has `Thread.Sleep(50)` (`:200`) to wait for stream info, which is fragile on slow networks. Prefer waiting on the player's opened or status event.

### 2.10 HWiNFO reading allocations

`InfoPanel/HWHash/HWHash.cs:345-370, 374-433`

For every sensor reading on every poll, the code allocates and pins a `GCHandle` and calls `Marshal.PtrToStructure` (reflection-based). That call also decodes the fixed-length `NameDefault`, `NameCustom` and `Unit` strings, which are only needed the first time a sensor is seen. It then does three dictionary operations on each of two dictionaries.

Suggested:
- Read the whole readings block once (`MemoryMappedViewAccessor.SafeMemoryMappedViewHandle.AcquirePointer` or `ReadArray`) and use a blittable struct with `fixed byte` name fields. On the update path, read only `Value`, `ValueMin`, `ValueMax` and `ValueAvg`, and decode the strings only for new keys.
- Use `CollectionsMarshal.GetValueRefOrAddDefault`-style updates. Or store the readings in a class so updates mutate in place instead of copying the struct out and back in.

### 2.11 Web server

`InfoPanel/Services/WebServerTask.cs:284-310`

- Every `/{id}/image` request runs a full render plus a PNG encode at the default compression level. With several browsers open, each one triggers its own render at its own refresh rate. Cache the encoded frame per profile for `WebServerRefreshRate` ms, or serve from the shared frame cache in 2.2.
- PNG is slow to encode. Offer JPEG or WebP (`SKEncodedImageFormat.Webp`) as an option, or at least lower the PNG compression level.
- `data.ToArray()` adds a copy; write `data.AsSpan()` instead.
- `/{id}` re-reads `index.html` from disk on every request, relative to the **current working directory** (`"index.html"`). It breaks if InfoPanel starts with a different working directory, such as autostart from Task Scheduler or a shortcut with a different "Start in" folder. Load it once from `AppContext.BaseDirectory`.
- `id < Profiles.Count` doesn't reject negative ids, so `/-1/image` returns a 500.

### 2.12 Video overlay rebuilt every frame

`InfoPanel/Drawing/PanelDraw.cs:675-829`

`CreateVideoOverlay` runs per frame. It allocates a bitmap, calls `SKTypeface.FromFamilyName` three to four times (a font-manager lookup each time), and parses a dozen color strings. Make the typefaces, paints and colors `static readonly`, and draw straight onto the canvas instead of into a temporary bitmap.

### 2.13 Shared `HttpClient`

`InfoPanel/Models/LockedImage.cs:232`: a new `HttpClient` per image load, including every refresh interval. Use one static `HttpClient`, or `SocketsHttpHandler` with `PooledConnectionLifetime`, and set an explicit `Timeout`. The default is 100 s, which can stall a refresh slot.

### 2.14 Undo snapshots on every property change

`InfoPanel/Models/SharedModel.cs:193-225`

Any `PropertyChanged` on any display item in the selected profile, including `Selected` toggles, schedules a debounced full-profile XML serialization and string compare. On large profiles this is noticeable while dragging items or clicking through the list. Ignore view-only properties (`Selected`, and any runtime-only state), or keep a dirty flag and serialize lazily when the user actually undoes.

### Smaller items

- `SkiaGraphics.CreateTypeface` builds its cache key with string interpolation on every call (every text draw). Use a `(string, string, bool, bool)` tuple key. Also, the `using var typeface = SKTypeface.FromFamilyName(fontName);` at `:315` is unused.
- `ExtractBaseFamilyName` runs 18 `Regex.Replace` calls. It only runs on a cache miss, but a single precompiled `[GeneratedRegex]` alternation is simpler.
- `LockedImage.GetSKBitmapFromSK` (`:476`) does `Copy(Bgra8888)` and then `Resize`, which is two full-size allocations per cache miss. Resize straight from `_compositeBitmap`.
- `Profile` previews (`ProfilePreviewCoordinator.cs:184`) render with `cache: false`, so every image is re-decoded and Mitchell-resized every 500 ms per visible profile. Give previews a cache entry, since they already have their own `PREVIEW-` cache hint.
- Overlay windows all share the single `DisplayWindowThread`, so several large overlays serialize behind one another. If users run many overlays, consider rendering off-thread into an `SKImage` and only presenting on the dispatcher.
- The plugin IPC uses StreamJsonRpc's default JSON formatter. If plugin table sensors get large, `MessagePackFormatter` cuts serialization cost substantially. Both host and client must change together.

---

## 3. Usability

1. **Make sensor poll rates configurable.** HWiNFO is hard-coded to 500 ms (`App.xaml.cs:378`) and Libre to 2000 ms (`LibreMonitor.cs:98`). Users who set HWiNFO to 2000 ms get 4× redundant polling. Users who want snappier Libre readings can't have them. Expose both in Settings, next to `TargetFrameRate`, and explain that render FPS above the poll rate only helps animations.
2. **Show panel errors in the UI consistently.** JL, Jonsbo, Thermaltake and Vmax set `errorMessage` on the device. Beada, LianLi and Turing USB only log, or (see 1.1) don't even log. Users see a frozen panel with "running" status. Use one "last error + timestamp" field across all vendors, shown on the USB Panels page, plus a "Reconnect" button.
3. **Show a placeholder for images that fail to load.** A failed `LockedImage` (missing file, 404) is still cached with `Loaded = false`. `PanelDraw` then draws nothing instead of the "no image" placeholder it uses while loading, so a broken path looks the same as an empty item. Draw the placeholder (ideally with a warning tint) when `!Loaded`. Also retry missing local files when they reappear; today the failed entry keeps sliding its expiration for as long as it's drawn.
4. **Fix the Wrap toggle** (1.5). As it stands, the tooltip describes the opposite of what happens.
5. **Show effective FPS per consumer.** The per-profile FPS overlay exists (`Profile.ShowFps`), but the USB Panels page could also show actual vs target FPS and frame time per device. `fpsCounter` already computes these. That makes it obvious when a device is transport-bound (JPEG too big, USB slow) rather than render-bound.
6. **Web server.** Load `index.html` from the install folder (see 2.11) so the remote view works under autostart. When the listen IP is `0.0.0.0` or `*`, show a warning in Settings that sensor data is exposed to the LAN without authentication. Consider an optional token query parameter.
7. **Explain frame rate in terms of CPU cost.** With 2.1 in place, a "Render only on change (saves CPU)" toggle that defaults on gives users a clear lever. Until then, a hint next to `TargetFrameRate` saying that each attached panel or overlay renders independently would explain unexpectedly high CPU use.
8. **Gauge and graph behavior should not depend on how many panels are attached** (1.4). Users currently see different smoothing on their overlay and their USB panel for the same profile.

---

## 4. Maintainability (affects how cheaply the above can be done)

- **Panel device tasks are near-duplicates.** Each of the 8 vendors re-implements the render-thread / send-thread / latest-frame / adaptive-delay pattern, and they have drifted apart (error handling, per-device vs global FPS, `token` vs `renderToken`, JPEG quality ladders). `JlPanelDeviceTask` and `JonsboPanelDeviceTask` are almost line-for-line copies. A `PanelDeviceTaskBase` with abstract `GenerateFrame()` / `SendFrame(frame)` / `OnBrightnessChanged()` would fix 1.1, 2.4, 2.5 and the frame-skip in 2.1 in one place.
- **Dead renderer code.** `Drawing/AcceleratedGraphics.cs`, `CompatGraphics.cs` and `MyGraphics.cs` are fully commented out (~800 lines). Remove them; git history keeps them.
- **`ThermalrightPanelDeviceTask.cs` is about 2,000 lines** and handles several transport protocols (ChiZhu, Trofeo, SCSI, HID RGB565). Splitting it per protocol would make the rendering and allocation changes above much less risky.
- **No tests exist.** The pure parts of the render pipeline would be easy to cover and would protect the changes above: `GraphDraw.ResolveAutoRange`, RGB565 conversion, `EnsureBitmapSize`, `RectToPath`, and `SensorDisplayItem` value formatting. A small xUnit project, plus a "render profile X to PNG and compare" golden test, would catch regressions from text-layout caching and color caching.

---

## Suggested order of work

1. **Quick fixes (hours):** 1.1, 1.2 (return instead of throw), 1.3 (re-entrancy guard), 1.6, 1.7, 1.8, 2.5, 2.8, 2.13, and the `index.html` path in 2.11.
2. **Allocation pass (1–2 days):** 2.4, 2.6, 2.7, 2.12. Measure with `dotnet-counters monitor --process-id <pid> System.Runtime` (watch `gen-2-gc-count` and `alloc-rate`) before and after.
3. **Text layout cache (2.3)**, measured on a text-heavy profile.
4. **Architecture:** a `PanelDeviceTaskBase`, then the shared frame cache (2.2), then render-on-change (2.1). Together these give the largest CPU reduction for typical users.
