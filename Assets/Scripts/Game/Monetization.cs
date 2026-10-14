using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace KarachiTycoon.Game
{
    // Purchases (design doc §22). The game only talks to IStoreService, so Unity IAP (phones) and
    // Steamworks (Steam) plug in later without touching gameplay code. Ads live in Game/Ads.

    public class Product
    {
        public string Id, Title, Description, PriceUsd;
        public int Coins;
        public bool Consumable;
        public int SubscriptionDays;   // > 0 for Seth ji Club
    }

    public static class Catalog
    {
        public static readonly List<Product> Products = new List<Product>
        {
            new Product { Id = "no_ads", Title = "No Ads", Description = "Removes ad breaks forever. Ad bonuses stay available if you want them.", PriceUsd = "$2.99" },
            new Product { Id = "double_income", Title = "Dada ki Barkat", Description = "Permanent 2x income: every plate you sell earns double, for good.", PriceUsd = "$4.99" },
            new Product { Id = "seth_ji_club", Title = "Seth ji Club", Description = "30 days of every ad bonus without watching an ad: double tips, an extra shout and more.", PriceUsd = "$2.99 / month", SubscriptionDays = 30 },
            new Product { Id = "starter_dadas_trunk", Title = "Dada's Trunk", Description = "One-time starter pack: 300 silver coins, a truck-art signboard style and a brass galla.", PriceUsd = "$1.99", Coins = 300 },
            new Product { Id = "coins_small", Title = "Pouch of sikkay", Description = "120 silver coins.", PriceUsd = "$0.99", Coins = 120, Consumable = true },
            new Product { Id = "coins_medium", Title = "Galla of sikkay", Description = "700 silver coins.", PriceUsd = "$4.99", Coins = 700, Consumable = true },
            new Product { Id = "coins_large", Title = "Seth's tijori", Description = "1,600 silver coins.", PriceUsd = "$9.99", Coins = 1600, Consumable = true },
        };
    }

    // Everything bought with real money. Saved apart from the day save, so a reload never takes back a purchase.
    public class Wallet
    {
        public int Coins;
        public bool NoAds, DoubleIncome;
        public HashSet<string> Owned = new HashSet<string>();
        public DateTime SethJiUntil;
        [JsonIgnore] public bool SethJi => DateTime.Now < SethJiUntil;

        public bool Owns(Product p) => !p.Consumable && p.SubscriptionDays == 0 && Owned.Contains(p.Id);

        public void Grant(Product p)
        {
            Coins += p.Coins;
            if (p.Id == "no_ads") NoAds = true;
            if (p.Id == "double_income") DoubleIncome = true;
            if (p.SubscriptionDays > 0) SethJiUntil = (SethJi ? SethJiUntil : DateTime.Now).AddDays(p.SubscriptionDays);
            if (!p.Consumable && p.SubscriptionDays == 0) Owned.Add(p.Id);
        }
    }

    public interface IStoreService
    {
        IReadOnlyList<Product> Products { get; }
        void Purchase(string productId, Action<bool> onDone);
    }

    // Stand-in used until Unity IAP / Steamworks are added: grants products straight away, charges nothing.
    public class SimulatedStore : IStoreService
    {
        readonly Wallet wallet;
        public SimulatedStore(Wallet w) { wallet = w; }
        public IReadOnlyList<Product> Products => Catalog.Products;
        public void Purchase(string productId, Action<bool> onDone)
        {
            var p = Catalog.Products.Find(x => x.Id == productId);
            if (p == null || wallet.Owns(p)) { onDone(false); return; }
            wallet.Grant(p);
            onDone(true);
        }
    }
}
