# Sub-project 9: Platform and Release

- **Status:** Approved by Tejash on 2026-10-18
- **Date:** 2026-10-18
- **Parent:** `2026-10-18-motion-bowling-system-design.md`
- **Depends on:** 1 (foundation). The runtime parts can start early. Release work finishes last, once everything is integrated.
- **Modules:** `Bowling.Platform` (runtime), `Bowling.App.Editor` (build script), plus `native/MacPlatform` and `tools/build`, `tools/kiosk`

## 1. Purpose

This sub-project makes the game run as a dependable macOS product:

- local storage;
- logging;
- operator controls;
- kiosk mode;
- keeping the display awake;
- auto-relaunch;
- a signed, notarized, stapled `.dmg` that any Mac (macOS 14 or newer) opens without warnings.

**Implements:** `ISettingsStore`, `ILeaderboardStore`, `IGameLog` and `IOperatorActions` (Contracts 6.6).

## 2. Runtime components (`Bowling.Platform`)

```
Modules/Platform/Runtime/
├─ Components/
│  ├─ PlatformLifecycle.cs        OnApplicationQuit → flush stores/logs; Application.logMessageReceived → IGameLog for errors
│  ├─ KioskController.cs          enables kiosk when launched with -kiosk (section 4)
│  └─ OperatorShortcut.cs         Input System: Ctrl+Option+Cmd+O toggles the operator panel (via App callback); F1 too in BOWLING_DEV
├─ Core/
│  ├─ JsonSettingsStore.cs        ISettingsStore
│  ├─ JsonLeaderboardStore.cs     ILeaderboardStore
│  ├─ AtomicFile.cs               write tmp → flush → File.Replace; corrupt-file quarantine
│  ├─ FileGameLog.cs              IGameLog, rotating files
│  ├─ OperatorActions.cs          IOperatorActions
│  ├─ LaunchArguments.cs          parses -kiosk, -tracking=<native|recorded:<path>|debug>, -soak
│  └─ Native/
│     ├─ IMacPlatformNative.cs    interface over the native bundle (fakeable)
│     └─ MacPlatformNative.cs     [DllImport("MacPlatform")] implementation
└─ Config/
   └─ PlatformTuning.cs           log sizes, retention, leaderboard pruning limits
```

## 3. Storage

All files live in `Application.persistentDataPath`, which on macOS is `~/Library/Application Support/Shava Studios/Motion Bowling/`.

| File | Content | Rules |
|---|---|---|
| `settings.json` | `OperatorSettings` (`SchemaVersion` 1) | Missing → defaults. Corrupt → quarantine and use defaults. Saved on `ApplySettings`. |
| `leaderboard.json` | `{ "schemaVersion": 1, "entries": [LeaderboardEntry…] }` | `Submit` writes immediately. Prune on submit: keep all of today's entries, plus the top 100 per frame count across all time. |
| `logs/motion-bowling-<n>.log` | Plain text, one line per entry: `ISO-8601 UTC \| level \| category \| message` | Rotation: 5 files × 5 MB. Errors also include the exception and stack. |
| `recordings/` | Pose recordings (pose-input spec) | Only while recording is on |

**Rules:**

- **Atomic writes (`AtomicFile`):**
  1. Write `<name>.tmp`.
  2. Flush.
  3. `File.Replace(tmp, target, backup: null)`. If the target is missing, use `File.Move`.
- **Corrupt files:** a file that fails JSON parsing or schema checks is renamed to `<name>.corrupt-<yyyyMMddHHmmss>`, a warning is logged, and the defaults are recreated.
- **"Today":** the operator's local calendar day. `PlayedAtUtc` is converted with the system time zone. Tests pin the time zone.
- **Privacy:** no camera images and no landmarks are ever logged. Display names are only "Player N" (system design D-10).

## 4. Kiosk and power (`native/MacPlatform`)

```
native/MacPlatform/
├─ MacPlatform.xcodeproj           target MacPlatform.bundle (arm64, macOS 14.0)
├─ Sources/CApi/MacPlatformCApi.h  contract below
├─ Sources/CApi/MacPlatformCApi.mm
├─ Sources/Kiosk/KioskMode.h/.mm   NSApplication presentation options
├─ Sources/Power/DisplayAwake.h/.mm IOPMAssertion + NSProcessInfo activity
└─ Tests/                           XCTest: API version, idempotent enable/disable
```

```c
#define MP_API_VERSION 1
int32_t mp_api_version(void);
void    mp_set_kiosk(int32_t enabled);          // presentationOptions: HideDock | HideMenuBar | DisableProcessSwitching |
                                                //   DisableForceQuit | DisableSessionTermination | DisableHideApplication
void    mp_set_cursor_hidden(int32_t hidden);
int32_t mp_begin_display_awake(const char* reason); // IOPMAssertionCreateWithName(kIOPMAssertionTypeNoDisplaySleep)
                                                    //  + NSProcessInfo beginActivity(UserInitiated | IdleDisplaySleepDisabled)
void    mp_end_display_awake(void);
```

**`KioskController`:**

- **With `-kiosk`:** at startup it calls `mp_set_kiosk(1)`, hides the cursor, starts display-awake and forces fullscreen.
- **Without `-kiosk`:** none of that happens, so it's a normal fullscreen-window app for development or home use.
- **Leaving kiosk mode:** only through the operator panel's Quit, which calls `mp_set_kiosk(0)` and `mp_end_display_awake()` before exiting.

## 5. Booth deployment (`tools/kiosk/`)

- **launchd agent:** `ai.shavastudios.motionbowling.plist` is a template:
  - `ProgramArguments` = [`/Applications/Motion Bowling.app/Contents/MacOS/Motion Bowling`, `-kiosk`].
  - `RunAtLoad` true.
  - `KeepAlive` = { `SuccessfulExit`: false }, so it relaunches after a crash but not after the operator quits cleanly.
  - `ThrottleInterval` 5, which satisfies SC-6's "relaunch in 10 s or less".
  - Stdout and stderr go to `~/Library/Logs/MotionBowling/`.
- **`install-launch-agent.sh`:** copies the plist to `~/Library/LaunchAgents/` and runs `launchctl bootstrap gui/$UID …`.
- **`uninstall-launch-agent.sh`:** reverses the install.
- **`docs/booth-setup.md`** (written in this sub-project) is the operator's checklist:
  - Mac mini plus TV plus camera at about 1.0 m height, centered under the screen.
  - A floor box taped 2.5–3.5 m from the camera, matching PoseInput's zone (the middle 40% of the camera's view). Verify it with the attract-screen zone guide.
  - Front lighting; no window behind the player.
  - Auto-login user. Screen saver and sleep off in System Settings. A Focus mode that silences notifications. Automatic OS restarts off.
  - Grant camera permission once at first launch. Turn off Center Stage, Portrait and Studio Light; the operator panel warns if any are on.
  - Install the launch agent.

## 6. Build, sign, notarize (`tools/build/`)

**Inputs:** a clean checkout with LFS assets, Xcode 16 or newer, the Developer ID Application certificate in the keychain, and the `NOTARY_PROFILE` environment variable naming an `xcrun notarytool store-credentials` profile. Secrets are never stored in the repo.

| Step | Script | Does |
|---|---|---|
| 1 | `build_native.sh` | Runs `native/TrackingCore/scripts/build_bundle.sh` and the MacPlatform equivalent (Release, arm64). Copies the bundles into `Assets/Plugins/macOS/`. |
| 2 | `build_unity.sh` | Unity batch mode `-executeMethod Bowling.App.Editor.BuildScript.BuildMacRelease`. Builds `build/Motion Bowling.app` directly (no Xcode project): IL2CPP, Apple Silicon, no `BOWLING_DEV` define, version from `--version` and `--build-number` arguments. |
| 3 | `sign_app.sh` | Signs inside-out with hardened runtime and a secure timestamp: every `.bundle` and `.dylib` under `Contents/PlugIns` and `Contents/Frameworks`, then `GameAssembly.dylib` and `UnityPlayer.dylib`, then the app with `tools/build/MotionBowling.entitlements`. Never uses `--deep` for signing. |
| 4 | `notarize.sh` | `ditto -c -k --keepParent` → `xcrun notarytool submit --keychain-profile "$NOTARY_PROFILE" --wait` → `xcrun stapler staple` on the app |
| 5 | `make_dmg.sh` | `hdiutil create` (UDZO) containing the app plus an `/Applications` symlink → sign the DMG → notarize → staple |
| 6 | `verify_release.sh` | `codesign --verify --strict --verbose=2`; `spctl --assess --type execute -vv` must say "accepted, source=Notarized Developer ID"; `stapler validate` on the app and the DMG |

**Entitlements (`MotionBowling.entitlements`):**

- `com.apple.security.device.camera` = true.
- No other entitlement is added unless a notarized build fails at runtime. In that case, add the single entitlement that fixes it, with the reason recorded in the plan.
- App Sandbox is **off**. This is Developer ID distribution, and kiosk presentation options plus the launchd deployment aren't Mac App Store goals.

**Info.plist** (Unity Player Settings plus a post-build step in `BuildScript`):

| Key | Value |
|---|---|
| `NSCameraUsageDescription` | Foundation string |
| `LSMinimumSystemVersion` | 14.0 |
| `LSApplicationCategoryType` | `public.app-category.sports-games` |
| `CFBundleShortVersionString` | Semver |
| `CFBundleVersion` | CI build number |

## 7. Dev vs. release builds

| | Dev (`BOWLING_DEV`) | Release |
|---|---|---|
| `DebugThrowSource`, `ThrowDebugOverlay` | Available | Compiled out |
| `-tracking=recorded:<path>` / `-tracking=debug` | Honoured | Ignored (native only) |
| F1 operator shortcut | Yes | No (chord only) |
| `-soak` | Honoured | Honoured (used for booth acceptance) |

**Soak mode (`-soak`):**

- App wires `RecordedTrackingSource` in loop mode over `StreamingAssets/Soak/*.pose.json.gz`.
- Those soak recordings are copied in by `build_unity.sh` only when `--with-soak` is passed.
- The game plays itself continuously, which exercises every state.

## 8. Tests

- **EditMode:**
  - `AtomicFile`: the replace path; crash simulation (a leftover `.tmp` is ignored on load); corrupt-file quarantine.
  - `JsonLeaderboardStore`: Top ordering with ties broken by earlier date; the Today and AllTime scopes under a pinned time zone; pruning.
  - `JsonSettingsStore`: defaults, a round trip, schema-version handling.
  - `FileGameLog`: format, rotation at 5 MB, keeping 5 files.
  - `LaunchArguments`: every form.
  - `OperatorActions`: `ApplySettings` saves and raises `SettingsApplied`; `ResetLeaderboard`; `Quit` calls the injected quit delegate.
  - `KioskController` with a fake `IMacPlatformNative`: the call order for `-kiosk` and for Quit.
- **Native XCTest** (MacPlatform): the API version; enabling and disabling twice is idempotent.
- **Release verification:** `verify_release.sh` passes on the produced DMG, run in the release CI job.
- **Soak (manual, on booth hardware, before each release):**
  - 10 hours in `-soak -kiosk`.
  - Pass if there are no crashes, memory grows less than 10% from hour 1 to hour 10, and p99 frame time stays at 16.7 ms or less.
  - Killing the process with `kill -9` relaunches it in 10 s or less (SC-6).

## 9. Acceptance criteria

1. All tests pass.
2. A fresh Mac (macOS 14 or newer) mounts the DMG, drags the app to Applications and launches it with no Gatekeeper warning. The camera prompt shows the usage string.
3. The soak test passes.
4. With the launch agent installed, a reboot boots straight into the attract screen with no clicks.
5. Operator Quit exits cleanly and launchd does **not** relaunch.

## 10. Decisions made here (Claude's calls, please check)

- **Developer ID distribution with no sandbox.** The Mac App Store is a non-goal.
- **Unity builds the `.app` directly, and scripts sign it inside-out.** This replaces the "Xcode archive" idea from the chat discussion: it's simpler to automate and handles nested plugins explicitly.
- **A launchd agent instead of `SMAppService`,** because operators install it per booth and it gives `KeepAlive` crash relaunch for free.
- **A separate `MacPlatform.bundle`** keeps kiosk and power code out of TrackingCore (SOT-2: one responsibility per plugin).
