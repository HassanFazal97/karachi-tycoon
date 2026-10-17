using System.Collections.Generic;
using KarachiTycoon.Sim;
using UnityEngine;
using static KarachiTycoon.Game.Util;

namespace KarachiTycoon.Game
{
    // Draws the simulation's customers as simple low-poly people (art style guide §11) with
    // order bubbles and a patience bar. Reads the Sim; never changes it.
    public class PeopleView : MonoBehaviour
    {
        class Person
        {
            public Transform Root;
            public List<Transform> Bodies = new List<Transform>();
            public List<Vector3> Offsets = new List<Vector3>();
            public List<(Transform l, Transform r)> Legs = new List<(Transform, Transform)>();
            public Transform Bar, Fill;
            public Material FillMat;
            public TextMesh Bubble;
            public float BubbleT, Phase;
            public bool OrderBubble;
            public Vector3 Last;
        }

        static readonly string[] Skin = { "#E2B78F", "#CF9C70", "#B98057", "#9C6542", "#7C4D30", "#62391F" };
        static readonly string[] Work = { "#C9B48A", "#8E9497", "#9FB4C4", "#7D8B5E", "#B5A58F", "#6F7A86" };
        static readonly string[] Bright = { "#C8202F", "#E58B2A", "#2B6FA8", "#3E8E5E", "#B03A7A", "#E8C35A" };

        readonly Dictionary<int, Person> people = new Dictionary<int, Person>();
        Sim.Sim sim;
        Camera cam;
        Font font;

        public void Init(Sim.Sim s, Camera c)
        {
            sim = s; cam = c; font = UiFont;
            sim.Bubble += OnBubble;
            sim.Cleared += Clear;
        }

        void OnDestroy() { if (sim != null) { sim.Bubble -= OnBubble; sim.Cleared -= Clear; } }

        Transform MakeBody(Transform parent, string kind, int seed, float scale, out (Transform, Transform) legs)
        {
            var r = new System.Random(seed);
            string P(string[] a) => a[r.Next(a.Length)];
            string skin = P(Skin), top, bottom; bool topi = false;
            switch (kind)
            {
                case "uncle": skin = "#B98057"; top = "#E4DFD3"; bottom = "#E4DFD3"; topi = true; break;
                case "student": top = P(new[] { "#2B3F8F", "#EDE6D6", "#C8202F", "#2F2F2F" }); bottom = "#2E3A4E"; break;
                case "babu": top = P(new[] { "#EDE6D6", "#BFD3E0", "#C9D8C5" }); bottom = "#3B3A40"; break;
                case "woman": top = P(Bright); bottom = P(new[] { "#EDE6D6", "#3B342E" }); break;
                case "child": top = P(Bright); bottom = "#2E3A4E"; break;
                default: top = P(Work); bottom = P(Work); topi = r.NextDouble() < 0.3; break;
            }
            var root = new GameObject(kind).transform; root.SetParent(parent, false);
            root.localScale = Vector3.one * scale;
            var hipL = new GameObject("hipL").transform; hipL.SetParent(root, false); hipL.localPosition = new Vector3(-0.09f, 0.82f, 0);
            var hipR = new GameObject("hipR").transform; hipR.SetParent(root, false); hipR.localPosition = new Vector3(0.09f, 0.82f, 0);
            Box(hipL, new Vector3(0, -0.4f, 0), new Vector3(0.16f, 0.8f, 0.16f), Mat(bottom), "leg");
            Box(hipR, new Vector3(0, -0.4f, 0), new Vector3(0.16f, 0.8f, 0.16f), Mat(bottom), "leg");
            Cyl(root, new Vector3(0, 1.0f, 0), 0.22f, 0.86f, Mat(top), "kameez");
            Ball(root, new Vector3(0, 1.56f, 0), 0.115f, Mat(skin), "head");
            if (topi) Cyl(root, new Vector3(0, 1.66f, 0), 0.11f, 0.08f, Mat("#F7F3EA"), "topi");
            else Ball(root, new Vector3(0, 1.6f, 0.01f), 0.12f, Mat(kind == "uncle" ? "#CFCBC4" : "#1E1A18"), "hair");
            if (kind == "woman") Box(root, new Vector3(0, 1.4f, 0), new Vector3(0.46f, 0.05f, 0.3f), Mat(P(Bright)), "dupatta");
            legs = (hipL, hipR);
            return root;
        }

        Person Ensure(Customer c)
        {
            if (people.TryGetValue(c.Id, out var p)) return p;
            p = new Person { Root = new GameObject("customer_" + c.Id).transform };
            p.Root.SetParent(transform, false);
            if (c.Arch == "family")
            {
                foreach (var (k, ox, oz, sc) in new[] { ("mazdoor", 0f, 0f, 1f), ("woman", 0.5f, 0.15f, 1f), ("child", -0.4f, 0.35f, 0.62f) })
                {
                    var b = MakeBody(p.Root, k, c.Look + (int)(ox * 10), sc, out var legs);
                    b.localPosition = new Vector3(ox, 0, oz); p.Bodies.Add(b); p.Offsets.Add(b.localPosition); p.Legs.Add(legs);
                }
            }
            else
            {
                string kind = c.Arch == "student" && c.Look % 3 == 0 ? "woman" : c.Arch;
                var b = MakeBody(p.Root, kind, c.Look, 1, out var legs);
                p.Bodies.Add(b); p.Offsets.Add(Vector3.zero); p.Legs.Add(legs);
            }
            p.Bar = new GameObject("patience").transform; p.Bar.SetParent(p.Root, false); p.Bar.localPosition = new Vector3(0, 2.05f, 0);
            Box(p.Bar, Vector3.zero, new Vector3(0.62f, 0.09f, 0.01f), Mat("#2A2320"), "bar_bg");
            p.FillMat = Glow("#3E8E5E");
            p.Fill = Box(p.Bar, new Vector3(0, 0, -0.01f), new Vector3(0.58f, 0.06f, 0.01f), p.FillMat, "bar_fill").transform;
            p.Bar.gameObject.SetActive(false);
            p.Last = W(c.X, 0, c.Z);
            people[c.Id] = p;
            return p;
        }

        void OnBubble(BubbleEvent e)
        {
            if (e.Served && e.Mood == 0) return;
            // Crowded queues: only the first few customers announce their order, and neighbours'
            // bubbles sit at different heights so the words never pile up on each other.
            int place = sim.Queue().IndexOf(e.C);
            if (e.Text == null && !e.Served && place >= 1) return;
            var p = Ensure(e.C);
            if (p.Bubble == null)
            {
                var go = new GameObject("bubble"); go.transform.SetParent(p.Root, false); go.transform.localPosition = new Vector3(0, 2.6f, 0);
                p.Bubble = go.AddComponent<TextMesh>();
                p.Bubble.anchor = TextAnchor.LowerCenter; p.Bubble.alignment = TextAlignment.Center;
                p.Bubble.fontSize = 64; p.Bubble.characterSize = 0.05f; p.Bubble.fontStyle = FontStyle.Bold;
                if (font != null) { p.Bubble.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            }
            p.Bubble.text = e.Text ?? (e.Served ? (e.Mood > 0 ? "Wah!" : "Hmm...") : sim.Dish[e.Dish].En);
            p.Bubble.color = e.Mood > 0 ? Hex("#F2D24A") : e.Mood < 0 ? Hex("#FF8A7A") : Color.white;
            p.Bubble.transform.localPosition = new Vector3(0, 2.6f + (place >= 0 ? place % 3 : e.C.Id % 3) * 0.42f, 0);
            p.Bubble.gameObject.SetActive(true);
            // Orders flash briefly; complaints and reactions stay a little longer.
            p.BubbleT = e.Text != null ? 2.6f : e.Served ? 1.6f : 1.1f;
            // Only one order bubble at a time: a new order clears the previous one.
            if (e.Text == null && !e.Served)
                foreach (var o in people.Values)
                    if (o != p && o.Bubble != null && o.Bubble.gameObject.activeSelf && o.OrderBubble) o.Bubble.gameObject.SetActive(false);
            p.OrderBubble = e.Text == null && !e.Served;
        }

        float[] NearestTable(float x, float z)
        {
            float[] best = null; float bd = 1e9f;
            foreach (var t in sim.D.District.Tables) { float dd = (t[0] - x) * (t[0] - x) + (t[1] - z) * (t[1] - z); if (dd < bd) { bd = dd; best = t; } }
            return bd < 4 ? best : new[] { x, z + 1 };
        }

        void Clear()
        {
            foreach (var p in people.Values) Destroy(p.Root.gameObject);
            people.Clear();
        }

        void Update()
        {
            if (sim?.S == null) return;
            var seen = new HashSet<int>();
            float dt = Time.deltaTime;
            foreach (var c in sim.Customers)
            {
                seen.Add(c.Id);
                var p = Ensure(c);
                var pos = W(c.X, c.Z < -2.7f ? 0.16f : 0, c.Z);
                float moved = Vector3.Distance(new Vector3(pos.x, 0, pos.z), new Vector3(p.Last.x, 0, p.Last.z));
                var dir = pos - p.Last; dir.y = 0;
                p.Root.position = pos;
                if (moved > 0.0005f) p.Root.rotation = Quaternion.LookRotation(dir.normalized);
                else if (c.State == "queue" || c.State == "rival") p.Root.rotation = Quaternion.LookRotation(Vector3.forward); // face the stall
                p.Last = pos;
                bool eating = c.State == "eating" && c.Seats != null;
                for (int i = 0; i < p.Bodies.Count; i++)
                {
                    var (l, r) = p.Legs[i];
                    if (eating)
                    {
                        var st = sim.D.District.Seats[c.Seats[Mathf.Min(i, c.Seats.Length - 1)]];
                        p.Bodies[i].position = W(st[0], (st[1] < -2.7f ? 0.16f : 0) - 0.36f * p.Bodies[i].localScale.y, st[1]);
                        l.localRotation = r.localRotation = Quaternion.Euler(-85, 0, 0);
                        var table = NearestTable(st[0], st[1]);
                        var look = W(table[0], 0, table[1]) - W(st[0], 0, st[1]);
                        if (look.sqrMagnitude > 0.001f) p.Bodies[i].rotation = Quaternion.LookRotation(look.normalized);
                    }
                    else
                    {
                        p.Bodies[i].localPosition = p.Offsets[i]; p.Bodies[i].localRotation = Quaternion.identity;
                        if (moved > 0.0005f) p.Phase += moved * 4.2f;
                        float a = moved > 0.0005f ? Mathf.Sin(p.Phase) * 32 : 0;
                        l.localRotation = Quaternion.Euler(a, 0, 0); r.localRotation = Quaternion.Euler(-a, 0, 0);
                    }
                }
                // Patience while waiting; cooking progress (gold) once the order is on the tawa.
                bool show = c.State == "queue";
                p.Bar.gameObject.SetActive(show);
                if (show)
                {
                    p.Bar.rotation = cam.transform.rotation;
                    float f; Color col;
                    if (c.Started) { f = 1 - Mathf.Max(0, c.Work) / c.WorkTotal; col = Hex("#D8A93A"); }
                    else { f = Mathf.Max(0, 1 - c.Waited / sim.PatienceOf(c)); col = Hex(f > 0.5f ? "#3E8E5E" : f > 0.25f ? "#E58B2A" : "#C8202F"); }
                    p.Fill.localScale = new Vector3(0.58f * Mathf.Max(0.01f, f), 0.06f, 0.01f);
                    p.Fill.localPosition = new Vector3(-0.29f * (1 - f), 0, -0.01f);
                    SetGlow(p.FillMat, col, 1.2f);
                }
                if (p.Bubble != null && p.Bubble.gameObject.activeSelf)
                {
                    p.Bubble.transform.rotation = cam.transform.rotation;
                    p.BubbleT -= dt;
                    if (p.BubbleT <= 0) p.Bubble.gameObject.SetActive(false);
                }
            }
            var gone = new List<int>();
            foreach (var kv in people) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var id in gone) { Destroy(people[id].Root.gameObject); people.Remove(id); }
        }
    }
}
