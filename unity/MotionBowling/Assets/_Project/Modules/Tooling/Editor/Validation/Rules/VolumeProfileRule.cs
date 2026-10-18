using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Bowling.Tooling.Editor
{
    /// <summary>
    /// G-4 (foundation spec, section 8.1): no volume profile holds an active Vignette, ScreenSpaceLensFlare,
    /// FilmGrain or Bloom whose intensity is above 0. It scans every <c>VolumeProfile</c> asset under
    /// <see cref="ValidationContext.AssetSearchRoot"/>, except vendor folders. Unity 6's default profile carries
    /// these overrides at zero intensity, so a zero-intensity override passes.
    /// <para>
    /// The intensity that counts is the one the volume system applies: a parameter that is not overridden is
    /// skipped when profiles are blended (<c>VolumeComponent.Override</c>), so it leaves the component's default,
    /// 0, however the profile file stores it.
    /// </para>
    /// </summary>
    public sealed class VolumeProfileRule : IValidationRule
    {
        public string Id => "G-4";

        public IEnumerable<Violation> Run(ValidationContext context)
        {
            var violations = new List<Violation>();
            string root = ScanScope.SearchRoot(context);
            if (!AssetDatabase.IsValidFolder(root))
            {
                violations.Add(Fail(root, "search folder not found"));
                return violations;
            }

            foreach (string path in ScanScope.AssetPaths("t:VolumeProfile", root).Where(p => !ScanScope.IsVendor(p)))
            {
                foreach (VolumeProfile profile in AssetDatabase.LoadAllAssetsAtPath(path).OfType<VolumeProfile>())
                {
                    foreach (VolumeComponent component in profile.components)
                    {
                        string problem = Problem(component);
                        if (problem != null) violations.Add(Fail(path, problem));
                    }
                }
            }
            return violations;
        }

        Violation Fail(string path, string message) => new Violation(Id, path, 0, message, false);

        static string Problem(VolumeComponent component)
        {
            if (component == null || !component.active) return null;
            switch (component)
            {
                case Vignette vignette: return Judge("Vignette", vignette.intensity);
                case ScreenSpaceLensFlare flare: return Judge("ScreenSpaceLensFlare", flare.intensity);
                case FilmGrain grain: return Judge("FilmGrain", grain.intensity);
                case Bloom bloom: return Judge("Bloom", bloom.intensity);
                default: return null;
            }
        }

        static string Judge(string name, VolumeParameter<float> intensity)
        {
            if (!intensity.overrideState || intensity.value <= 0f) return null;
            return $"{name} is active with intensity {intensity.value.ToString(CultureInfo.InvariantCulture)}"
                + " (set the intensity to 0 or remove the override)";
        }
    }
}
