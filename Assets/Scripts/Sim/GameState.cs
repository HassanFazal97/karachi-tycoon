// Everything that is saved. Saves always hold the start of a day, so a reload never lands mid-shift.
using System.Collections.Generic;

namespace KarachiTycoon.Sim
{
    public class MenuEntry { public bool On = true; public int Price; }

    public class DayHistory { public int Day, Revenue, Net, Served; public float Fame, Auth; }

    public class Sale { public int Qty, Rs; }

    public class Note { public string Who, Text; }

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
        public int Coins;
        public bool NoAds;
    }
}
