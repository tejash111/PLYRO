# Sub-project 2: TrackingCore Native Plugin

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Parent:** `2026-10-18-motion-bowling-system-design.md`
- **Depends on:** 1 (foundation)
- **Consumed by:** 3 (pose input; P/Invoke lives there)

## 1. Purpose

TrackingCore is a native macOS plugin, `TrackingCore.bundle`, that owns the camera and MediaPipe Pose. It does this on its own threads, so that Unity only ever copies small landmark structs.

It is the main reason the game feels responsive:

- No camera frames pass through C#.
- No Unity frame waits for inference.
- Stale frames are dropped rather than queued.

## 2. Scope

**In scope:**

- AVFoundation capture.
- The MediaPipe C++ `PoseLandmarker` wrapper.
- The latest-frame mailbox.
- The lock-free sample ring buffer.
- The Metal preview texture.
- The C API.
- Camera discovery.
- Stats.
- Build scripts for MediaPipe and for the bundle.
- XCTests.

**Out of scope:**

- Axis conversion. PoseInput does it, so that native and recorded data share one code path (system design 6.1).
- Player selection and filtering (PoseInput).
- The C# side (PoseInput).

## 3. Source layout (SOT-2: one responsibility per file)

```
native/TrackingCore/
├─ TrackingCore.xcodeproj              target: TrackingCore.bundle (arm64, macOS 14.0)
├─ Sources/
│  ├─ CApi/TrackingCoreCApi.h          THE contract (section 6) — C only, no ObjC/C++ types
│  ├─ CApi/TrackingCoreCApi.mm         thin: validates args, forwards to TrackingService
│  ├─ CApi/UnityPluginLifecycle.mm     UnityPluginLoad/Unload, captures IUnityGraphicsMetal
│  ├─ Core/TrackingService.h/.mm       owns lifecycle, threads, status, last error
│  ├─ Core/StatusCodes.h               tc_status helpers
│  ├─ Core/Stats.h/.mm                 fps + latency percentiles (rolling 2 s window)
│  ├─ Capture/CaptureSource.h          protocol: start/stop, delivers CVPixelBuffer + PTS
│  ├─ Capture/CameraCaptureSource.h/.mm AVCaptureSession implementation
│  ├─ Capture/FileCaptureSource.h/.mm  PNG-sequence implementation (tests / CI without camera)
│  ├─ Capture/CameraCatalog.h/.mm      device discovery + format selection
│  ├─ Capture/FrameMailbox.h           single-slot latest-frame handoff (atomic swap, drops stale)
│  ├─ Inference/PoseEngine.h/.mm       ObjC++ wrapper around the facade below
│  ├─ Inference/PixelConverter.h/.mm   BGRA → RGBA + downscale via vImage, reusable buffers
│  ├─ Buffer/PoseRingBuffer.h          SPSC ring of tc_pose_sample with per-slot seqlock
│  └─ Preview/PreviewTexture.h/.mm     CVMetalTextureCache → MTLTexture, triple-retained
├─ mediapipe_facade/                   Bazel package compiled INTO the static lib
│  ├─ BUILD
│  ├─ pose_facade.h                    plain C++ API, no MediaPipe/absl/protobuf types in the header
│  └─ pose_facade.cc
├─ third_party/mediapipe/              Bazel workspace pinning MediaPipe (VERSION file = git tag)
├─ Tests/                              XCTest bundle
│  ├─ PoseRingBufferTests.mm
│  ├─ FrameMailboxTests.mm
│  ├─ CApiLifecycleTests.mm
│  ├─ PoseEngineFixtureTests.mm
│  └─ Fixtures/                        PNG frames of a standing person (Git LFS; captured in spike S0)
└─ scripts/
   ├─ build_mediapipe.sh               bazelisk → libmediapipe_pose.a
   ├─ fetch_models.sh                  downloads .task models, verifies SHA-256 from models.lock
   └─ build_bundle.sh                  xcodebuild Release → copies bundle into Unity Assets/Plugins/macOS
```

**The MediaPipe facade boundary.** MediaPipe, abseil and protobuf headers never reach Objective-C++ code. `pose_facade.h` exposes only this:

```cpp
namespace tcmp {
struct FacadeConfig { std::string model_path; int max_people; float min_detection, min_presence, min_tracking; };
struct FacadeLandmark { float x, y, z, visibility, presence; };
struct FacadePerson { FacadeLandmark image[33]; FacadeLandmark world[33]; };
class PoseFacade {
 public:
  static std::unique_ptr<PoseFacade> Create(const FacadeConfig&, std::string* error);
  // rgba: tightly packed RGBA8, width*height*4 bytes. timestamp_ms strictly increasing.
  int Detect(const uint8_t* rgba, int width, int height, int64_t timestamp_ms,
             FacadePerson* out_people, int max_people, std::string* error);  // returns person count or -1
  virtual ~PoseFacade();
};
}
```

**Build approach (Claude's call):**

- Bazel builds `//mediapipe_facade:pose_facade` together with all of MediaPipe into one archive using `cc_static_library`, and `build_mediapipe.sh` outputs `build/libmediapipe_pose.a`.
- The Xcode target links that archive statically.
- **Fallback, if `cc_static_library` isn't workable with the pinned MediaPipe/Bazel versions:** build `libmediapipe_pose.dylib` (`cc_binary`, `linkshared = 1`) and embed it in `TrackingCore.bundle/Contents/Frameworks/` with `@loader_path/../Frameworks` as the rpath. The plan records which one was used.

## 4. Threads and data flow

```
AVCapture delegate queue ("tc.capture", QoS userInteractive)
   └─ on each frame: FrameMailbox.put(pixelBuffer, pts)        // replaces any unconsumed frame → stale count++
                     PreviewTexture.update(pixelBuffer)         // cheap: wraps, no copy

Inference thread ("tc.inference", QoS userInteractive), loop:
   └─ frame = FrameMailbox.take(wait ≤ 100 ms)
      PixelConverter: BGRA (camera res) → RGBA (inference res, default 640×360)
      PoseEngine.detect(rgba, pts_ms)  → up to 3 FacadePerson
      PoseRingBuffer.push(tc_pose_sample{ timestamp = pts seconds, frame_index, people })

Unity main thread:
   └─ tc_read_samples_since(t, out, max) copies every committed sample with timestamp > t
```

- **Running mode:** MediaPipe runs in `VIDEO` mode, synchronously, on our own inference thread. This gives explicit control: the newest frame is always processed, and stale frames are never queued.
- **Timestamps:** the camera presentation timestamp (`CMSampleBufferGetPresentationTimeStamp`) is converted to seconds on the host clock. It is strictly increasing. If a duplicate or backward timestamp occurs, that frame is dropped and counted.
- **Ring buffer:**
  - Capacity is 128 samples (more than 2 s at 60 fps).
  - It is single-producer (inference) and single-consumer (Unity).
  - Each slot has an atomic sequence number (seqlock). The reader validates the sequence before and after copying a slot.
  - A slot overwritten mid-read is skipped and counted in `samples_overwritten`.
- **Memory:** all buffers are allocated at `tc_start`. Nothing is allocated per frame on any thread.

## 5. Camera behavior

- **Discovery:** `AVCaptureDeviceDiscoverySession` with types `builtInWideAngleCamera`, `external` and `continuityCamera`.
- **Selection:** use the `camera_unique_id` from the config if it's present and connected. Otherwise prefer an external camera, then Continuity Camera, then the built-in camera.
- **Format:** the smallest format that is 1280×720 or larger and supports 60 fps; otherwise 30 fps. The pixel format is `kCVPixelFormatType_32BGRA`. The frame rate is set with `activeVideoMin/MaxFrameDuration`.
- **Center Stage:** forced off (`AVCaptureDevice.centerStageControlMode = .app`, `isCenterStageEnabled = false`). Center Stage reframing would break the play-zone mapping.
- **Video effects:** Portrait and Studio Light are controlled by the user in Control Center. Their state is reported through `tc_get_stats` flags, and the operator panel warns when they're on.
- **Late frames:** `alwaysDiscardsLateVideoFrames = true`.
- **Unplug:** the session's runtime-error and disconnect notifications set the status to `TC_STATUS_NO_CAMERA`. PoseInput decides whether to retry.
- **Permission:**
  - `authorizationStatus` `notDetermined`: request access, and stay at `STARTING` until the user answers.
  - `denied` or `restricted`: `TC_STATUS_PERMISSION_DENIED`.

## 6. C API (`TrackingCoreCApi.h`)

```c
#define TC_API_VERSION 1
#define TC_LANDMARKS 33
#define TC_MAX_PEOPLE 3
#define TC_RING_CAPACITY 128

typedef enum {                       // numeric values MUST match Bowling.Contracts.TrackingStatus
  TC_STATUS_STARTING = 0, TC_STATUS_RUNNING = 1, TC_STATUS_NO_CAMERA = 2,
  TC_STATUS_PERMISSION_DENIED = 3, TC_STATUS_MODEL_MISSING = 4, TC_STATUS_FAILED = 5, TC_STATUS_STOPPED = 6
} tc_status;

typedef struct { float x, y, z, visibility, presence; } tc_landmark;           // 20 bytes
typedef struct { tc_landmark image[TC_LANDMARKS]; tc_landmark world[TC_LANDMARKS]; } tc_person;
typedef struct {
  double timestamp;                  // seconds, camera PTS on host clock
  int64_t frame_index;
  int32_t person_count;              // 0..TC_MAX_PEOPLE
  int32_t reserved;
  tc_person people[TC_MAX_PEOPLE];
} tc_pose_sample;

typedef struct {
  const char* model_path;            // absolute path to .task file (UTF-8)
  const char* camera_unique_id;      // NULL = automatic selection (section 5)
  const char* debug_frames_dir;      // NULL in production; PNG sequence for tests
  int32_t max_people;                // 1..TC_MAX_PEOPLE (default 3)
  int32_t target_fps;                // 30 or 60
  int32_t inference_width;           // default 640
  int32_t inference_height;          // default 360
  float min_detection_confidence;    // default 0.5
  float min_presence_confidence;     // default 0.5
  float min_tracking_confidence;     // default 0.5
} tc_config;

typedef struct { char unique_id[128]; char name[128]; int32_t is_continuity; int32_t max_fps; } tc_camera_info;

typedef struct {
  double capture_fps, inference_fps, inference_ms_p50, inference_ms_p95;
  int64_t frames_captured, frames_inferred, frames_dropped_stale, samples_overwritten;
  int32_t video_effects_active;      // 1 if Portrait/Studio Light/etc. detected on the device
  int32_t camera_width, camera_height;
} tc_stats;

int32_t   tc_api_version(void);
int32_t   tc_sizeof_pose_sample(void);   // C# verifies Marshal.SizeOf matches before use
int32_t   tc_list_cameras(tc_camera_info* out, int32_t max);
tc_status tc_start(const tc_config* config);   // non-blocking; restarts if already running
void      tc_stop(void);                       // idempotent; joins threads (≤ 500 ms)
tc_status tc_get_status(void);
int32_t   tc_read_samples_since(double timestamp, tc_pose_sample* out, int32_t max);
void*     tc_get_preview_texture(void);        // id<MTLTexture> BGRA8 at camera res; NULL if none
void      tc_get_stats(tc_stats* out);
const char* tc_last_error(void);               // UTF-8, owned by plugin, valid until next tc_* call
```

**Rules:**

- **Versioning:** any change to this header bumps `TC_API_VERSION`: a new function, a new struct field, or a changed value. The same change updates the version PoseInput expects (pose-input spec section 7). A stale bundle then fails cleanly with `Failed`, instead of `EntryPointNotFoundException`.
- Call every `tc_*` function from one thread, the Unity main thread.
- The plugin owns all memory. The caller supplies output arrays.
- No ObjC object or C++ exception crosses the boundary. Every entry point catches everything and sets `tc_last_error`.
- `tc_start` checks that the model file exists **before** touching the camera. If it doesn't, it returns `TC_STATUS_MODEL_MISSING`.
- **Preview texture:**
  - Created on Unity's Metal device, obtained in `UnityPluginLoad` through `IUnityGraphicsMetal`.
  - The plugin keeps the last 3 `CVMetalTexture` objects alive, so a texture Unity is sampling is never freed underneath it.
  - The pointer is stable between frames whenever the camera resolution hasn't changed.

## 7. Models

- `scripts/fetch_models.sh` downloads `pose_landmarker_full.task` and `pose_landmarker_heavy.task` from the official MediaPipe model storage.
- It verifies them against `scripts/models.lock` (URL plus SHA-256), then places them in `unity/MotionBowling/Assets/StreamingAssets/Models/` (Git LFS).
- This sub-project appends `Assets/StreamingAssets/Models/pose_landmarker_full.task` to `tools/checks/required-files.txt`.
- The default model is `full`. `heavy` is used only if spike S0 shows that `full` fails SC-2 and that `heavy` still meets the latency budget.

## 8. Build and output

- **`build_mediapipe.sh`:**
  - Uses bazelisk.
  - Pins the Bazel version via `.bazelversion`.
  - Builds with `-c opt --cpu=darwin_arm64 --macos_minimum_os=14.0`.
  - Uses the CPU / XNNPACK delegate.
- **`build_bundle.sh`:**
  - Runs `xcodebuild -configuration Release -arch arm64`.
  - Copies `TrackingCore.bundle` to `unity/MotionBowling/Assets/Plugins/macOS/`.
  - Writes `TrackingCore.version.txt`, containing the git SHA, MediaPipe tag and model hashes.
- **Unity plugin import settings:** the `.meta` file is checked in. Platforms: Editor + Standalone macOS. CPU: ARM64.
- **Signing:** the bundle is signed during app signing (platform spec). Builds stay unsigned during local development.

## 9. Tests (XCTest, run by `tools/ci/verify.sh` via `xcodebuild test`)

| Test | Checks |
|---|---|
| `PoseRingBufferTests` | Ordering. `read_since` returns only newer samples. Overwrite behavior. A 10 s producer/consumer stress run with a checksum in `reserved` finds zero torn reads. |
| `FrameMailboxTests` | `put` replaces and counts stale frames. `take` times out cleanly. |
| `CApiLifecycleTests` | `tc_sizeof_pose_sample() == sizeof(tc_pose_sample)`. `tc_start` with a bad model path gives `MODEL_MISSING`. 100 start/stop cycles with `debug_frames_dir` run without leaks (Instruments, manually once per release) or crashes. `tc_stop` without start is a no-op. |
| `PoseEngineFixtureTests` | Over the fixture PNG sequence: at least 1 person each frame; mean shoulder and hip visibility of 0.8 or more; the wrist image positions in frame 0 match the stored expected values within 0.02. |

## 10. Acceptance criteria

1. On the reference hardware with a 60 fps camera, `tc_get_stats` reports inference fps of 30 or more, and `inference_ms_p95` within the S0-measured budget (25 ms or less).
2. Camera PTS to sample-available latency is 35 ms or less at p95. This is measured by a stats field added for development and removed afterward, or by logging `now − timestamp` in PoseInput.
3. All XCTests pass in CI with `debug_frames_dir`, without a camera.
4. Unplugging the camera mid-run gives `TC_STATUS_NO_CAMERA` within 1 s. Replugging and calling `tc_start` again gives `RUNNING`.
5. Denying permission gives `TC_STATUS_PERMISSION_DENIED`. The plugin never crashes.
6. Over a 10-minute run, there are zero allocations on the capture and inference threads after start, verified with the Instruments Allocations tool and recorded in the plan's verification step.

## 11. Decisions made here (Claude's calls, please check)

- `VIDEO` mode on our own thread with a latest-frame mailbox, instead of MediaPipe's `LIVE_STREAM` callback mode.
- Inference input is downscaled to 640×360. Landmarks are normalized, so precision is unaffected. To be revisited after S0.
- The CPU (XNNPACK) delegate. The GPU delegate on desktop macOS is less proven and is a later optimization.
- The facade boundary keeps MediaPipe headers out of Objective-C++.
