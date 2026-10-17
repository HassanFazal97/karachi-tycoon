// Content definitions loaded from Assets/Resources/Data/*.json (design doc §20: data first).
// Plain C# with no UnityEngine dependency, so the simulation also runs headless (Tools/BalanceRunner).
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KarachiTycoon.Sim
{
    public class UnlockDef
    {
        public string Type;   // "start" | "served" | "dhaba_karigar"
        public int Value;
    }

    public class DishDef
    {
        public string Id, En, Ur, Note;
        public int Price, Min, Max;
        [JsonProperty("ref")] public int RefPrice;
        public float Cook, Quality;
        public int Fat;
        public Dictionary<string, int> Ingredients = new();
        public Dictionary<string, float> Slots = new();
        public UnlockDef Unlock = new();
    }

    public class IngredientDef
    {
        public string Id, En, Ur, Supplier;
        public int Cost, Target;
        public float Spoil;
    }

    public class ArchetypeDef
    {
        public string Id, En, Ur;
        public int Party;
        public float Budget, Patience, FameMult, Auth;
        public bool Scripted;
        public Dictionary<string, float> Slots = new();
        public Dictionary<string, float> Prefs = new();
    }

    public class StaffDef
    {
        public string Id, En, Ur, Name, Does;
        public int Wage, Cooks;
        public float Speed = 1, Patience = 1, Quality;
    }

    public class StageDef
    {
        public int RentWeek, StaffCap, MaxQueue, Seats, SetupCost;
        public float Visit, FamilyVisit, Patience;
    }

    public class FatDef { public int CostPerUnit; public float Quality, Auth; }
    public class GeneratorDef { public int Cost, FuelPerHour; }
    public class OutageDef { public float Chance, MinLen, MaxLen, From, To; }
    public class AaoDef { public float Duration, Cooldown, Mult, Convert; }
    public class Act1Def { public int Revenue, Served; }
    public class Act2Def { public float Fame; public int Cash; }

    public class BalanceDef
    {
        public float DayStart, DayEnd, RealSecondsPerDay;
        public int StartCash;
        public float StartFame, StartAuth, WalkSpeed, VisitBase, RivalShare;
        public Dictionary<string, float> Footfall = new();
        public Dictionary<string, StageDef> Stages = new();
        public Dictionary<string, FatDef> Fat = new();
        public GeneratorDef Generator;
        public OutageDef Outage;
        public AaoDef Aao;
        public Act1Def Act1;
        public Act2Def Act2;
        public int[] Mastery;
    }

    public class PointDef { public float X, Z, Step; }

    public class SignDef
    {
        public int Bay;
        public string En, Ur, Since, Bg, Fg;
        public bool Rival, Shut;
    }

    public class BuildingDef
    {
        public int Floors, Seed;
        public string Shutter, Plaque;
        public int[] Jharoka = new int[0];
        public List<SignDef> Signs = new();
    }

    public class RivalDef
    {
        public string Id, En, Ur, Kind;
        public float X, Z;
        public List<string> Slots = new();
    }

    public class DistrictDef
    {
        public string Name, Ur;
        public PointDef Pitch, Queue;
        public float[][] Seats, Tables;
        public List<RivalDef> Rivals = new();
        public List<BuildingDef> Buildings = new();
        public Dictionary<string, List<string>> UncleLines = new();
    }

    public class UpgradeDef
    {
        public string Id, En, Note;
        public int Cost;
        // Multipliers: "patience", "visit", "night" (night footfall), "cook_<dish>" (cooking time).
        public Dictionary<string, float> Effect = new();
    }

    public class GoalDef
    {
        public string Id, Text, Type, Dish, Slot;
        public float Until;
        public int Min, Max, Reward, Coins;
    }

    public class GameData
    {
        public List<DishDef> Dishes;
        public List<IngredientDef> Ingredients;
        public List<ArchetypeDef> Customers;
        public List<StaffDef> Staff;
        public BalanceDef Balance;
        public DistrictDef District;
        public List<UpgradeDef> Upgrades = new();
        public List<GoalDef> Goals = new();

        // Reads the data files through a loader (Resources in Unity, the file system in tools).
        public static GameData Load(System.Func<string, string> readText)
        {
            T Read<T>(string name) => JsonConvert.DeserializeObject<T>(readText(name));
            return new GameData
            {
                Dishes = Read<List<DishDef>>("dishes"),
                Ingredients = Read<List<IngredientDef>>("ingredients"),
                Customers = Read<List<ArchetypeDef>>("customers"),
                Staff = Read<List<StaffDef>>("staff"),
                Balance = Read<BalanceDef>("balance"),
                District = Read<DistrictDef>("burns_road"),
                Upgrades = Read<List<UpgradeDef>>("upgrades"),
                Goals = Read<List<GoalDef>>("goals"),
            };
        }
    }
}
