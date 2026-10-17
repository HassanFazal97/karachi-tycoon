using System.Linq;
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
        public string Id, En, Ur, Name, Does, Role;
        public int Wage, Cooks;
        public float Speed = 1, Patience = 1, Quality;
    }

    // One rung of the business ladder (Resources/Data/business.json): thela to famous restaurant.
    public class StageDef
    {
        public string Id, Name, Note;
        public int RentWeek, StaffCap, MaxQueue, Seats, SetupCost;
        public float Visit, FamilyVisit, Patience;
        // Bigger places draw people from across the city, not just passers-by.
        public float Draw = 1;
        // Highest level each upgrade track can reach at this tier ("staff" is StaffCap).
        public Dictionary<string, int> Caps = new();
        public TierRequirement Requires;
    }

    public class TierRequirement
    {
        public int Act, Served;
        public float Fame;
        public List<string> Maxed = new();
    }

    // An upgrade track: levels with rising cost; "per" is the effect of each level.
    public class TrackDef
    {
        public string Id, Name, Icon, Note, Unit;
        public int Base;
        public float Growth = 1.8f, Per;
    }

    public class CityDistrictDef
    {
        public string Id, Name, Format, Unlock;
        public float X, Y;
        public bool Open;
    }

    public class BusinessDef
    {
        public List<StageDef> Tiers = new();
        public List<TrackDef> Tracks = new();
        public float[][] SeatsIndoor = new float[0][];
        public float[][] SeatsCart = new float[0][];   // the bara thela's stools
        public List<CityDistrictDef> Districts = new();
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

    public class GoalDef
    {
        public string Id, Text, Type, Dish, Slot;
        public float Until;
        public int Min, Max, Reward, Coins;
    }

    // Story content (Resources/Data/story.json): named regulars, Dada's recipe pages, choice cards, Uncle's quests.
    public class RegularDef
    {
        public string Id, Name, Role, Arch, Slot, Line, Page;
        public List<string> Days = new();
        public Dictionary<string, int> Order = new();
        public List<string> Levels = new();
    }

    public class PageDef
    {
        public string Id, Title, Text, Hint;
        // "quality_<dish>" (added), or "patience" / "visit" / "tips" / "spoil" (multipliers).
        public string Bonus;
        public float Value;
    }

    public class CardEffects
    {
        public int Cash;
        public float Fame, Auth;
        public Dictionary<string, int> Friend = new();
        public Dictionary<string, int> Stock = new();
        public string Page;
        public bool RivalsOff;
    }

    public class CardOption { public string Label, Result; public CardEffects Effects = new(); }

    public class CardDef
    {
        public string Id, Title, Text, Stage;
        public int MinDay;
        public List<string> Slots = new();
        public List<CardOption> Options = new();
    }

    public class QuestDef
    {
        public string Id, Title, Type, Dish, Stage, Page, Intro, Outro;
        public int MinDay, Target, Coins;
    }

    public class StoryDef
    {
        public List<RegularDef> Regulars = new();
        public List<PageDef> Pages = new();
        public List<CardDef> Cards = new();
        public List<QuestDef> Quests = new();
    }

    public class GameData
    {
        public List<DishDef> Dishes;
        public List<IngredientDef> Ingredients;
        public List<ArchetypeDef> Customers;
        public List<StaffDef> Staff;
        public BalanceDef Balance;
        public DistrictDef District;
        public List<GoalDef> Goals = new();
        public StoryDef Story = new();
        public BusinessDef Business = new();

        // Reads the data files through a loader (Resources in Unity, the file system in tools).
        public static GameData Load(System.Func<string, string> readText)
        {
            T Read<T>(string name) => JsonConvert.DeserializeObject<T>(readText(name));
            var data = new GameData
            {
                Dishes = Read<List<DishDef>>("dishes"),
                Ingredients = Read<List<IngredientDef>>("ingredients"),
                Customers = Read<List<ArchetypeDef>>("customers"),
                Staff = Read<List<StaffDef>>("staff"),
                Balance = Read<BalanceDef>("balance"),
                District = Read<DistrictDef>("burns_road"),
                Goals = Read<List<GoalDef>>("goals"),
                Story = Read<StoryDef>("story"),
                Business = Read<BusinessDef>("business"),
            };
            // The ladder in business.json is the source of truth for every stage.
            data.Balance.Stages = data.Business.Tiers.ToDictionary(t => t.Id);
            return data;
        }
    }
}
