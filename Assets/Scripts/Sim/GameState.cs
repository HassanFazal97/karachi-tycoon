// Everything that is saved. Saves always hold the start of a day, so a reload never lands mid-shift.
using System.Collections.Generic;

namespace KarachiTycoon.Sim
{
    public class MenuEntry { public bool On = true; public int Price; }

    public class DayHistory { public int Day, Revenue, Net, Served; public float Fame, Auth; }

    public class Sale { public int Qty, Rs; }

    public class Note { public string Who, Text; }

    // One of the day's three "Aaj ka kaam" goals.
    public class DailyGoal
    {
        public string Id, Text, Type, Dish, Slot;
        public float Until;
        public int Target, Progress, Reward, Coins;
        public bool Done, Failed;
    }

    public class Ledger
    {
        public int Day; public string Weekday;
        public Dictionary<string, Sale> Sales = new();
        public int Revenue, StockBought, FatCost, Wages, Rent, Setup, Tips, WastageRs;
        public float Fuel;
        public Dictionary<string, int> Wastage = new();
        public Dictionary<string, int> Missed = new();
        public int Served, Walkouts, Refused, SoldOut, QueueFull;
        public float FameStart, AuthStart, FameEnd, AuthEnd;
        public int Expenses, Net, CashEnd;
        public bool TipsDoubled;
        public List<Note> Notes = new();
        // Act 1 safety net: what Uncle Rasheed lent so the galla still covers tomorrow's stock.
        public int Loan;
        // Rush streak: customers served in a row without a walkout. Raises tips.
        public int Streak, BestStreak;
        public int GoalRewards, Upgrades;
    }

    public class GameState
    {
        public int Version = 1;
        public int Day = 1;
        public float Minute;
        public int Cash, TotalRevenue, TotalServed;
        public float Fame, Auth;
        public string Stage = "cart";
        public int Act = 1;
        public bool Act1Done, Act2Done, DayOver, Generator;
        public Dictionary<string, bool> Staff = new() { ["chhotu"] = false, ["karigar"] = false };
        public Dictionary<string, MenuEntry> Menu = new();
        public string Fat = "ghee";
        public Dictionary<string, int> Stock = new();
        public Dictionary<string, int> AutoBuy = new();
        public Dictionary<string, bool> Unlocked = new();
        public Dictionary<string, int> Xp = new();
        public string ShopName, ShopUrdu, Hero = "pota";
        public List<DayHistory> History = new();
        public float AaoUntil, AaoReady;
        public Ledger LastLedger;

        // Free-to-play (design doc §22). Coins are the premium currency; NoAds removes interstitials.
        // Onboarding. Null for saves made before the tutorial existed: they skip it and keep everything unlocked.
        // "day1" while the guided first day runs, "done" afterwards (also when skipped).
        public string Tutorial;
        // One-time hints and unlock explainers already shown.
        public List<string> Seen = new();
        public List<DailyGoal> Goals = new();
        public List<string> Owned = new();   // thela upgrades bought
        // Story: friendship points with each named regular, Dada's recipe pages found, choice cards
        // already drawn (no repeats until all are used), and Uncle Rasheed's quest line.
        public Dictionary<string, int> Friends = new();
        public List<string> Pages = new();
        public List<string> CardsSeen = new();
        public int QuestIndex;
        public string QuestActive;
        public int QuestProgress;
        // Behaviour, for gentle reminders ("never shouted by day 3").
        public int Shouts, PriceChanges;

        public int Coins;
        public bool NoAds;
        // Bought "Dada ki Barkat": every sale earns double.
        public bool DoubleIncome;
    }
}
