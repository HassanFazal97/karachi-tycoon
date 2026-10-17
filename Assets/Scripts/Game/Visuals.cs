using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KarachiTycoon.Game
{
    // Post-processing for "painted realism" (art style guide): bloom on the practical lights
    // (tube lights, sodium lamps, charcoal), a neutral film curve, a touch of warmth and a soft vignette.
    // "Battery saver" in Settings turns post-processing and shadows off for slower phones.
    public class Visuals : MonoBehaviour
    {
        Volume volume;
        Camera cam;

        public void Init(Camera camera)
        {
            cam = camera;
            if (GraphicsSettings.currentRenderPipeline == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            volume = new GameObject("Post").AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = p;
            var bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.95f); bloom.intensity.Override(0.85f); bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.93f, 0.84f)); bloom.highQualityFiltering.Override(false);
            p.Add<Tonemapping>(true).mode.Override(TonemappingMode.Neutral);
            var grade = p.Add<ColorAdjustments>(true);
            grade.postExposure.Override(0.2f); grade.contrast.Override(8f); grade.saturation.Override(6f);
            var wb = p.Add<WhiteBalance>(true);
            wb.temperature.Override(6f);
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.22f); vig.smoothness.Override(0.5f);
            Apply();
            Settings.Changed += Apply;
        }

        void OnDestroy() => Settings.Changed -= Apply;

        void Apply()
        {
            if (volume == null) return;
            bool high = !Settings.BatterySaver;
            volume.enabled = high;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = high;
            foreach (var l in FindObjectsByType<Light>())
                if (l.type == LightType.Directional) l.shadows = high ? LightShadows.Soft : LightShadows.None;
        }
    }
}
