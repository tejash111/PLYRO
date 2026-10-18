using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// Installs the project's pinned package set and removes the default packages the project doesn't use
    /// (foundation spec, section 3). Run it headless with
    /// <c>tools/ci/unity.sh exec Bowling.Tooling.Editor.PackageInstaller.InstallBatch</c>.
    /// </summary>
    /// <remarks>
    /// Resolving packages recompiles and reloads the scripts, and a reload drops every
    /// <see cref="EditorApplication.update"/> callback and every static field. So the install progress
    /// lives in <see cref="SessionState"/> (it survives reloads within one editor session), and the static
    /// constructor re-attaches the poll after a reload. The poll judges the outcome by the package state,
    /// not by the request object, which a reload loses. It ends the editor with
    /// <see cref="EditorApplication.Exit"/>. Outside a batch install the static constructor does nothing.
    /// </remarks>
    [InitializeOnLoad]
    public static class PackageInstaller
    {
        public static readonly string[] Required =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.inputsystem",
            "com.unity.cinemachine",
            "com.unity.test-framework",
            "com.unity.test-framework.performance",
            "com.unity.nuget.newtonsoft-json",
        };

        public static readonly string[] Removed =
        {
            "com.unity.visualscripting",
            "com.unity.collab-proxy",
            "com.unity.timeline",
            "com.unity.ugui",
            "com.unity.multiplayer.center",
            "com.unity.ai.navigation",
        };

        const string StateKey = "Bowling.Tooling.PackageInstaller.State";
        const string StartKey = "Bowling.Tooling.PackageInstaller.StartTime";
        const string StateRequested = "requested";
        const string StateApplied = "applied";
        const double SettleSeconds = 5;
        const double TimeoutSeconds = 600;
        const double ProgressLogSeconds = 30;

        static AddAndRemoveRequest _request;
        static double _quietSince;
        static double _lastProgressLog;

        static PackageInstaller()
        {
            if (!Application.isBatchMode) return;
            string state = SessionState.GetString(StateKey, "");
            if (state != StateRequested && state != StateApplied) return;
            Log($"scripts reloaded while installing (state '{state}'); resuming the wait");
            Attach();
        }

        /// <summary>Batch entry point. Ends the editor with exit code 0 on success and 1 on failure.</summary>
        public static void InstallBatch()
        {
            HashSet<string> direct = DirectDependencies();
            string[] toRemove = Removed.Where(direct.Contains).ToArray();
            SessionState.SetString(StateKey, StateRequested);
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            Log($"adding [{string.Join(", ", Required)}]; removing [{string.Join(", ", toRemove)}]");
            _request = Client.AddAndRemove(Required, toRemove);
            Attach();
        }

        static void Attach()
        {
            _quietSince = EditorApplication.timeSinceStartup;
            _lastProgressLog = _quietSince;
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - SessionState.GetFloat(StartKey, (float)now) > TimeoutSeconds)
            {
                Finish(1, $"timed out after {TimeoutSeconds} s. {Describe(Verify())}");
                return;
            }
            if (_request != null)
            {
                if (!_request.IsCompleted) return;
                if (_request.Status != StatusCode.Success)
                {
                    Finish(1, $"AddAndRemove failed: {_request.Error?.message}");
                    return;
                }
                Log("AddAndRemove completed; waiting for the import and compile to settle");
                _request = null;
                SessionState.SetString(StateKey, StateApplied);
                _quietSince = now;
                return;
            }
            // No request in this domain: it completed (state "applied") or a reload lost it (state "requested").
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _quietSince = now;
                return;
            }
            if (now - _quietSince < SettleSeconds) return;
            List<string> problems = Verify();
            if (problems.Count == 0)
            {
                Finish(0, $"package set is in place: {Describe(Versions())}");
                return;
            }
            if (now - _lastProgressLog < ProgressLogSeconds) return;
            _lastProgressLog = now;
            Log($"still waiting. {Describe(problems)}");
        }

        static void Finish(int code, string message)
        {
            EditorApplication.update -= Poll;
            SessionState.EraseString(StateKey);
            if (code == 0) Log(message);
            else Debug.LogError($"[PackageInstaller] {message}");
            EditorApplication.Exit(code);
        }

        static HashSet<string> DirectDependencies()
        {
            return new HashSet<string>(
                PackageInfo.GetAllRegisteredPackages().Where(p => p.isDirectDependency).Select(p => p.name));
        }

        static List<string> Verify()
        {
            HashSet<string> direct = DirectDependencies();
            var problems = new List<string>();
            problems.AddRange(Required.Where(n => !direct.Contains(n)).Select(n => "missing " + n));
            problems.AddRange(Removed.Where(direct.Contains).Select(n => "still installed " + n));
            return problems;
        }

        static List<string> Versions()
        {
            Dictionary<string, string> installed = PackageInfo.GetAllRegisteredPackages()
                .ToDictionary(p => p.name, p => p.version);
            return Required.Select(n => $"{n}@{(installed.TryGetValue(n, out string v) ? v : "?")}").ToList();
        }

        static string Describe(IEnumerable<string> items) => string.Join(", ", items);

        static void Log(string message) => Debug.Log($"[PackageInstaller] {message}");
    }
}
