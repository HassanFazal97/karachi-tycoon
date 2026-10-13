// Karachi Tycoon simulation, ported from the browser prototype (prototype/js/sim.js).
// Pure logic, no rendering. Time is in game minutes from midnight; positions are metres along
// Burns Road (x) and across it (z).
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace KarachiTycoon.Sim
{
    public class OrderItem { public string Dish; public int Qty; }

    public class Customer
    {
        public int Id, Party, Look;
        public string Arch, State = "walk", Dest = "pass", Rival;
        public float X, Z, Tx, Tz, Waited, Work, WorkTotal, Timer, Sat;
        public bool Near, Uncle, Taken, Started, Gone;
        public List<OrderItem> Order;
        public float OrderWeight;
        public int[] Seats;
    }

    public class Cook { public Customer Order; }

    public class BubbleEvent { public Customer C; public string Text, Dish, Key; public int Mood; public bool Served; }

    public class Notice { public string Kind, Key, Who, Text, Dish, Ing; public int Level; }

    public class Outage { public float From, To; }

    public class Sim
    {
        public static readonly string[] Weekdays = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        static readonly (string id, float to)[] SlotEnds = { ("breakfast", 600), ("day", 960), ("evening", 1110), ("night", 1440) };
        public static string SlotAt(float m) { foreach (var s in SlotEnds) if (m < s.to) return s.id; return "night"; }
        static float Clamp(float v, float a, float b) => Math.Max(a, Math.Min(b, v));
        const float Edge = 31f;

        public readonly GameData D;
        public GameState S;
        public Ledger Today;
        public List<Customer> Customers = new();
        public List<Customer> Pending = new();
        public List<Cook> Cooks = new();
        public List<Outage> Outages = new();
        public string Snapshot;

        public readonly Dictionary<string, DishDef> Dish;
        public readonly Dictionary<string, IngredientDef> Ing;
        public readonly Dictionary<string, ArchetypeDef> Arch;
        public readonly Dictionary<string, StaffDef> StaffDefs;

        readonly Func<double> rand;
        int nextId = 1;
        List<float> uncleTimes = new();
        bool wasOut;
        int[] seatTaken = new int[0];
        HashSet<string> warned = new();

        public event Action<Customer> Spawned, Despawned;
        public event Action<BubbleEvent> Bubble;
        public event Action<Notice> Noticed;
        public event Action<string> Unlocked;
        public event Action<int> ActDone;
        public event Action<Ledger> DayEnded;
        public event Action<int> DayStarted;
        public event Action<bool> PowerChanged;
        public event Action Cleared, StaffChanged, StageChanged, Shouted, CashChanged;

        public Sim(GameData data, Func<double> random = null)
        {
            D = data;
            var r = new Random();
            rand = random ?? (() => r.NextDouble());
            Dish = data.Dishes.ToDictionary(x => x.Id);
            Ing = data.Ingredients.ToDictionary(x => x.Id);
            Arch = data.Customers.ToDictionary(x => x.Id);
            StaffDefs = data.Staff.ToDictionary(x => x.Id);
        }

        float R() => (float)rand();
        T Pick<T>(IList<T> list) => list[(int)(R() * list.Count) % list.Count];

        // ---------- setup ----------
        public void NewGame(string shopName, string shopUrdu, string hero)
        {
            var b = D.Balance;
            S = new GameState
            {
                Minute = b.DayStart, Cash = b.StartCash, Fame = b.StartFame, Auth = b.StartAuth,
                ShopName = shopName, ShopUrdu = shopUrdu, Hero = hero ?? "pota",
            };
            foreach (var d in D.Dishes) { S.Menu[d.Id] = new MenuEntry { Price = d.Price }; S.Xp[d.Id] = 0; S.Unlocked[d.Id] = d.Unlock.Type == "start"; }
            foreach (var i in D.Ingredients) { S.Stock[i.Id] = 0; S.AutoBuy[i.Id] = i.Target; }
            StartDay();
        }

        public void Load(string json)
        {
            S = JsonConvert.DeserializeObject<GameState>(json);
            StartDay(resumed: true);
        }

        public string Serialize() => Snapshot;

        // ---------- derived ----------
        public StageDef Stage => D.Balance.Stages[S.Stage];
        public string Slot => SlotAt(S.Minute);
        public string Weekday => Weekdays[(S.Day - 1) % 7];
        public int StaffCount => S.Staff.Values.Count(v => v);

        public int MasteryLevel(string id)
        {
            var t = D.Balance.Mastery; int l = 0;
            for (int i = 0; i < t.Length; i++) if (S.Xp[id] >= t[i]) l = i;
            return l;
        }

        bool NihariReady => S.Stage == "dhaba" && S.Staff["karigar"];
        public bool IsLocked(string id) => !S.Unlocked[id] || (id == "nihari" && !NihariReady);
        public bool IsAvailable(string id) => !IsLocked(id) && S.Menu[id].On;
        public bool PowerOut => Outages.Any(o => S.Minute >= o.From && S.Minute < o.To);

        public float DishQuality(string id)
        {
            var d = Dish[id]; var f = D.Balance.Fat[S.Fat];
            float q = d.Quality + MasteryLevel(id) * 3;
            if (d.Fat > 0) q += f.Quality;
            if (S.Staff["karigar"]) q += StaffDefs["karigar"].Quality;
            return q;
        }

        public int PlateCost(string id)
        {
            var d = Dish[id];
            return d.Ingredients.Sum(kv => Ing[kv.Key].Cost * kv.Value) + d.Fat * D.Balance.Fat[S.Fat].CostPerUnit;
        }

        float PriceTolerance(string id, ArchetypeDef a) =>
            Dish[id].RefPrice * a.Budget * (1 + 0.06f * S.Fame) * (1 + 0.04f * (S.Auth - 2));

        public HashSet<string> NeededIngredients()
        {
            var set = new HashSet<string>();
            foreach (var d in D.Dishes)
                if (IsAvailable(d.Id)) foreach (var k in d.Ingredients.Keys) set.Add(k);
            return set;
        }

        public float PatienceOf(Customer c) =>
            Arch[c.Arch].Patience * Stage.Patience * (S.Staff["chhotu"] ? StaffDefs["chhotu"].Patience : 1);

        public List<Customer> Queue() => Customers.Where(c => c.State == "queue").ToList();

        // ---------- day lifecycle ----------
        void StartDay(bool resumed = false)
        {
            var b = D.Balance;
            S.Minute = b.DayStart; S.DayOver = false;
            Customers.Clear(); Pending.Clear();
            Cooks = Enumerable.Range(0, 1 + (S.Staff["karigar"] ? 1 : 0)).Select(_ => new Cook()).ToList();
            Outages.Clear();
            if (R() < b.Outage.Chance)
            {
                float from = b.Outage.From + R() * (b.Outage.To - b.Outage.From);
                Outages.Add(new Outage { From = from, To = from + b.Outage.MinLen + R() * (b.Outage.MaxLen - b.Outage.MinLen) });
            }
            uncleTimes = new List<float> { 440 + R() * 30, 1100 + R() * 40 };
            wasOut = false;
            seatTaken = new int[D.District.Seats.Length];
            warned.Clear();
            Today = new Ledger { Day = S.Day, Weekday = Weekday, FameStart = S.Fame, AuthStart = S.Auth };
            if (!resumed) AutoRestock();
            Snapshot = JsonConvert.SerializeObject(S);
            DayStarted?.Invoke(S.Day);
        }

        void AutoRestock()
        {
            foreach (var k in NeededIngredients())
            {
                int want = Math.Max(0, S.AutoBuy[k] - S.Stock[k]);
                if (want > 0) Buy(k, want, true);
            }
        }

        public int Buy(string k, int qty, bool auto = false)
        {
            int cost = Ing[k].Cost;
            int n = Math.Min(qty, S.Cash / cost);
            if (n <= 0) { if (!auto) Noticed?.Invoke(new Notice { Kind = "warn", Key = "noCash" }); return 0; }
            S.Stock[k] += n; S.Cash -= n * cost; Today.StockBought += n * cost;
            warned.Remove(k);
            CashChanged?.Invoke();
            return n;
        }

        void EndDay()
        {
            var t = Today;
            foreach (var c in Customers) if (c.State == "queue" && !c.Started) ReturnStock(c);
            Customers.Clear(); Pending.Clear(); foreach (var k in Cooks) k.Order = null;
            t.Rent = (int)Math.Round(Stage.RentWeek / 7f);
            t.Wages = S.Staff.Where(kv => kv.Value).Sum(kv => StaffDefs[kv.Key].Wage);
            foreach (var ing in D.Ingredients)
            {
                int lost = (int)Math.Round(S.Stock[ing.Id] * ing.Spoil);
                if (lost > 0) { t.Wastage[ing.Id] = lost; t.WastageRs += lost * ing.Cost; S.Stock[ing.Id] -= lost; }
            }
            S.Cash -= t.Rent + t.Wages + (int)Math.Round(t.Fuel);
            t.Expenses = t.StockBought + t.FatCost + t.Wages + t.Rent + (int)Math.Round(t.Fuel);
            t.Net = t.Revenue + t.Tips - t.Expenses - t.Setup;
            t.FameEnd = S.Fame; t.AuthEnd = S.Auth; t.CashEnd = S.Cash;
            S.History.Add(new DayHistory { Day = t.Day, Revenue = t.Revenue, Net = t.Net, Served = t.Served, Fame = S.Fame, Auth = S.Auth });
            if (S.History.Count > 60) S.History.RemoveAt(0);
            S.Minute = D.Balance.DayEnd; S.DayOver = true;
            S.LastLedger = t;
            CheckActs();
            Cleared?.Invoke();
            DayEnded?.Invoke(t);
        }

        public void NextDay() { S.Day += 1; StartDay(); }

        // ---------- player actions ----------
        public void SetPrice(string id, int price)
        {
            var d = Dish[id];
            S.Menu[id].Price = (int)Clamp((float)Math.Round(price / 10f) * 10, d.Min, d.Max);
        }
        public void ToggleDish(string id) => S.Menu[id].On = !S.Menu[id].On;
        public void SetFat(string f) => S.Fat = f;
        public void SetAutoBuy(string k, int v) => S.AutoBuy[k] = (int)Clamp(v, 0, 300);

        public bool Hire(string id)
        {
            if (S.Staff[id]) return false;
            if (StaffCount >= Stage.StaffCap) { Noticed?.Invoke(new Notice { Kind = "warn", Key = "staffCap" }); return false; }
            S.Staff[id] = true;
            if (id == "karigar") Cooks.Add(new Cook());
            StaffChanged?.Invoke();
            CheckUnlocks();
            return true;
        }

        public void Fire(string id)
        {
            if (!S.Staff[id]) return;
            S.Staff[id] = false;
            if (id == "karigar" && Cooks.Count > 1)
            {
                int i = Cooks.FindIndex(1, k => k.Order == null);
                if (i < 0) i = Cooks.Count - 1;
                var k = Cooks[i]; Cooks.RemoveAt(i);
                if (k.Order != null) Pending.Insert(0, k.Order);
            }
            StaffChanged?.Invoke();
        }

        public bool RentDhaba()
        {
            int cost = D.Balance.Stages["dhaba"].SetupCost;
            if (!S.Act1Done || S.Stage != "cart") return false;
            if (S.Cash < cost) { Noticed?.Invoke(new Notice { Kind = "warn", Key = "noCash" }); return false; }
            S.Cash -= cost; Today.Setup += cost;
            S.Stage = "dhaba"; S.Act = 2;
            StageChanged?.Invoke();
            Noticed?.Invoke(new Notice { Kind = "good", Key = "dhabaOpen" });
            CheckUnlocks();
            return true;
        }

        public bool BuyGenerator()
        {
            int cost = D.Balance.Generator.Cost;
            if (S.Generator || S.Stage != "dhaba") return false;
            if (S.Cash < cost) { Noticed?.Invoke(new Notice { Kind = "warn", Key = "noCash" }); return false; }
            S.Cash -= cost; Today.Setup += cost; S.Generator = true;
            return true;
        }

        public bool Shout()
        {
            var a = D.Balance.Aao;
            if (S.Minute < S.AaoReady || S.DayOver) return false;
            S.AaoUntil = S.Minute + a.Duration;
            S.AaoReady = S.Minute + a.Cooldown;
            // Some people already walking past on the near side turn around and come over.
            foreach (var c in Customers)
            {
                if (c.State == "walk" && c.Dest == "pass" && c.Near && Math.Abs(c.X) < 14 && R() < a.Convert)
                {
                    var order = ChooseOrder(Arch[c.Arch], null, out _);
                    if (order != null) { c.Dest = "player"; c.Order = order; TargetQueueEntry(c); }
                }
            }
            Shouted?.Invoke();
            return true;
        }

        // ---------- rewarded-ad bonuses (design doc §22) ----------
        // The ad itself is shown by the game layer; these only apply the reward once it is earned.
        public bool CanDoubleTips => S.DayOver && Today.Tips > 0 && !Today.TipsDoubled;
        public void RewardDoubleTips()
        {
            if (!CanDoubleTips) return;
            Today.TipsDoubled = true; S.Cash += Today.Tips; Today.CashEnd = S.Cash;
            CashChanged?.Invoke();
        }
        public bool CanRefillShout => !S.DayOver && S.Minute < S.AaoReady;
        public void RewardRefillShout() { if (CanRefillShout) S.AaoReady = S.Minute; }

        // ---------- main tick ----------
        public void Tick(float dt)
        {
            if (S == null || S.DayOver) return;
            S.Minute += dt;
            if (S.Minute >= D.Balance.DayEnd) { EndDay(); return; }
            bool outNow = PowerOut;
            if (outNow != wasOut)
            {
                wasOut = outNow; PowerChanged?.Invoke(outNow);
                if (outNow) Noticed?.Invoke(new Notice { Kind = "warn", Key = S.Generator ? "outageGen" : "outage" });
            }
            if (outNow && S.Generator) Today.Fuel += D.Balance.Generator.FuelPerHour * dt / 60f;
            Spawn(dt);
            for (int i = 0; i < uncleTimes.Count; i++)
                if (uncleTimes[i] > 0 && S.Minute >= uncleTimes[i]) { uncleTimes[i] = -1; SpawnUncle(); }
            UpdateCustomers(dt);
            UpdateKitchen(dt);
        }

        float FootfallRate()
        {
            float r = D.Balance.Footfall[Slot];
            if (Weekday == "Sat" || Weekday == "Sun") r *= 1.15f;
            if (Weekday == "Fri" && S.Minute > 780 && S.Minute < 855) r *= 0.3f; // Jumma
            if (PowerOut) r *= 0.8f;
            return r;
        }

        void Spawn(float dt)
        {
            float expected = FootfallRate() / 60f * dt;
            while (expected > 0)
            {
                if (R() < Math.Min(1, expected)) SpawnOne();
                expected -= 1;
            }
        }

        bool Weekend => Weekday == "Sat" || Weekday == "Sun";

        ArchetypeDef PickArchetype()
        {
            var list = D.Customers.Where(a => !a.Scripted).ToList();
            var w = list.Select(a => a.Slots[Slot] * (a.Id == "family" && Weekend ? 1.6f : 1)).ToList();
            float r = R() * w.Sum();
            for (int i = 0; i < list.Count; i++) { r -= w[i]; if (r <= 0) return list[i]; }
            return list[0];
        }

        List<OrderItem> ChooseOrder(ArchetypeDef a, string exclude, out float weight)
        {
            weight = 0;
            var opts = D.Dishes.Where(d => IsAvailable(d.Id) && d.Id != exclude)
                .Select(d => (d, w: (a.Prefs.TryGetValue(d.Id, out var p) ? p : 0.2f) * d.Slots[Slot])).ToList();
            if (opts.Count == 0) return null;
            float r = R() * opts.Sum(o => o.w);
            var first = opts[0];
            foreach (var o in opts) { r -= o.w; if (r <= 0) { first = o; break; } }
            var order = new List<OrderItem> { new OrderItem { Dish = first.d.Id, Qty = a.Party } };
            if (R() < (a.Party > 1 ? 0.6f : 0.28f))
            {
                var second = opts.Where(o => o.d.Id != first.d.Id).ToList();
                if (second.Count > 0) order.Add(new OrderItem { Dish = Pick(second).d.Id, Qty = a.Party > 1 ? 2 : 1 });
            }
            weight = first.w;
            return order;
        }

        Customer MakeCustomer(ArchetypeDef a, bool fromLeft)
        {
            bool near = R() < 0.62f;
            var c = new Customer
            {
                Id = nextId++, Arch = a.Id, Party = a.Party, Near = near,
                X = fromLeft ? -Edge : Edge, Z = near ? -1.9f + R() * 1.2f : 3.8f + R() * 0.5f,
                Tx = fromLeft ? Edge : -Edge, Look = (int)(R() * 1e6f),
            };
            c.Tz = c.Z;
            return c;
        }

        void SpawnOne()
        {
            var b = D.Balance; var st = Stage;
            var a = PickArchetype();
            var c = MakeCustomer(a, R() < 0.5f);
            var order = ChooseOrder(a, null, out float weight);
            if (order != null)
            {
                float p = b.VisitBase * (0.55f + 0.3f * S.Fame) * Clamp(weight / 1.2f, 0.5f, 1.5f) * st.Visit;
                if (a.Id == "family") p *= st.FamilyVisit;
                if (S.Minute < S.AaoUntil) p *= b.Aao.Mult;
                if (PowerOut && !S.Generator) p *= 0.8f;
                if (R() < Math.Min(0.92f, p))
                {
                    c.Dest = "player"; c.Order = order; c.Near = true; c.Z = -1.6f + R() * 0.8f;
                    TargetQueueEntry(c);
                }
            }
            if (c.Dest == "pass" && c.Near)
            {
                var rivals = D.District.Rivals.Where(r => r.Slots.Contains(Slot)).ToList();
                if (rivals.Count > 0 && R() < b.RivalShare)
                {
                    var r = Pick(rivals);
                    c.Dest = "rival"; c.Rival = r.Id;
                    c.Tx = r.X + (R() - 0.5f) * 2.2f; c.Tz = r.Z + 0.9f + R() * 0.7f;
                }
            }
            if (c.Dest == "pass") c.Tz = c.Z;
            Customers.Add(c);
            Spawned?.Invoke(c);
        }

        void SpawnUncle()
        {
            if (!IsAvailable("chai")) return;
            var c = MakeCustomer(Arch["uncle"], false);
            c.Near = true; c.Z = -1.4f; c.Dest = "player"; c.Uncle = true;
            var order = new List<OrderItem> { new OrderItem { Dish = "chai", Qty = 1 } };
            string extra = S.Minute > 900
                ? (IsAvailable("samosa") ? "samosa" : IsAvailable("bun_kabab") ? "bun_kabab" : null)
                : (IsAvailable("nihari") ? "nihari" : null);
            if (extra != null) order.Add(new OrderItem { Dish = extra, Qty = 1 });
            c.Order = order;
            TargetQueueEntry(c);
            Customers.Add(c);
            Spawned?.Invoke(c);
        }

        public (float x, float z) QueuePos(int i) => (D.District.Queue.X + D.District.Queue.Step * i, D.District.Queue.Z);

        void TargetQueueEntry(Customer c)
        {
            var p = QueuePos(Queue().Count);
            c.Tx = p.x; c.Tz = p.z; c.State = "walk";
        }

        bool InStockFor(OrderItem it) => Dish[it.Dish].Ingredients.All(kv => S.Stock[kv.Key] >= kv.Value * it.Qty);

        void ReturnStock(Customer c)
        {
            if (!c.Taken) return;
            foreach (var it in c.Order) foreach (var kv in Dish[it.Dish].Ingredients) S.Stock[kv.Key] += kv.Value * it.Qty;
            c.Taken = false;
        }

        // The customer reaches the stall, reads the price board and orders.
        void PlaceOrder(Customer c)
        {
            var a = Arch[c.Arch];
            foreach (var it in c.Order)
            {
                if (!IsAvailable(it.Dish) || !InStockFor(it))
                {
                    if (IsAvailable(it.Dish)) Today.Missed[it.Dish] = (Today.Missed.TryGetValue(it.Dish, out var m) ? m : 0) + it.Qty;
                    var alt = D.Dishes.FirstOrDefault(d => IsAvailable(d.Id) && InStockFor(new OrderItem { Dish = d.Id, Qty = it.Qty }) && !c.Order.Any(o => o.Dish == d.Id));
                    if (alt != null) { it.Dish = alt.Id; Today.SoldOut++; } else it.Qty = 0;
                }
            }
            c.Order = c.Order.Where(it => it.Qty > 0).ToList();
            if (c.Order.Count == 0)
            {
                Today.SoldOut++; S.Fame = Clamp(S.Fame - 0.002f, 0, 5);
                Bubble?.Invoke(new BubbleEvent { C = c, Text = "Khatam?", Mood = -1, Key = "soldOut" });
                Leave(c); return;
            }
            var main = c.Order[0];
            float ratio = S.Menu[main.Dish].Price / PriceTolerance(main.Dish, a);
            if (!c.Uncle && R() < Clamp((ratio - 1) / 0.6f, 0, 1))
            {
                Today.Refused++; S.Fame = Clamp(S.Fame - 0.001f, 0, 5);
                Bubble?.Invoke(new BubbleEvent { C = c, Text = "Mehenga hai!", Mood = -1, Key = "tooExpensive" });
                Leave(c); return;
            }
            foreach (var it in c.Order)
                foreach (var kv in Dish[it.Dish].Ingredients)
                {
                    S.Stock[kv.Key] -= kv.Value * it.Qty;
                    if (S.Stock[kv.Key] < 3 && warned.Add(kv.Key)) Noticed?.Invoke(new Notice { Kind = "warn", Key = "lowStock", Ing = kv.Key });
                }
            c.Taken = true;
            float work = c.Order.Sum(it => Dish[it.Dish].Cook * (float)Math.Pow(it.Qty, 0.7) * (1 - 0.04f * MasteryLevel(it.Dish)));
            if (S.Staff["chhotu"]) work *= StaffDefs["chhotu"].Speed;
            if (PowerOut && !S.Generator) work *= 1.3f;
            c.Work = c.WorkTotal = work;
            Pending.Add(c);
            Bubble?.Invoke(new BubbleEvent { C = c, Dish = main.Dish });
        }

        void UpdateKitchen(float dt)
        {
            foreach (var k in Cooks)
            {
                if (k.Order == null && Pending.Count > 0) { k.Order = Pending[0]; Pending.RemoveAt(0); k.Order.Started = true; }
                if (k.Order != null)
                {
                    k.Order.Work -= dt;
                    if (k.Order.Work <= 0) { var c = k.Order; k.Order = null; Serve(c); }
                }
            }
        }

        void Serve(Customer c)
        {
            var b = D.Balance; var t = Today; var a = Arch[c.Arch];
            int bill = 0; float qual = 0; int n = 0;
            foreach (var it in c.Order)
            {
                var d = Dish[it.Dish];
                int price = S.Menu[it.Dish].Price;
                bill += price * it.Qty;
                if (!t.Sales.TryGetValue(it.Dish, out var sale)) t.Sales[it.Dish] = sale = new Sale();
                sale.Qty += it.Qty; sale.Rs += price * it.Qty;
                int lvlBefore = MasteryLevel(it.Dish);
                S.Xp[it.Dish] += it.Qty;
                if (d.Fat > 0)
                {
                    int fatCost = d.Fat * it.Qty * b.Fat[S.Fat].CostPerUnit;
                    t.FatCost += fatCost; S.Cash -= fatCost;
                    float delta = b.Fat[S.Fat].Auth * d.Fat * it.Qty;
                    S.Auth = Clamp(S.Auth + (delta > 0 ? delta * (1 - S.Auth / 5) : delta * (0.3f + S.Auth / 5)), 0, 5);
                }
                if (MasteryLevel(it.Dish) > lvlBefore) Noticed?.Invoke(new Notice { Kind = "good", Key = "mastery", Dish = it.Dish, Level = MasteryLevel(it.Dish) });
                qual += DishQuality(it.Dish) - (price - d.RefPrice) / (float)d.RefPrice * 40; n++;
            }
            qual /= n;
            S.Cash += bill; t.Revenue += bill; S.TotalRevenue += bill;
            S.TotalServed += c.Party; t.Served += c.Party;
            float expectation = 50 + 6 * S.Fame;
            float sat = Clamp((qual - expectation - c.Waited / PatienceOf(c) * 22 + (S.Auth - 2.5f) * 4 * a.Auth) / 25, -1, 1);
            if (sat > 0) S.Fame += 0.003f * sat * a.FameMult * (1 - S.Fame / 5.5f) * (c.Party > 1 ? 1.5f : 1);
            else S.Fame += 0.009f * sat;
            S.Fame = Clamp(S.Fame, 0, 5);
            c.Sat = sat;
            // Happy customers leave a little extra. A rewarded ad can double the night's tips (design doc §22).
            if (sat > 0.5f) { int tip = (int)Math.Round(bill * 0.08f / 10f) * 10; t.Tips += tip; S.Cash += tip; }
            if (c.Uncle) UncleSpeaks(c, sat);
            else Bubble?.Invoke(new BubbleEvent { C = c, Mood = sat > 0.15f ? 1 : sat < -0.15f ? -1 : 0, Served = true });
            CashChanged?.Invoke();
            var seats = FreeSeats();
            if (S.Stage == "dhaba" && seats.Count >= c.Party)
            {
                c.Seats = seats.Take(c.Party).ToArray();
                foreach (var i in c.Seats) seatTaken[i] = c.Id;
                var s0 = D.District.Seats[c.Seats[0]];
                c.State = "walk"; c.Dest = "seat"; c.Tx = s0[0]; c.Tz = s0[1];
            }
            else Leave(c);
            CheckUnlocks();
            CheckActs();
        }

        void UncleSpeaks(Customer c, float sat)
        {
            var lines = D.District.UncleLines;
            string kind = S.Fat == "ghee" ? "ghee" : "oil";
            if (c.Waited > PatienceOf(c) * 0.7f) kind = "slow";
            string text = Pick(lines[kind]);
            if (kind == "oil") S.Auth = Clamp(S.Auth - 0.03f, 0, 5);
            if (kind == "ghee" && sat > 0) S.Auth = Clamp(S.Auth + 0.01f, 0, 5);
            Today.Notes.Add(new Note { Who = "uncle", Text = text });
            Bubble?.Invoke(new BubbleEvent { C = c, Mood = kind == "ghee" ? 1 : -1, Served = true });
            Noticed?.Invoke(new Notice { Kind = kind == "ghee" ? "good" : "warn", Who = "uncle", Text = text });
        }

        List<int> FreeSeats()
        {
            var o = new List<int>();
            for (int i = 0; i < seatTaken.Length; i++) if (seatTaken[i] == 0) o.Add(i);
            return o;
        }

        void Leave(Customer c)
        {
            c.State = "walk"; c.Dest = "leave";
            c.Tx = c.X < 0 ? -Edge : Edge; c.Tz = -1.2f + R() * 0.8f;
            if (c.Seats != null) { foreach (var i in c.Seats) seatTaken[i] = 0; c.Seats = null; }
        }

        void UpdateCustomers(float dt)
        {
            float step = D.Balance.WalkSpeed * dt;
            var q = new List<Customer>();
            foreach (var c in Customers.ToList())
            {
                if (c.State == "walk")
                {
                    float dx = c.Tx - c.X, dz = c.Tz - c.Z, dist = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (dist <= step) { c.X = c.Tx; c.Z = c.Tz; Arrive(c); }
                    else { c.X += dx / dist * step; c.Z += dz / dist * step; }
                    // Re-aim at the end of the queue while walking in, since the line moves.
                    if (c.Dest == "player" && c.State == "walk") { var p = QueuePos(Queue().Count); c.Tx = p.x; c.Tz = p.z; }
                }
                else if (c.State == "queue")
                {
                    q.Add(c);
                    if (!c.Started)
                    {
                        c.Waited += dt;
                        if (c.Waited > PatienceOf(c))
                        {
                            Today.Walkouts++; S.Fame = Clamp(S.Fame - 0.004f, 0, 5);
                            Pending.Remove(c);
                            ReturnStock(c);
                            Bubble?.Invoke(new BubbleEvent { C = c, Text = c.Uncle ? "Bohat dair, beta." : "Bohat dair!", Mood = -1, Key = "walkout" });
                            Leave(c);
                        }
                    }
                }
                else if (c.State == "rival" || c.State == "eating")
                {
                    c.Timer -= dt;
                    if (c.Timer <= 0) Leave(c);
                }
            }
            // Shuffle the queue forward.
            for (int i = 0; i < q.Count; i++)
            {
                var p = QueuePos(i); float k = Math.Min(1, dt * 0.8f);
                q[i].X += (p.x - q[i].X) * k; q[i].Z += (p.z - q[i].Z) * k;
            }
            Customers.RemoveAll(c => c.Gone);
        }

        void Arrive(Customer c)
        {
            switch (c.Dest)
            {
                case "pass":
                case "leave":
                    c.Gone = true; Despawned?.Invoke(c); return;
                case "rival":
                    c.State = "rival"; c.Timer = 10 + R() * 18; return;
                case "seat":
                    c.State = "eating"; c.Timer = 14 + R() * 12 + (c.Party > 1 ? 8 : 0); return;
                case "player":
                    if (Queue().Count >= Stage.MaxQueue)
                    {
                        Today.QueueFull++; S.Fame = Clamp(S.Fame - 0.001f, 0, 5);
                        Bubble?.Invoke(new BubbleEvent { C = c, Text = "Lambi line!", Mood = -1, Key = "queueFull" });
                        Leave(c); return;
                    }
                    c.State = "queue"; c.Waited = 0;
                    PlaceOrder(c);
                    return;
            }
        }

        // ---------- progress ----------
        void CheckUnlocks()
        {
            foreach (var d in D.Dishes)
            {
                if (S.Unlocked[d.Id]) continue;
                var u = d.Unlock;
                bool ok = (u.Type == "served" && S.TotalServed >= u.Value) || (u.Type == "dhaba_karigar" && NihariReady);
                if (!ok) continue;
                S.Unlocked[d.Id] = true;
                foreach (var k in d.Ingredients.Keys) if (S.Stock[k] < 10) Buy(k, Math.Min(S.AutoBuy[k], 20), true);
                Unlocked?.Invoke(d.Id);
            }
        }

        public void CheckActs()
        {
            var b = D.Balance;
            if (!S.Act1Done && S.TotalRevenue >= b.Act1.Revenue && S.TotalServed >= b.Act1.Served) { S.Act1Done = true; ActDone?.Invoke(1); }
            if (S.Stage == "dhaba" && !S.Act2Done && S.Fame >= b.Act2.Fame && S.Cash >= b.Act2.Cash) { S.Act2Done = true; ActDone?.Invoke(2); }
        }
    }
}
