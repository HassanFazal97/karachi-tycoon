using System.Collections.Generic;
using UnityEngine;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // The life around the stall (art style guide rule 5, and "particles" in the upgrade plan):
    // kites and crows over the rooftops by day, steam off the kettle and the nihari deg,
    // charcoal smoke and sparks from the grills after dark.
    public class Ambient : MonoBehaviour
    {
        Sim.Sim sim;
        StreetBuilder street;
        readonly List<(Transform t, Vector3 home, float phase, float speed)> kites = new List<(Transform, Vector3, float, float)>();
        readonly List<(Transform t, Transform wingL, Transform wingR, Vector3 centre, float radius, float phase, float speed)> crows = new List<(Transform, Transform, Transform, Vector3, float, float, float)>();
        ParticleSystem chaiSteam, degSteam;
        readonly List<(ParticleSystem smoke, ParticleSystem sparks)> grills = new List<(ParticleSystem, ParticleSystem)>();
        Material particle;

        public void Init(Sim.Sim s, StreetBuilder st)
        {
            sim = s; street = st;
            var r = new System.Random(5);
            float F() => (float)r.NextDouble();
            string[] kiteCols = { "#C8202F", "#E8C35A", "#2B6FA8", "#3E8E5E", "#B03A7A", "#EDE6D6" };
            for (int i = 0; i < 6; i++)
            {
                var k = new GameObject("kite").transform; k.SetParent(transform, false);
                var body = Box(k, Vector3.zero, new Vector3(0.9f, 0.9f, 0.02f), Mat(kiteCols[i]), "kite_paper");
                body.transform.localRotation = Quaternion.Euler(0, 0, 45);
                Box(k, new Vector3(0, -0.85f, 0), new Vector3(0.04f, 0.9f, 0.01f), Mat(kiteCols[(i + 2) % kiteCols.Length]), "kite_tail");
                var home = W(-40 + i * 15 + F() * 6, 20 + F() * 10, -30 - F() * 15);
                kites.Add((k, home, F() * 10, 0.4f + F() * 0.5f));
            }
            for (int i = 0; i < 5; i++)
            {
                var c = new GameObject("crow").transform; c.SetParent(transform, false);
                var black = Mat("#1A1816");
                Box(c, Vector3.zero, new Vector3(0.18f, 0.12f, 0.5f), black, "crow_body");
                var wl = new GameObject("wingL").transform; wl.SetParent(c, false);
                var wr = new GameObject("wingR").transform; wr.SetParent(c, false);
                Box(wl, new Vector3(-0.35f, 0, 0), new Vector3(0.7f, 0.03f, 0.3f), black, "wing");
                Box(wr, new Vector3(0.35f, 0, 0), new Vector3(0.7f, 0.03f, 0.3f), black, "wing");
                crows.Add((c, wl, wr, W(-20 + F() * 40, 14 + F() * 6, -14 - F() * 10), 6 + F() * 8, F() * 6, 0.25f + F() * 0.2f));
            }

            particle = Resources.Load<Material>("Materials/KT_Particle");
            if (particle != null)
            {
                particle = new Material(particle);
                foreach (var prop in new[] { "_BaseMap", "_MainTex" }) if (particle.HasProperty(prop)) particle.SetTexture(prop, Paint.Get("soft"));
            }
            if (street.Items.TryGetValue("chai", out var chai)) chaiSteam = Fx(chai.transform, new Vector3(0, 0.55f, 0), new Color(1, 1, 1, 0.35f), 5, 0.35f, 0.5f, 2.2f);
            if (street.Items.TryGetValue("nihari", out var deg)) degSteam = Fx(deg.transform, new Vector3(0, 0.85f, 0), new Color(1, 1, 1, 0.3f), 7, 0.6f, 0.45f, 2.8f);
            foreach (var g in street.Grills)
            {
                var smoke = Fx(g, new Vector3(0, 1.0f, 0), new Color(0.55f, 0.52f, 0.5f, 0.28f), 6, 0.8f, 0.6f, 3.2f);
                var sparks = Fx(g, new Vector3(0, 0.95f, 0), new Color(4f, 1.6f, 0.4f, 1f), 10, 0.06f, 1.6f, 0.7f);
                grills.Add((smoke, sparks));
            }
        }

        ParticleSystem Fx(Transform parent, Vector3 pos, Color c, float rate, float size, float speed, float life)
        {
            if (particle == null) return null;
            var go = new GameObject("fx"); go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.rotation = Quaternion.Euler(-90, 0, 0);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = life; main.startSpeed = speed; main.startSize = size; main.startColor = c;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 10; sh.radius = 0.06f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.5f, 1, 1.8f));
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.6f;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = particle;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        static void Run(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            if (on && !ps.isEmitting) ps.Play();
            else if (!on && ps.isEmitting) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void Update()
        {
            if (sim?.S == null) return;
            float m = sim.S.Minute, t = Time.time;
            bool day = m > 400 && m < 1110, night = m > 1080;
            foreach (var (k, home, phase, speed) in kites)
            {
                k.gameObject.SetActive(day);
                k.position = home + new Vector3(Mathf.Sin(t * speed + phase) * 2.5f, Mathf.Sin(t * speed * 1.7f + phase) * 1.2f, 0);
                k.rotation = Quaternion.Euler(0, 0, Mathf.Sin(t * speed * 2.3f + phase) * 14);
            }
            foreach (var (c, wl, wr, centre, radius, phase, speed) in crows)
            {
                c.gameObject.SetActive(m > 380 && m < 1120);
                float a = t * speed + phase;
                c.position = centre + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a * 2) * 0.6f, Mathf.Sin(a) * radius * 0.4f);
                c.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a) * 0.4f));
                float flap = Mathf.Sin(t * 9 + phase) * 30;
                wl.localRotation = Quaternion.Euler(0, 0, flap); wr.localRotation = Quaternion.Euler(0, 0, -flap);
            }
            bool chaiOn = sim.IsAvailable("chai") && !sim.S.DayOver;
            Run(chaiSteam, chaiOn);
            Run(degSteam, sim.AtLeast("dhaba") && !sim.S.DayOver);
            foreach (var (smoke, sparks) in grills) { Run(smoke, m > 960); Run(sparks, night); }
        }
    }
}
