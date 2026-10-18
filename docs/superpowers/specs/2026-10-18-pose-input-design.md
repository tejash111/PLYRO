# Sub-project 3: Pose Input

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Parent:** `2026-10-18-motion-bowling-system-design.md`
- **Depends on:** 1 (foundation). It integrates with 2 (TrackingCore), but is built and tested first against recorded and fake sources.
- **Module:** `Bowling.PoseInput`

## 1. Purpose

PoseInput turns raw tracking samples into clean, player-centric input for the rest of the game. It does the following:

- Picks the one player who is playing.
- Smooths their pose.
- Converts it to player space.
- Calibrates them.
- Drives the hand cursor.
- Supervises the tracker's health.
- Records and replays pose sessions for tuning and tests.

**Implements (from Contracts):**

- `IPlayerInput`
- `IPlayerCalibrator`
- `IHandCursor`
- `ICameraPreview`
- `ITrackingDiagnostics`
- `IRecordingIndicator`

It also provides three `ITrackingSource` implementations.

## 2. Components (SOT-2: one type per file)

```
Modules/PoseInput/Runtime/
├─ Components/
│  ├─ PoseInputDriver.cs            [DefaultExecutionOrder(-300)] ticks PoseInputService each Update
│  └─ PoseRecorderHotkeys.cs        dev/operator only: forwards label keys to PoseRecorder (wired by App)
├─ Core/
│  ├─ PoseInputService.cs           facade: implements IPlayerInput, IHandCursor, ICameraPreview, ITrackingDiagnostics
│  ├─ PoseAxes.cs                   ToPlayerSpace — the ONLY MediaPipe→player-space conversion
│  ├─ PlayerLock.cs                 chooses/keeps the active person
│  ├─ PresenceDebouncer.cs          hysteresis for IsPlayerPresent
│  ├─ OneEuroFilter.cs              scalar filter
│  ├─ PoseFilter.cs                 applies OneEuroFilter per landmark per axis
│  ├─ PoseHistory.cs                ring of PlayerPose (2 s), pooled, zero-alloc
│  ├─ PlayerCalibrator.cs           implements IPlayerCalibrator
│  ├─ HandCursorMapper.cs           implements IHandCursor math
│  ├─ TrackingWatchdog.cs           stall detection + restart policy
│  ├─ PoseSamplePool.cs             reusable PoseSample objects
│  ├─ Recording/
│  │  ├─ PoseRecording.cs           data model (format v1, section 6)
│  │  ├─ PoseRecordingReader.cs
│  │  ├─ PoseRecordingWriter.cs
│  │  └─ PoseRecorder.cs            implements IRecordingIndicator; writes raw samples while enabled
│  └─ Sources/
│     ├─ NativeTrackingSource.cs    ITrackingSource over TrackingCore P/Invoke
│     ├─ NativeTrackingInterop.cs   [DllImport("TrackingCore")] declarations + blittable structs
│     ├─ RecordedTrackingSource.cs  ITrackingSource replaying a PoseRecording against IClock
│     └─ NullTrackingSource.cs      Status = Stopped; used when tracking is disabled in tests
└─ Config/
   └─ PoseInputTuning.cs            ScriptableObject (asset: Config/PoseInput/PoseInputTuning.asset)
```

Every number in sections 3–5 is a field on `PoseInputTuning`, using the default shown.

## 3. Pipeline (each Unity frame, `PoseInputService.Tick`)

1. `source.ReadSamplesSince(lastTimestamp, buffer)`. Zero allocations: samples come from `PoseSamplePool`.
2. For each sample, oldest first:
   1. `PlayerLock.Select(sample)` picks a `PersonPose` or none.
   2. If a person is picked, `PoseAxes.ToPlayerSpace` converts world landmarks, `PoseFilter` smooths image and world landmarks, `Confidence` is computed, and a `PlayerPose` is appended to `PoseHistory`.
   3. `PresenceDebouncer.Update(locked && inZone, sample.Timestamp)`.
3. The latest pose goes to `PlayerCalibrator.Update(...)` and `HandCursorMapper.Update(...)`.
4. `TrackingWatchdog.Update(source.Status, newestSampleTime, clock.Now)`.
5. Events (`PresenceChanged`, `StatusChanged`) are raised **after** processing completes, never in the middle of the loop.

### 3.1 Axis conversion

```csharp
// MediaPipe world: metres, origin hip midpoint, +x image-right, +y DOWN, +z AWAY from camera (smaller z = closer).
// Player space (system design 6.1): +x image-right, +y UP, +z TOWARD camera.
public static Vector3 ToPlayerSpace(Vector3 mp) => new Vector3(mp.x, -mp.y, -mp.z);
```

- The signs are confirmed by spike S0 and recorded in `docs/research/2026-10-tracking-spike-report.md`.
- A fixture test uses a real S0 frame where the player's right wrist is raised above the head and pushed toward the camera. It asserts that the wrist's player-space Y is greater than the nose's Y, and that its Z is greater than the hip midpoint's Z (0).

### 3.2 Player lock

**Core joints:** both shoulders, elbows, wrists and hips.

**Candidate:** a person whose mean core-joint visibility is `minCandidateConfidence` (0.5) or more.

**In zone:** the hip-midpoint image x is within [`zoneMinX`, `zoneMaxX`] (0.30–0.70), **and** the body height in the image (nose y to the lower ankle y) is `minBodyHeight` (0.45) or more. The second condition rejects people far behind the player.

**Selection:**

1. If a person is locked, keep the candidate whose hip-midpoint image position is nearest the last locked position. It must be within `maxJump` (0.15). This provides continuity across frames.
2. Otherwise, lock the in-zone candidate with the greatest body height (the closest person).
3. The lock is lost after `lockLostTimeout` (0.5 s) without a match. When a new lock is taken, all filters reset.

**Presence:** `IsPlayerPresent` becomes true after the player has been locked and in the zone for `presenceEnterTime` (1.0 s). It becomes false after `presenceLeaveTime` (1.0 s) without that. Gameplay adds its own pause delay on top (gameplay spec).

### 3.3 Filtering (One Euro)

- One filter per landmark per axis, using the sample timestamps for dt. Visibility and presence pass through unfiltered.
- **Throw profile** (world landmarks, used by ThrowDetection):
  - `minCutoff` 3.0 Hz, `beta` 1.0, `dCutoff` 1.0 Hz.
  - This is deliberately light: lag hurts speed measurement more than jitter does.
- **Cursor profile** (index-fingertip image landmarks, used only by `HandCursorMapper`): `minCutoff` 1.0 Hz, `beta` 0.05, `dCutoff` 1.0 Hz.
- These are starting values. Tune them against S0 recordings, and record the tuned values in the plan's verification notes.

### 3.4 Confidence

`PlayerPose.Confidence` is the mean visibility of the core joints.

## 4. Calibration (`PlayerCalibrator`), system design D-7

```
Begin() → WaitingForFullBody
  WaitingForFullBody  → WaitingForRaisedHand when shoulders, hips, knees and ankles all have visibility ≥ 0.6
  WaitingForRaisedHand → Holding when exactly ONE wrist is ≥ 0.10 m above the nose (player-space Y) AND that arm's
                          elbow angle ≥ 150° (nearly straight). Both wrists raised → stay (ambiguous).
  Holding (1.0 s)      → Done when the same hand stays raised for holdTime; HoldProgress = elapsed / holdTime.
                          Hand drops before the end → back to WaitingForRaisedHand (progress resets).
  Done → Completed(profile)
Any step: timeout 15 s since Begin → Failed(Timeout); presence lost → Failed(PlayerLeft);
          status ≠ Running → Failed(TrackingLost).
```

**Profile** (medians over the samples taken during Holding):

- **BowlingHand:** the raised hand. MediaPipe's left and right are the person's own sides, so they map directly to `Handedness`.
- **ArmLengthM:** |shoulder→elbow| + |elbow→wrist| on the raised side, clamped to [0.35, 0.90].
- **ShoulderWidthM:** |left shoulder − right shoulder|, clamped to [0.25, 0.60].
- **NeutralImageX:** the hip-midpoint image x.

## 5. Hand cursor (`HandCursorMapper`)

- **Active hand:** a wrist is "raised" when its player-space Y is above its shoulder's Y minus 0.05 m. If both are raised, the higher one wins. The cursor is inactive when neither is raised, or when no player is locked.
- **Pointer:** the index-fingertip landmark (19 or 20), in player-space world coordinates, filtered with the cursor profile.
- **Reach box:** in player space, centered 0.15 m toward the active arm's side and 0.10 m above that shoulder. It is 0.70 m wide and 0.45 m tall. The pointer position inside the box maps to (u, v) in [0,1], clamped.
- **Mirroring:** the display is mirrored. When the player moves their hand toward their own right, the cursor moves right on screen. `Position.y` = 0 at the top of the box.
- **Hysteresis:** a cursor that becomes inactive stays at its last position for 0.2 s before `IsActive` turns false, which avoids flicker.

## 6. Recording format (`PoseRecording` v1)

**File naming:** `<yyyyMMdd-HHmmss>-<label>.pose.json.gz`, gzip-compressed UTF-8 JSON. The same format is written by the spike S0 Python recorder and by `PoseRecorder`.

```json
{
  "format": "motion-bowling.pose-recording",
  "version": 1,
  "meta": {
    "recordedAtUtc": "2026-10-20T14:03:11Z",
    "source": "python-mediapipe-<version> | trackingcore-<git sha>",
    "model": "pose_landmarker_full",
    "camera": "Logitech BRIO",
    "fps": 60,
    "worldAxes": "mediapipe",
    "subject": { "heightClass": "child | teen | adult", "handedness": "right | left" },
    "labels": [
      { "t0": 3.20, "t1": 4.90, "kind": "throw", "hand": "right", "intent": "straight | hook-left | hook-right | slow | fast", "note": "" },
      { "t0": 10.0, "t1": 70.0, "kind": "non-throw", "note": "waving, walking" }
    ]
  },
  "samples": [
    { "t": 0.0333, "i": 1, "people": [ { "image": [[0.51, 0.22, -0.31, 0.99, 0.99], "…33 entries"], "world": [["x", "y", "z", "vis", "pres"], "…33"] } ] }
  ]
}
```

- **Contents:** samples are raw, so each recording stores the `PoseSample` contents in MediaPipe axes, with nothing filtered or converted.
- **Timestamps:** `t` is the time in seconds from the first sample.
- **Labels:**
  - A `throw` label's [t0, t1] covers the motion from the start of the backswing until 0.3 s after release.
  - The `intent` field records what the subject tried to do; the throw-detection spec uses it.
- **`PoseRecorder`:**
  - Active only while `OperatorSettings.PoseRecordingEnabled` is true.
  - Writes to `Application.persistentDataPath/recordings/`.
  - Operator hotkeys mark labels: `T` toggles a throw segment, `N` toggles a non-throw segment.
  - `IsRecording` drives the on-screen badge (UI spec).
- **`RecordedTrackingSource`:**
  - Replays samples in real time against `IClock`, offsetting their timestamps onto the current clock.
  - Options: `loop`, and `speed` (1.0 by default).
  - `Status` is `Running` while samples remain, then `Stopped`, or loops back to the start when `loop` is set.

## 7. Native source (`NativeTrackingSource`)

- `Start()` does the following:
  1. Checks `tc_api_version() == 1` and `tc_sizeof_pose_sample() == Marshal.SizeOf<TcPoseSample>()`. A mismatch means `Failed`, and the error is logged.
  2. Calls `tc_start` with the model at `Application.streamingAssetsPath/Models/pose_landmarker_full.task`, the configured camera id, 3 people, target 60 fps, inference 640×360, and confidences of 0.5.
- **Each tick:**
  - `tc_read_samples_since` reads into one preallocated `TcPoseSample[128]`, which is then copied into pooled `PoseSample` objects.
  - `Status` is updated from `tc_get_status()`.
- **Zero-allocation interop layout:** `TcPoseSample` is blittable, so P/Invoke never marshals or allocates.
  - It is declared `[StructLayout(LayoutKind.Sequential)] unsafe struct` with these fields: `double Timestamp; long FrameIndex; int PersonCount; int Reserved; fixed float People[3 * 2 * 33 * 5];`.
  - The `People` buffer is flattened in the order person → image/world → landmark → (x, y, z, visibility, presence).
  - Copying into the pooled objects uses pointer reads.
  - `tc_camera_info` uses `fixed byte UniqueId[128]; fixed byte Name[128];`, and strings are decoded only when the operator panel asks.
  - This is why `Bowling.PoseInput` alone has `allowUnsafeCode: true` (foundation section 4).
- **Preview:** `Texture2D.CreateExternalTexture` is created on the first non-null `tc_get_preview_texture()` and updated each frame with `UpdateExternalTexture`. It is recreated if the camera's size changes.
- **Diagnostics:**
  - `tc_get_stats` is polled at 2 Hz and provides `ITrackingDiagnostics.Stats`.
  - `tc_list_cameras` is called on `Start` and whenever the operator panel opens.
- **Shutdown:** `Stop()` calls `tc_stop`. App calls `Stop()` in `OnApplicationQuit`.
- **Camera switch:** `PoseInputService.SetCamera(string uniqueId)` stops and restarts the source with the new camera id, and resets the lock and the filters. App calls it when `IOperatorActions.SettingsApplied` reports a changed `CameraUniqueId`.

## 8. Watchdog (system design section 9)

- **First stall:** if `Running` but no new sample arrives for 1.0 s, call `Stop()` then `Start()` once.
- **Second stall:** a second stall within 30 s reports `Failed` through `StatusChanged`, then retries `Start()` every 5 s.
- **Errors:** `NoCamera`, `PermissionDenied` and `ModelMissing` pass straight through. `NoCamera` retries every 2 s. The other two never retry automatically; the operator has to fix them.

## 9. Tests

- **EditMode, plain classes, with `FakeClock` and `FakeTrackingSource` from `Bowling.Testing`:**
  - `OneEuroFilter`: a constant input settles with no offset; a step response has a monotonic rise; a larger `beta` lowers lag at high speed.
  - `PoseAxes`: unit vectors; the S0 fixture frame (section 3.1).
  - `PlayerLock`:
    - A single person in the zone locks.
    - With two people, the bigger one locks.
    - Continuity: a locked person moving 0.1 per frame is kept, even though another person is bigger.
    - Leaving the zone clears presence after 1.0 s.
    - The lock is lost after 0.5 s.
  - `PlayerCalibrator`:
    - The happy path gives a profile matching synthetic arm lengths within 0.01 m.
    - Both hands raised never completes.
    - Timeout, `PlayerLeft` and `TrackingLost` each fail as specified.
  - `HandCursorMapper`: the box corners map to (0,0) and (1,1); moving toward the player's right increases `Position.x`; the 0.2 s inactive hysteresis.
  - `TrackingWatchdog`: the stall → restart → stall → `Failed` → retry timeline.
  - `PoseRecordingWriter` → `PoseRecordingReader` round trip preserves every value bit-exactly.
  - `RecordedTrackingSource`: timing within one frame of real time; loop behavior.
- **Zero-alloc test:** `PoseInputService.Tick` over 600 recorded samples allocates 0 bytes after warm-up. Use `GC.GetAllocatedBytesForCurrentThread` around the loop.
- **Interop layout test:** `Marshal.SizeOf<TcPoseSample>() == 8 + 8 + 4 + 4 + 3 × 2 × 33 × 20 = 3984`. This is checked again at runtime against the native value.

## 10. Acceptance criteria

1. All tests in section 9 pass.
2. Playing back the S0 recordings through `RecordedTrackingSource`, the lock never jumps to a bystander in the multi-person recordings.
3. Calibration completes in 3 s or less for 9 of 10 S0 subjects.
4. With the native source on the reference hardware, `PoseInputService.Tick` takes 0.3 ms or less per frame at p95 (Unity Profiler marker `PoseInput.Tick`).

## 11. Decisions made here (Claude's calls, please check)

- Arm length is measured as the sum of its two segments while the arm is raised, rather than the straight-line distance. This is more robust to a slight elbow bend.
- The play zone is the middle 40% of the image horizontally. The booth's floor box must match it (platform spec, booth setup).
- Recordings store raw, unfiltered samples. Then any filter change can be re-evaluated against old data.
