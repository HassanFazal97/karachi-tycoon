using System;
using System.Linq;
using KarachiTycoon.Sim;
using UnityEngine;

namespace KarachiTycoon.Game
{
    // Every sound is synthesised at start-up, so there are no audio assets yet: street ambience,
    // the generator, coins in the galla, the stall bell and small UI clicks.
    // Around azaan the street noise drops right down (design doc §19).
    public class AudioDirector : MonoBehaviour
    {
        const int Rate = 44100;
        AudioSource ambient, hum, sfx;
        AudioClip click, coin, bad, bell, chime, dayEnd, fanfare, stamp, drawer;
        AudioClip[] streak;
        Sim.Sim sim;
        float[] prayers;
        float lastCoin, lastBad;

        public void Init(float[] prayerMinutes)
        {
            prayers = prayerMinutes;
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            ambient = Source(Street(), true);
            hum = Source(Hum(), true);
            sfx = Source(null, false);
            click = Tone(0.04f, t => Sine(t, 900) * Decay(t, 60));
            coin = Tone(0.35f, t => (Sine(t, 1568) * 0.6f + Sine(t, 2093) * 0.4f + Sine(t, 4186) * 0.15f) * Decay(t, 9) * (t < 0.06f ? 1 : 0.8f));
            bad = Tone(0.3f, t => Sine(t, t < 0.12f ? 392 : 311) * Decay(t, 8) * 0.6f);
            bell = Tone(1.2f, t => (Sine(t, 1320) + Sine(t, 1320 * 2.76f) * 0.5f + Sine(t, 1320 * 5.4f) * 0.2f) * Decay(t, 3.5f) * 0.5f + Sine(t - 0.25f, 1320) * Decay(t - 0.25f, 3.5f) * 0.4f * Step(t - 0.25f));
            chime = Arpeggio(new[] { 784f, 988, 1175 }, 0.09f, 0.6f);
            dayEnd = Arpeggio(new[] { 523f, 659, 784, 1046 }, 0.12f, 0.9f);
            fanfare = Arpeggio(new[] { 523f, 659, 784, 1046, 784, 1046, 1318 }, 0.13f, 1.4f);
            // A rubber stamp: a dull thump, then a bright two-note "shabash".
            stamp = Tone(0.9f, t => (Sine(t, 70) * Decay(t, 22) * 1.2f + (Sine(t - 0.12f, 880) + Sine(t - 0.12f, 1760) * 0.3f) * Decay(t - 0.12f, 6) * 0.5f * Step(t - 0.12f)
                + Sine(t - 0.24f, 1175) * Decay(t - 0.24f, 5) * 0.45f * Step(t - 0.24f)));
            // The galla drawer for upgrades: a wooden knock and a jingle of coins.
            drawer = Tone(0.8f, t => Sine(t, 110) * Decay(t, 18) + (Sine(t - 0.1f, 2093) + Sine(t - 0.16f, 2637) + Sine(t - 0.22f, 3136)) * Decay(t - 0.1f, 9) * 0.25f * Step(t - 0.1f));
            // Rush streak dings rise every five customers.
            streak = new AudioClip[6];
            for (int i = 0; i < streak.Length; i++) { float f = 660 * Mathf.Pow(1.122f, i * 2); streak[i] = Tone(0.35f, t => (Sine(t, f) + Sine(t, f * 2) * 0.3f) * Decay(t, 9) * 0.6f); }
            UiKit.Clicked += Click;
        }

        void OnDestroy() { UiKit.Clicked -= Click; Detach(); }

        public void Attach(Sim.Sim s)
        {
            Detach();
            sim = s;
            sim.Bubble += OnBubble;
            sim.DayEnded += _ => Play(dayEnd, 0.8f);
            sim.Unlocked += _ => Play(chime, 0.8f);
            sim.ActDone += _ => Play(fanfare, 0.9f);
            sim.Shouted += () => Play(bell, 0.7f);
            sim.GoalDone += _ => Play(stamp, 0.9f);
            sim.UpgradeBought += _ => Play(drawer, 0.8f);
            sim.StreakChanged += n => { if (n >= 5 && n % 5 == 0) Play(streak[Mathf.Min(n / 5 - 1, streak.Length - 1)], 0.6f); };
            ambient.Play(); hum.Play();
        }

        void Detach() { if (sim != null) sim.Bubble -= OnBubble; }

        void OnBubble(BubbleEvent e)
        {
            float now = Time.unscaledTime;
            if (e.Served && e.Mood >= 0 && now - lastCoin > 0.08f) { lastCoin = now; Play(coin, 0.5f); }
            else if (e.Mood < 0 && now - lastBad > 0.6f) { lastBad = now; Play(bad, 0.35f); }
        }

        public void Click() => Play(click, 0.4f);
        public void Play(AudioClip c, float v) { if (c != null) sfx.PlayOneShot(c, v * Settings.SfxVolume); }

        void Update()
        {
            if (sim == null) return;
            var s = sim.S;
            float level = sim.Slot switch { "breakfast" => 0.5f, "day" => 0.6f, "evening" => 0.85f, _ => 0.95f };
            level *= Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(sim.Customers.Count / 25f));
            if (prayers.Any(p => Mathf.Abs(s.Minute - p) < 8)) level *= 0.2f;
            if (s.DayOver) level *= 0.4f;
            ambient.volume = Mathf.MoveTowards(ambient.volume, level * 0.5f * Settings.StreetVolume, Time.unscaledDeltaTime * 0.5f);
            float humLevel = sim.PowerOut && s.Generator ? 0.25f : 0;
            hum.volume = Mathf.MoveTowards(hum.volume, humLevel * Settings.StreetVolume, Time.unscaledDeltaTime);
        }

        // ---------- synthesis ----------
        AudioSource Source(AudioClip c, bool loop)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = c; a.loop = loop; a.playOnAwake = false; a.volume = 0; a.spatialBlend = 0;
            if (!loop) a.volume = 1;
            return a;
        }

        static float Sine(float t, float f) => Mathf.Sin(2 * Mathf.PI * f * t);
        static float Decay(float t, float k) => t < 0 ? 0 : Mathf.Exp(-k * t) * Mathf.Clamp01(t * 400);
        static float Step(float t) => t >= 0 ? 1 : 0;

        static AudioClip Tone(float seconds, Func<float, float> f)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1, 1) * 0.8f;
            var c = AudioClip.Create("tone", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        static AudioClip Arpeggio(float[] notes, float gap, float seconds) => Tone(seconds, t =>
        {
            float v = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                float u = t - i * gap;
                if (u < 0) break;
                v += (Sine(u, notes[i]) + Sine(u, notes[i] * 2) * 0.25f) * Decay(u, 5f) * 0.45f;
            }
            return v;
        });

        // Eight seconds of street: low traffic rumble, crowd murmur and the odd far-off horn. Loops seamlessly.
        static AudioClip Street()
        {
            int n = Rate * 8;
            var data = new float[n];
            var rng = new System.Random(7);
            float brown = 0, murmur = 0;
            for (int i = 0; i < n; i++)
            {
                float white = (float)rng.NextDouble() * 2 - 1;
                brown = Mathf.Clamp(brown + white * 0.02f, -1, 1) * 0.998f;
                murmur += (white - murmur) * 0.08f;
                float t = i / (float)Rate;
                float swell = 0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * t / 8f * 3);
                data[i] = brown * 0.9f + murmur * 0.35f * swell;
            }
            // Two short rickshaw/bus horns.
            foreach (var (at, f) in new[] { (1.7f, 415f), (5.3f, 523f) })
                for (int i = 0; i < Rate * 0.35f; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Clamp01(t * 30) * Mathf.Clamp01((0.35f - t) * 20);
                    float sq = Mathf.Sign(Sine(t, f)) * 0.5f + Sine(t, f * 1.26f) * 0.5f;
                    int j = (int)(at * Rate) + i;
                    data[j] += sq * env * 0.06f;
                }
            // Crossfade the ends so the loop has no click.
            int fade = Rate / 10;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; data[i] = data[i] * k + data[n - fade + i] * (1 - k); }
            var c = AudioClip.Create("street", n - fade, 1, Rate, false);
            c.SetData(data.Take(n - fade).ToArray(), 0);
            return c;
        }

        static AudioClip Hum() => Tone(1f, t => (Sine(t, 100) * 0.6f + Sine(t, 200) * 0.3f + Sine(t, 300) * 0.15f) * 0.5f);
    }
}
