using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sprint 5 içerik üreteci: Dünya 0 (Park, deco prefablar) ve Dünya 1 (Yiyecekler, food prefablar) için
/// 5'er LevelData üretir (artan zorluk + yığınlama), GameScene'de LevelManager.worlds'ü bağlar.
/// Bomba prefabı Assets/Prefabs/Bomb.prefab (LevelSetupCreator üretir). Phyllotaxis dizilim LevelManager'da.
/// Menu: Tools/GET_IT/Create World Content (Park + Foods)
/// </summary>
public static class WorldContentCreator
{
    const string DECO_DIR      = "Assets/Prefabs/DecoObjects";
    const string FOODS_DIR     = "Assets/Prefabs/Foods";
    const string CARS_DIR      = "Assets/Prefabs/Cars";
    const string BUILDINGS_DIR = "Assets/Prefabs/Buildings";
    const string SWEETS_DIR    = "Assets/Prefabs/Sweets";
    const string DRINKS_DIR    = "Assets/Prefabs/Drinks";
    const string GIFTS_DIR     = "Assets/Prefabs/Gifts";
    const string BOOKS_DIR     = "Assets/Prefabs/Books";
    const string CATS_DIR      = "Assets/Prefabs/Cats";
    const string DOGS_DIR      = "Assets/Prefabs/Dogs";
    const string SHIPS_DIR     = "Assets/Prefabs/Ships";
    const string PLANES_DIR    = "Assets/Prefabs/Planes";
    const string MONEY_DIR     = "Assets/Prefabs/Money";
    const string JEWELRY_DIR   = "Assets/Prefabs/Jewelry";
    const string POWERUPS_DIR  = "Assets/Prefabs/PowerUps";
    const string LEVELS_DIR    = "Assets/Levels";
    const string BOMB_PATH     = "Assets/Prefabs/Bomb.prefab";

    // (tür, nokta sayısı, yığın yüksekliği, boyut çarpanı)
    struct S { public string t; public int c, k; public float sc; public S(string t, int c, int k = 1, float sc = 1f) { this.t = t; this.c = c; this.k = k; this.sc = sc; } }
    struct O { public string t; public int r; public O(string t, int r) { this.t = t; this.r = r; } }

    [MenuItem("Tools/GET_IT/Create World Content (All Worlds)")]
    public static void Run()
    {
        EnsureDir(LEVELS_DIR);
        var bomb = AssetDatabase.LoadAssetAtPath<GameObject>(BOMB_PATH);
        if (bomb == null) Debug.LogWarning("[WorldContent] Bomb.prefab yok — bombalı leveller bombasız kurulacak. Önce Create Sample Levels çalıştır.");

        // DÜNYA 0 = PARK: 2026-07-24'te elle 5 level → GENERIC 15 level (kullanıcı: diğerleri gibi, hard 5/10/15).
        // FBX deco'lar (Tree/Bench/FlowerPot...) + prosedürel eklentiler (Bush/Fountain/Gazebo... ParkDecoGenerator).
        var park = BuildGenericWorld("Park", 0, DECO_DIR, bomb, baseReqLo: 14, baseReqHi: 30);

        var foods = new[]
        {
            CreateLevel("World1_Level1", 1, 0, 180f, FOODS_DIR,
                new[]{ new S("Donut",1,14), new S("PizzaSlice",1,10), new S("Tomato",1,8), new S("Hamburger",1,10), new S("Broccoli",1), new S("FriedEgg",1), new S("HotDog",1), new S("Sushi",1), new S("Kebab",1), new S("Cupcake",1), new S("IceCream",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Donut",48), new O("Tomato",36) }, null, 0),
            CreateLevel("World1_Level2", 1, 1, 175f, FOODS_DIR,
                new[]{ new S("Donut",1,14), new S("PizzaSlice",1,12), new S("Tomato",1,8), new S("Hamburger",1,10), new S("Broccoli",1), new S("FriedEgg",1), new S("HotDog",1), new S("FrenchFries",1), new S("Sushi",1), new S("Kebab",1), new S("Croissant",1), new S("Taco",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("PizzaSlice",48), new O("HotDog",30), new O("Broccoli",24) }, null, 0),
            CreateLevel("World1_Level3", 1, 2, 210f, FOODS_DIR,
                new[]{ new S("Donut",1,14), new S("PizzaSlice",1,12), new S("Hamburger",1,12), new S("Tomato",1,8), new S("FriedEgg",1), new S("HotDog",1), new S("FrenchFries",1), new S("Sushi",1), new S("Kebab",1), new S("Ramen",1), new S("Steak",1), new S("Cupcake",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("MacaronYellow",100), new O("MacaronPink",70), new O("MacaronBlue",70) }, bomb, 3),
            CreateLevel("World1_Level4", 1, 3, 165f, FOODS_DIR,
                new[]{ new S("Donut",1,14), new S("PizzaSlice",1,12), new S("Hamburger",1,12), new S("HotDog",1), new S("FrenchFries",1), new S("Sushi",1), new S("Kebab",1), new S("Ramen",1), new S("ChickenSalad",1), new S("Spaghetti",1), new S("BentoBox",1), new S("IceCream",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Ramen",24), new O("Kebab",36), new O("ChickenSalad",18) }, bomb, 4),
            CreateLevel("World1_Level5", 1, 4, 160f, FOODS_DIR,
                new[]{ new S("Donut",1,14), new S("PizzaSlice",1,12), new S("Hamburger",1,12), new S("Sushi",1), new S("Kebab",1), new S("Ramen",1), new S("ChickenSalad",1), new S("Spaghetti",1), new S("WholePizza",1), new S("Steak",1), new S("Taco",1), new S("BentoBox",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("WholePizza",24), new O("Spaghetti",24), new O("Hamburger",42) }, bomb, 5),

            // ── Yeni levellar (Level6–20), eski + yeni yiyecekler karışık, temalı ──
            // L6 — Bahçe: meyve & sebze girişi (kolay, bombasız)
            CreateLevel("World1_Level6", 1, 5, 170f, FOODS_DIR,
                new[]{ new S("Apple",1,14), new S("Strawberry",1,12), new S("Tomato",1,10), new S("Cherry",1,8), new S("Pear",1), new S("Cucumber",1), new S("Broccoli",1), new S("Eggplant",1), new S("Donut",1), new S("Cupcake",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Apple",54), new O("Strawberry",40) }, null, 0),
            // L7 — Fırın: kurabiye, hamur işi, ekmek
            CreateLevel("World1_Level7", 1, 6, 175f, FOODS_DIR,
                new[]{ new S("Cookies",1,14), new S("Donut",1,12), new S("Croissant",1,10), new S("HeartCookies",1,8), new S("CherryDanish",1), new S("Toast",1), new S("Pretzel",1), new S("Bread",1), new S("Baguette",1), new S("Cupcake",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Cookies",54), new O("Croissant",30), new O("Donut",30) }, null, 0),
            // L8 — Suşi bar (bombalar başlar)
            CreateLevel("World1_Level8", 1, 7, 180f, FOODS_DIR,
                new[]{ new S("Sushi",1,14), new S("Nigiri",1,12), new S("EbiNigiri",1,10), new S("SalmonNigiri",1,8), new S("Salmon",1), new S("Ramen",1), new S("BentoBox",1), new S("Kebab",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Nigiri",48), new O("Sushi",42), new O("SalmonNigiri",24) }, bomb, 3),
            // L9 — Izgara & burger
            CreateLevel("World1_Level9", 1, 8, 185f, FOODS_DIR,
                new[]{ new S("Hamburger",1,14), new S("Cheeseburger",1,12), new S("HotDog",1,10), new S("Sausage",1,8), new S("TowerBurger",1), new S("FrenchFries",1), new S("Steak",1), new S("SteakPlate",1), new S("Kebab",1), new S("Skewer",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Hamburger",48), new O("HotDog",36), new O("Sausage",30) }, bomb, 4),
            // L10 — Dünya mutfağı
            CreateLevel("World1_Level10", 1, 9, 190f, FOODS_DIR,
                new[]{ new S("Taco",1,12), new S("PizzaSlice",1,12), new S("Khachapuri",1,10), new S("Tamales",1,8), new S("Kibbeh",1), new S("PaneerTikka",1), new S("MargheritaPizza",1), new S("WholePizza",1), new S("Kebab",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Taco",42), new O("PizzaSlice",42), new O("Khachapuri",24) }, bomb, 4),
            // L11 — Tatlı festivali (macaron roketi tetiklenir)
            CreateLevel("World1_Level11", 1, 10, 195f, FOODS_DIR,
                new[]{ new S("MacaronPink",1,10), new S("MacaronBlue",1,10), new S("MacaronGreen",1,10), new S("MacaronYellow",1,10), new S("MacaronPurple",1,10), new S("Cupcake",1), new S("IceCream",1), new S("Donut",1), new S("Cookies",1), new S("HeartCookies",1), new S("CherryDanish",1), new S("Pie",1), new S("Tray",1) },
                new[]{ new O("MacaronYellow",90), new O("MacaronPink",70), new O("IceCream",30) }, bomb, 5),
            // L12 — Sebze bahçesi
            CreateLevel("World1_Level12", 1, 11, 195f, FOODS_DIR,
                new[]{ new S("Broccoli",1,14), new S("Tomato",1,12), new S("Cucumber",1,10), new S("Eggplant",1,8), new S("VeggiePlatter",1), new S("Lettuce",1), new S("Pumpkin",1), new S("PalmFruit",1), new S("Apple",1), new S("Pear",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Broccoli",48), new O("Tomato",42), new O("Cucumber",30) }, bomb, 5),
            // L13 — Deniz ürünleri
            CreateLevel("World1_Level13", 1, 12, 200f, FOODS_DIR,
                new[]{ new S("Salmon",1,12), new S("SalmonNigiri",1,12), new S("EbiNigiri",1,10), new S("Nigiri",1,10), new S("Sushi",1,8), new S("Ramen",1), new S("BentoBox",1), new S("Kebab",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Salmon",36), new O("Nigiri",48), new O("EbiNigiri",36) }, bomb, 5),
            // L14 — Sokak lezzetleri
            CreateLevel("World1_Level14", 1, 13, 200f, FOODS_DIR,
                new[]{ new S("HotDog",1,12), new S("Kebab",1,12), new S("Skewer",1,10), new S("Popcorn",1,10), new S("Sausage",1,8), new S("PaneerTikka",1), new S("Taco",1), new S("FrenchFries",1), new S("Pretzel",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("Kebab",48), new O("HotDog",42), new O("Popcorn",30) }, bomb, 6),
            // L15 — Pizza partisi (masa: Kebab + WholePizza)
            CreateLevel("World1_Level15", 1, 14, 205f, FOODS_DIR,
                new[]{ new S("PizzaSlice",1,14), new S("MargheritaPizza",1,12), new S("WholePizza",1,10), new S("Khachapuri",1,8), new S("Hamburger",1), new S("Cheeseburger",1), new S("Kebab",1), new S("ChickenSalad",1), new S("MacaronPink",1), new S("MacaronBlue",1), new S("MacaronGreen",1), new S("MacaronYellow",1), new S("MacaronPurple",1), new S("Tray",1) },
                new[]{ new O("PizzaSlice",54), new O("MargheritaPizza",36), new O("WholePizza",24) }, bomb, 6),
        };   // Foods 15 level (2026-07-24: L16-20 kaldırıldı; özel yapılar L1-5'te korundu, L15 finali+hard)

        // ── DÜNYA 2 = ARABALAR (20 level) ────────────────────────────────────────────────────────
        // Arabalar yiyeceklerden ~2× büyük → kule yüksekliği (stack) DÜŞÜK (5-6): 8 simetrik kule ~13-16
        // birim (çerçeve/kamerayı aşmaz). İlk 3 giriş = kule/hedef türleri; gerisi dolu halka (stack 1).
        // Zor level (L10, L20) otomatik: süre yarı (GameManager), bombalar gizli (PlaceBombs), kurukafa intro.
        var cars = new[]
        {
            // L1 — Şehir Sedanları (kolay, bombasız)
            CreateLevel("World2_Level1", 2, 0, 180f, CARS_DIR,
                new[]{ new S("AzureClassicSedan",1,6), new S("BlueFiatSedan",1,5), new S("RedPeugeotCity",1,5), new S("SilverBMWSeries",1), new S("BurgundyHyundaiSona",1), new S("HondaAccordMaestro",1), new S("White1980sCorolla",1), new S("SilverMiniCooper",1), new S("Smart",1), new S("BlueElegance",1), new S("RenaultMenageIv",1), new S("BlueCompactHatchbac",1), new S("CoastalConvertibleO",1) },
                new[]{ new O("AzureClassicSedan",24), new O("BlueFiatSedan",18) }, null, 0),
            // L2 — Klasik Arabalar (bombasız)
            CreateLevel("World2_Level2", 2, 1, 175f, CARS_DIR,
                new[]{ new S("BlueElegance",1,6), new S("RedElegance",1,5), new S("RusticLegacy",1,5), new S("Gangster",1), new S("BlueBlowerClassic",1), new S("BlowerBouleva",1), new S("CrimsonChromeCruise",1), new S("PastelTwoTone",1), new S("FordEscortRs",1), new S("BmwM3E46",1), new S("HondaCivicEJ2",1), new S("RustyRelic",1) },
                new[]{ new O("BlueElegance",24), new O("RedElegance",20), new O("RusticLegacy",16) }, null, 0),
            // L3 — Spor Arabalar (bombalar başlar)
            CreateLevel("World2_Level3", 2, 2, 175f, CARS_DIR,
                new[]{ new S("OrangeLamborghiniRe",1,6), new S("PorscheGT3RS",1,5), new S("SunburstVeyron",1,5), new S("EmeraldMGRoadster",1), new S("MidnightShadowGT",1), new S("CrimsonVelocity",1), new S("EmeraldVelocity",1), new S("SunsetVelocity",1), new S("SilverVelocityRoads",1), new S("KoenigseggAgeraRS",1), new S("PorscheVie",1), new S("SpeedXXTrail",1) },
                new[]{ new O("OrangeLamborghiniRe",26), new O("PorscheGT3RS",20), new O("SunburstVeyron",16) }, bomb, 3),
            // L4 — Kas Arabaları
            CreateLevel("World2_Level4", 2, 3, 170f, CARS_DIR,
                new[]{ new S("SpikedRustMuscle",1,6), new S("SunfireMuscleCar",1,5), new S("MidnightCopper",1,5), new S("DesertBronzeGTO",1), new S("Gangster",1), new S("CrimsonChromeCruise",1), new S("BlueBlowerClassic",1), new S("BlowerBouleva",1), new S("RustyRelic",1), new S("RusticLegacy",1), new S("RustyYellowTaxi",1) },
                new[]{ new O("SpikedRustMuscle",26), new O("SunfireMuscleCar",20), new O("MidnightCopper",16) }, bomb, 4),
            // L5 — Yarış Pisti
            CreateLevel("World2_Level5", 2, 4, 165f, CARS_DIR,
                new[]{ new S("BlueRallyRacer",1,6), new S("LimeGreenRacing",1,5), new S("NeonFlameRacer",1,5), new S("MaddyBlueRacer",1), new S("BlueBlazeFormula",1), new S("LimeLightningCorvet",1), new S("NeonPinkSpeed",1), new S("PeugeotRacing",1), new S("OrangeRacer",1), new S("AeroWingGTRacer",1) },
                new[]{ new O("BlueRallyRacer",26), new O("LimeGreenRacing",22), new O("NeonFlameRacer",18) }, bomb, 5),
            // L6 — Vintage Garaj (kolay reset, bombasız)
            CreateLevel("World2_Level6", 2, 5, 175f, CARS_DIR,
                new[]{ new S("RustyRelic",1,6), new S("RustyAbandonedDamag",1,5), new S("RusticLegacy",1,5), new S("Gangster",1), new S("FordEscortRs",1), new S("White1980sCorolla",1), new S("BlueFiatSedan",1), new S("RustyYellowTaxi",1), new S("HondaCivicEJ2",1), new S("GraffitiVanSunli",1), new S("PastelTwoTone",1) },
                new[]{ new O("RustyRelic",24), new O("RusticLegacy",18) }, null, 0),
            // L7 — SUV & Arazi (bombasız)
            CreateLevel("World2_Level7", 2, 6, 175f, CARS_DIR,
                new[]{ new S("LandCruiser",1,6), new S("AzureOffRoad",1,5), new S("CreamTRXCloudy",1,5), new S("WinterTrailWarrior",1), new S("WhitePorscheMacan",1), new S("EmeraldHauler",1), new S("GraffitiVanSunli",1), new S("PurpleLibertyWalk",1), new S("LowPolyAll",1) },
                new[]{ new O("LandCruiser",22), new O("AzureOffRoad",20), new O("CreamTRXCloudy",14) }, null, 0),
            // L8 — Süper Arabalar
            CreateLevel("World2_Level8", 2, 7, 180f, CARS_DIR,
                new[]{ new S("KoenigseggAgeraRS",1,6), new S("SunburstVeyron",1,5), new S("OrangeLamborghiniRe",1,5), new S("SilverHypercar",1), new S("LumenTreadHypercar",1), new S("NeonNightHypercar",1), new S("CrystalVelocityConc",1), new S("PorscheGT3RS",1), new S("AeroWingGTRacer",1), new S("MidnightShadowGT",1) },
                new[]{ new O("KoenigseggAgeraRS",26), new O("SunburstVeyron",22), new O("SilverHypercar",16) }, bomb, 3),
            // L9 — Formula & F1
            CreateLevel("World2_Level9", 2, 8, 185f, CARS_DIR,
                new[]{ new S("PetronasMercedesF1",1,6), new S("BlueBlazeFormula",1,5), new S("F1",1,5), new S("BlueRallyRacer",1), new S("LimeGreenRacing",1), new S("NeonFlameRacer",1), new S("NeonPinkSpeed",1), new S("MaddyBlueRacer",1), new S("PeugeotRacing",1), new S("LimeLightningCorvet",1) },
                new[]{ new O("PetronasMercedesF1",26), new O("BlueBlazeFormula",22), new O("F1",16) }, bomb, 4),
            // L10 — ZOR: Kaos Trafiği (yarı süre + gizli bomba + kurukafa OTOMATİK)
            CreateLevel("World2_Level10", 2, 9, 190f, CARS_DIR,
                new[]{ new S("KoenigseggAgeraRS",1,6), new S("SpikedRustMuscle",1,5), new S("CyberpunkPoliceCar",1,5), new S("LandCruiser",1), new S("NeonNightDrifter",1), new S("OrangeLamborghiniRe",1), new S("PetronasMercedesF1",1), new S("SunburstVeyron",1), new S("AzureClassicSedan",1), new S("FerrariGTOIcon",1), new S("SilverHypercar",1) },
                new[]{ new O("KoenigseggAgeraRS",22), new O("SpikedRustMuscle",18), new O("CyberpunkPoliceCar",12) }, bomb, 5),
            // L11 — Neon Gece
            CreateLevel("World2_Level11", 2, 10, 190f, CARS_DIR,
                new[]{ new S("NeonNightDrifter",1,6), new S("NeonNightHypercar",1,5), new S("CrimsonNeonRain",1,5), new S("NeonPinkSpeed",1), new S("NeonFlameRacer",1), new S("CyberpunkPoliceCar",1), new S("AuroraOne",1), new S("YellowThunder",1), new S("YellowCometEV",1), new S("BlueBMWOv",1) },
                new[]{ new O("NeonNightDrifter",26), new O("NeonNightHypercar",22), new O("CrimsonNeonRain",18) }, bomb, 5),
            // L12 — Alman Gücü (BMW & Porsche)
            CreateLevel("World2_Level12", 2, 11, 190f, CARS_DIR,
                new[]{ new S("SilverBMWSeries",1,6), new S("CrimsonClassicBMW",1,5), new S("BmwM3E46",1,5), new S("BlueBMWOv",1), new S("WhitePorscheMacan",1), new S("PorscheGT3RS",1), new S("PorscheVie",1), new S("MidnightShadowGT",1), new S("PetronasMercedesF1",1) },
                new[]{ new O("SilverBMWSeries",26), new O("CrimsonClassicBMW",22), new O("BmwM3E46",18) }, bomb, 5),
            // L13 — İtalyan Tutkusu (Ferrari & Lambo)
            CreateLevel("World2_Level13", 2, 12, 195f, CARS_DIR,
                new[]{ new S("FerrariGTOIcon",1,6), new S("OrangeLamborghiniRe",1,5), new S("FerrariStretchLimou",1,5), new S("EspanaFlameBull",1), new S("SunburstVeyron",1), new S("EmeraldMGRoadster",1), new S("CrimsonVelocity",1), new S("CrimsonInferno",1), new S("MidnightShadowGT",1) },
                new[]{ new O("FerrariGTOIcon",26), new O("OrangeLamborghiniRe",22), new O("FerrariStretchLimou",14) }, bomb, 5),
            // L14 — Elektrikli Gelecek
            CreateLevel("World2_Level14", 2, 13, 195f, CARS_DIR,
                new[]{ new S("YellowCometEV",1,6), new S("AuroraOne",1,5), new S("LumenTreadHypercar",1,5), new S("CrystalVelocityConc",1), new S("NeonNightHypercar",1), new S("SilverHypercar",1), new S("GildedVelocity",1), new S("ArgentineSunburstVi",1), new S("AeroWingGTRacer",1) },
                new[]{ new O("YellowCometEV",26), new O("AuroraOne",22), new O("LumenTreadHypercar",18) }, bomb, 6),
            // L15 — Polis & Özel Araçlar
            CreateLevel("World2_Level15", 2, 14, 200f, CARS_DIR,
                new[]{ new S("CyberpunkPoliceCar",1,6), new S("CyberpunkPoliceCar2",1,5), new S("BatmobileRCCar",1,5), new S("RustyYellowTaxi",1), new S("PurpleLibertyWalk",1), new S("GraffitiVanSunli",1), new S("EmeraldHauler",1), new S("SpikedRustMuscle",1), new S("NeonNightDrifter",1) },
                new[]{ new O("CyberpunkPoliceCar",24), new O("BatmobileRCCar",16), new O("RustyYellowTaxi",18) }, bomb, 6),
        };   // Cars 15 level (2026-07-24: L16-20 kaldırıldı; kapsanmayan arabalar EnsureAllCarsCovered ile eklenir)

        EnsureAllCarsCovered(cars);   // TÜM araba prefabları en az 1 levelda görünsün (çeşitlilik)

        // ── DÜNYA 3 = BİNALAR (20 level, GENERIC üretici — 96 binayı otomatik dağıtır) ──
        var buildings = BuildBuildingWorld(bomb);

        // ── DÜNYA 4 = TATLILAR (20 level, GENERIC üretici — tatlıları otomatik dağıtır) ──
        var sweets = BuildGenericWorld("Sweets", 4, SWEETS_DIR, bomb, baseReqLo: 14, baseReqHi: 30);

        // ── DÜNYA 5 = İÇECEKLER (20 level, GENERIC üretici) ──
        var drinks = BuildGenericWorld("İçecekler", 5, DRINKS_DIR, bomb, baseReqLo: 12, baseReqHi: 26);

        // ── DÜNYA 6 = HEDİYELER (20 level) ──
        var gifts = BuildGenericWorld("Hediyeler", 6, GIFTS_DIR, bomb, baseReqLo: 12, baseReqHi: 26);

        // ── DÜNYA 7 = KİTAPLAR (20 level) ──
        var books = BuildGenericWorld("Kitaplar", 7, BOOKS_DIR, bomb, baseReqLo: 12, baseReqHi: 26);

        // ── DÜNYA 9 = KEDİLER (20 level) — animasyonlu Cat.prefab (çok büyük) hedef olmasın diye hariç.
        var cats = BuildGenericWorld("Kediler", 9, CATS_DIR, bomb, baseReqLo: 12, baseReqHi: 26,
            exclude: new HashSet<string> { "Cat" });

        // ── DÜNYA 10 = KÖPEKLER (20 level) ──
        var dogs = BuildGenericWorld("Köpekler", 10, DOGS_DIR, bomb, baseReqLo: 12, baseReqHi: 26);

        // ── DÜNYA 11 = GEMİLER (20 level) ──
        var ships = BuildGenericWorld("Gemiler", 11, SHIPS_DIR, bomb, baseReqLo: 12, baseReqHi: 26);

        // ── DÜNYA 12 = UÇAKLAR ──
        var planes = BuildGenericWorld("Uçaklar", 12, PLANES_DIR, bomb, baseReqLo: 12, baseReqHi: 26);

        // ── DÜNYA 13 = HAZİNE (Para + Altın birleşik) ──
        var money = BuildGenericWorld("Hazine", 13, MONEY_DIR, bomb, baseReqLo: 12, baseReqHi: 26);

        // ── DÜNYA 15 = MÜCEVHER ──
        var jewelry = BuildGenericWorld("Mücevher", 15, JEWELRY_DIR, bomb, baseReqLo: 12, baseReqHi: 26);

        // ── DÜNYA 17 = KARMA (final): ilk 14 dünyadan 20'şer prefab karıştırılır ──
        var mixed = BuildMixedWorld(bomb);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WireScene(park, foods, cars, buildings, sweets, drinks, gifts, books, cats, dogs, ships, planes, money, jewelry, mixed);
        Debug.Log("[WorldContent] 15 içerikli dünya (Park + 13 tematik + Karma, 15'er level) üretildi, LevelManager.worlds bağlandı.");
    }

    static LevelData CreateLevel(string name, int world, int index, float time, string prefabDir,
        S[] spawns, O[] objectives, GameObject bomb, int bombCount)
    {
        string path = $"{LEVELS_DIR}/{name}.asset";
        var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<LevelData>();

        data.worldId = world;
        data.levelIndex = index;
        data.levelTime = time;

        data.spawns = new List<LevelData.SpawnEntry>();
        foreach (var s in spawns)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabDir}/{s.t}.prefab");
            if (pf == null) { Debug.LogWarning($"[WorldContent] prefab yok: {prefabDir}/{s.t}"); continue; }
            data.spawns.Add(new LevelData.SpawnEntry { prefab = pf, count = s.c, stack = s.k, scale = s.sc });
        }
        AddPowerUps(data);   // her gameplay level'ına az sayıda güç-up (2 büyütme, 2 hız, 1 mıknatıs)

        data.objectives = new List<LevelData.ObjectiveEntry>();
        foreach (var o in objectives)
            data.objectives.Add(new LevelData.ObjectiveEntry { objectType = o.t, required = o.r });

        data.bombsEnabled = bomb != null && bombCount > 0;
        data.bombPrefab = bomb;
        data.bombCount = bombCount;

        if (isNew) AssetDatabase.CreateAsset(data, path);
        else EditorUtility.SetDirty(data);
        return data;
    }

    // TÜM araba prefablarının en az bir cars-level'ında görünmesini GARANTİLER (çeşitlilik). Temalı seçimleri
    // korur; sadece hiç kullanılmayanları round-robin ile levellara ekler (stack 1, dolgu — hedef değil).
    static void EnsureAllCarsCovered(LevelData[] cars)
    {
        var allNames = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { CARS_DIR }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(CARS_DIR + "/") && path.EndsWith(".prefab"))
                allNames.Add(System.IO.Path.GetFileNameWithoutExtension(path));
        }
        allNames.Sort();

        var covered = new HashSet<string>();
        foreach (var lv in cars)
            foreach (var s in lv.spawns)
                if (s.prefab != null) covered.Add(s.prefab.name);

        int li = 0, added = 0;
        foreach (var name in allNames)
        {
            if (covered.Contains(name)) continue;
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{CARS_DIR}/{name}.prefab");
            if (pf == null) continue;
            var lv = cars[li % cars.Length];
            lv.spawns.Add(new LevelData.SpawnEntry { prefab = pf, count = 1, stack = 1, scale = 1f });
            EditorUtility.SetDirty(lv);
            li++; added++;
        }
        Debug.Log($"[WorldContent] Araba kapsama: {allNames.Count} arabadan {added}'i kapsanmıyordu → levellara eklendi (hepsi kapsandı).");
    }

    // DÜNYA 3 = BİNALAR: 96 binayı 20 levele OTOMATİK dağıtır (elle isim yazmadan). Her level ~13 bina türü
    // (kayan pencere → 20 levelda hepsi kapsanır, çeşitlilik), hedefler otomatik (binalar büyük → düşük adet),
    // artan zorluk (süre ↓, hedef ↑, bomba). Binalar YÜKSEK → kule (stack) YOK, hepsi dolu halka (stack 1).
    static LevelData[] BuildBuildingWorld(GameObject bomb)
        => BuildGenericWorld("Binalar", 3, BUILDINGS_DIR, bomb, 10, 22);

    const int LEVELS_PER_WORLD = 15;   // 2026-07-24: dünya başına 20→15 (kullanıcı: daha az tekrar, oyuncu odaklı)

    // GENERIC dünya üreticisi: bir prefab klasöründeki TÜM nesneleri 15 levele OTOMATİK dağıtır (elle isim
    // yazmadan). Kayan pencere (adım 5, genişlik ≤13) çeşit+tekrar verir; sonda KAPSAMA GARANTİSİ ile
    // kayan-pencerenin atladığı prefablar round-robin eklenir (N>~88'de pencere tek başına kapsayamaz).
    // Hedefler otomatik, artan zorluk (süre ↓, hedef ↑, bomba). baseReqLo/Hi: hedef adedi eğrisi. Tüm entry stack 1.
    static LevelData[] BuildGenericWorld(string worldName, int worldId, string prefabDir, GameObject bomb,
        int baseReqLo, int baseReqHi, HashSet<string> exclude = null)
    {
        var names = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(prefabDir + "/") && path.EndsWith(".prefab"))
            {
                string nm = System.IO.Path.GetFileNameWithoutExtension(path);
                if (exclude != null && exclude.Contains(nm)) continue;   // örn. animasyonlu Cat (çok büyük → hedef olamaz)
                names.Add(nm);
            }
        }
        names.Sort();
        var levels = new LevelData[LEVELS_PER_WORLD];
        int N = names.Count;
        if (N == 0) { Debug.LogWarning($"[WorldContent] {worldName} prefab yok — önce ilgili Create ... çalıştır."); return levels; }

        int width = Mathf.Min(13, N);
        int denom = LEVELS_PER_WORLD - 1;
        for (int i = 0; i < LEVELS_PER_WORLD; i++)
        {
            var uniq = new List<string>(); var seen = new HashSet<string>();
            for (int j = 0; j < width; j++)
            {
                string nm = names[(i * 5 + j) % N];
                if (seen.Add(nm)) uniq.Add(nm);
            }

            var spawns = new List<S>();
            foreach (var nm in uniq) spawns.Add(new S(nm, 1, 1, 1f));

            float f = i / (float)denom;
            int baseReq = Mathf.RoundToInt(Mathf.Lerp(baseReqLo, baseReqHi, f));
            var objs = new List<O> { new O(uniq[0], baseReq) };
            if (uniq.Count > 1) objs.Add(new O(uniq[1], Mathf.RoundToInt(baseReq * 0.7f)));
            if (i >= 4 && uniq.Count > 2) objs.Add(new O(uniq[2], Mathf.RoundToInt(baseReq * 0.55f)));

            float time = Mathf.Lerp(185f, 150f, f);
            // Zor level (5/10/15 → index 4/9/14): bombalar zaten IsHard ile gizli+bol; normal levellarda kademeli.
            int bombCount = (i >= 2 && bomb != null) ? Mathf.Clamp(2 + i / 2, 2, 9) : 0;

            levels[i] = CreateLevel($"World{worldId}_Level{i + 1}", worldId, i, time, prefabDir,
                spawns.ToArray(), objs.ToArray(), (bombCount > 0 ? bomb : null), bombCount);
        }

        EnsureAllCovered(levels, prefabDir, exclude);   // kayan-pencerenin atladığı prefabları levellara serpiştir
        Debug.Log($"[WorldContent] {worldName}: {N} nesne → {LEVELS_PER_WORLD} level (tümü kapsandı, generic dağıtım).");
        return levels;
    }

    // Bir klasördeki TÜM prefabların en az bir levelda görünmesini GARANTİLER (çeşitlilik). Kapsanmayanları
    // round-robin ile levellara ekler (stack 1 dolgu — hedef değil). EnsureAllCarsCovered'ın genelleştirilmişi.
    static void EnsureAllCovered(LevelData[] levels, string prefabDir, HashSet<string> exclude)
    {
        var allNames = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { prefabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(prefabDir + "/") && path.EndsWith(".prefab"))
            {
                string nm = System.IO.Path.GetFileNameWithoutExtension(path);
                if (exclude != null && exclude.Contains(nm)) continue;
                allNames.Add(nm);
            }
        }
        allNames.Sort();

        var covered = new HashSet<string>();
        foreach (var lv in levels)
            if (lv != null)
                foreach (var s in lv.spawns)
                    if (s.prefab != null) covered.Add(s.prefab.name);

        int li = 0, added = 0;
        foreach (var name in allNames)
        {
            if (covered.Contains(name)) continue;
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{prefabDir}/{name}.prefab");
            if (pf == null) continue;
            var lv = levels[li % levels.Length];
            if (lv == null) { li++; continue; }
            lv.spawns.Add(new LevelData.SpawnEntry { prefab = pf, count = 1, stack = 1, scale = 1f });
            EditorUtility.SetDirty(lv);
            li++; added++;
        }
        if (added > 0) Debug.Log($"[WorldContent] Kapsama: {prefabDir} → {added} prefab kayan-pencerede yoktu, levellara eklendi.");
    }

    // KARMA (worldId 17, final): ilk 14 dünyanın prefab klasörlerinden 20'şer prefab alıp KARIŞTIRARAK 15 level
    // üretir. Dünyalar arası serpiştirme (round-robin) → her level çeşitli dünyalardan nesne içerir. Farklı
    // dizinlerden prefab → doğrudan prefab-referanslı level (CreateLevelDirect). Hard 5/10/15 otomatik (IsHard).
    static LevelData[] BuildMixedWorld(GameObject bomb)
    {
        // Order'daki ilk 14 dünyanın prefab dizinleri (Park dahil; Karma hariç). Cats'ten animasyonlu Cat hariç.
        var sources = new (string dir, string exclude)[]
        {
            (FOODS_DIR, null), (CARS_DIR, null), (BUILDINGS_DIR, null), (SWEETS_DIR, null), (DRINKS_DIR, null),
            (GIFTS_DIR, null), (BOOKS_DIR, null), (CATS_DIR, "Cat"), (DOGS_DIR, null), (SHIPS_DIR, null),
            (PLANES_DIR, null), (MONEY_DIR, null), (JEWELRY_DIR, null), (DECO_DIR, null),
        };

        // Her dünyadan 20 prefab (eşit aralıklı örnekleme → çeşit); sonra dünyalar arası round-robin serpiştir.
        var perWorld = new List<List<GameObject>>();
        foreach (var (dir, exclude) in sources)
        {
            var names = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { dir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(dir + "/") || !path.EndsWith(".prefab")) continue;
                string nm = System.IO.Path.GetFileNameWithoutExtension(path);
                if (exclude != null && nm == exclude) continue;
                names.Add(nm);
            }
            names.Sort();
            var picked = new List<GameObject>();
            int take = Mathf.Min(20, names.Count);
            for (int k = 0; k < take; k++)
            {
                int idx = (names.Count <= 20) ? k : Mathf.RoundToInt(k * (names.Count - 1) / (float)(take - 1));
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{names[idx]}.prefab");
                if (pf != null) picked.Add(pf);
            }
            if (picked.Count > 0) perWorld.Add(picked);
        }

        // Round-robin serpiştir (foods0,cars0,...,foods1,cars1,...) → ardışık pencere = çok-dünya karışımı.
        var pool = new List<GameObject>();
        int maxLen = 0; foreach (var l in perWorld) maxLen = Mathf.Max(maxLen, l.Count);
        for (int k = 0; k < maxLen; k++)
            foreach (var l in perWorld) if (k < l.Count) pool.Add(l[k]);

        var levels = new LevelData[LEVELS_PER_WORLD];
        int N = pool.Count;
        if (N == 0) { Debug.LogWarning("[WorldContent] Karma: havuz boş."); return levels; }

        int width = Mathf.Min(16, N);
        int step = Mathf.Max(1, N / LEVELS_PER_WORLD);   // pencereyi tüm havuza yay → çeşit + kapsama
        int denom = LEVELS_PER_WORLD - 1;
        for (int i = 0; i < LEVELS_PER_WORLD; i++)
        {
            var uniq = new List<GameObject>(); var seen = new HashSet<GameObject>();
            for (int j = 0; j < width; j++)
            {
                var pf = pool[(i * step + j) % N];
                if (seen.Add(pf)) uniq.Add(pf);
            }

            float f = i / (float)denom;
            int baseReq = Mathf.RoundToInt(Mathf.Lerp(14, 26, f));
            var spawns = new List<(GameObject, int)>();
            foreach (var pf in uniq) spawns.Add((pf, 1));
            var objs = new List<(string, int)> { (uniq[0].name, baseReq) };
            if (uniq.Count > 1) objs.Add((uniq[1].name, Mathf.RoundToInt(baseReq * 0.7f)));
            if (i >= 4 && uniq.Count > 2) objs.Add((uniq[2].name, Mathf.RoundToInt(baseReq * 0.55f)));

            float time = Mathf.Lerp(185f, 150f, f);
            int bombCount = (i >= 2 && bomb != null) ? Mathf.Clamp(2 + i / 2, 2, 9) : 0;
            levels[i] = CreateLevelDirect($"World17_Level{i + 1}", 17, i, time, spawns, objs, (bombCount > 0 ? bomb : null), bombCount);
        }
        Debug.Log($"[WorldContent] Karma: {perWorld.Count} dünyadan {N} prefab havuzu → {LEVELS_PER_WORLD} karışık level.");
        return levels;
    }

    // CreateLevel'ın prefab-REFERANSLI varyantı (Karma: prefablar farklı dizinlerde, string-lookup yapılamaz).
    static LevelData CreateLevelDirect(string name, int world, int index, float time,
        List<(GameObject prefab, int stack)> spawns, List<(string type, int req)> objectives, GameObject bomb, int bombCount)
    {
        string path = $"{LEVELS_DIR}/{name}.asset";
        var data = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        bool isNew = data == null;
        if (isNew) data = ScriptableObject.CreateInstance<LevelData>();

        data.worldId = world;
        data.levelIndex = index;
        data.levelTime = time;

        data.spawns = new List<LevelData.SpawnEntry>();
        foreach (var (prefab, stack) in spawns)
            if (prefab != null) data.spawns.Add(new LevelData.SpawnEntry { prefab = prefab, count = 1, stack = stack, scale = 1f });
        AddPowerUps(data);

        data.objectives = new List<LevelData.ObjectiveEntry>();
        foreach (var (type, req) in objectives)
            data.objectives.Add(new LevelData.ObjectiveEntry { objectType = type, required = req });

        data.bombsEnabled = bomb != null && bombCount > 0;
        data.bombPrefab = bomb;
        data.bombCount = bombCount;

        if (isNew) AssetDatabase.CreateAsset(data, path);
        else EditorUtility.SetDirty(data);
        return data;
    }

    static void AddPowerUps(LevelData d)
    {
        TryAddPow(d, "PowerGrow", 1);   // genişletme: 2→1 (kullanıcı)
        TryAddPow(d, "PowerSpeed", 2);
        TryAddPow(d, "PowerMagnet", 1);
    }
    static void TryAddPow(LevelData d, string name, int count)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{POWERUPS_DIR}/{name}.prefab");
        if (pf != null) d.spawns.Add(new LevelData.SpawnEntry { prefab = pf, count = count, stack = 1, scale = 1f });
        else Debug.LogWarning($"[WorldContent] güç-up yok: {name} (önce Create PowerUps çalıştır)");
    }

    static void WireScene(LevelData[] park, LevelData[] foods, LevelData[] cars, LevelData[] buildings, LevelData[] sweets, LevelData[] drinks,
        LevelData[] gifts, LevelData[] books, LevelData[] cats, LevelData[] dogs, LevelData[] ships, LevelData[] planes,
        LevelData[] money, LevelData[] jewelry, LevelData[] mixed)
    {
        var deco = GameObject.Find("_DecoGroup");
        if (deco != null) Object.DestroyImmediate(deco);

        var lmGo = GameObject.Find("LevelManager");
        if (lmGo == null) lmGo = new GameObject("LevelManager");
        var lm = lmGo.GetComponent<LevelManager>();
        if (lm == null) lm = lmGo.AddComponent<LevelManager>();

        var foodGround = LoadGroundTexture("Assets/Art/marble_countertop_tile.png");
        var sweetGround = LoadGroundTexture("Assets/Art/sweets_ground_tile.png");        // pastel krema+şeker deseni
        var carsGround = LoadGroundTexture("Assets/Art/cars_ground_tile.png");           // koyu asfalt + yol çizgileri
        var buildingsGround = LoadGroundTexture("Assets/Art/buildings_ground_tile.png"); // açık taş/parke döşeme
        var drinksGround = LoadGroundTexture("Assets/Art/drinks_ground_tile.png");        // içecekler zemini
        var giftsGround = LoadGroundTexture("Assets/Art/gifts_ground_tile.png");          // hediyeler zemini
        var booksGround = LoadGroundTexture("Assets/Art/books_ground_tile.png");          // kitaplar zemini
        var catsGround = LoadGroundTexture("Assets/Art/cats_ground_tile.png");            // kediler zemini
        var dogsGround = LoadGroundTexture("Assets/Art/dogs_ground_tile.png");            // köpekler zemini
        var shipsGround = LoadGroundTexture("Assets/Art/ships_ground_tile.png");          // gemiler zemini (su)
        var planesGround = LoadGroundTexture("Assets/Art/planes_ground_tile.png");        // uçaklar zemini (gökyüzü/pist)
        var moneyGround = LoadGroundTexture("Assets/Art/gold_ground_tile.png");           // Hazine zemini (altın)
        var jewelryGround = LoadGroundTexture("Assets/Art/jewels_ground_tile.png");       // Mücevher zemini
        var mixedGround = LoadGroundTexture("Assets/Art/mixed_ground_tile.png");           // Karma zemini

        lm.worlds = new[]
        {
            new LevelManager.WorldLevelSet { worldId = 0, levels = park,
                groundTexture = null, groundTint = new Color(0.46f, 0.52f, 0.40f), groundTile = 6f },   // Park: yeşil zemin
            new LevelManager.WorldLevelSet { worldId = 1, levels = foods,
                groundTexture = foodGround, groundTint = Color.white, groundTile = 14f },               // Yiyecekler: ahşap/yeni zemin (büyük sahne)
            new LevelManager.WorldLevelSet { worldId = 2, levels = cars,
                groundTexture = carsGround, groundTint = Color.white, groundTile = 15f },   // Arabalar: asfalt + yol çizgisi deseni
            new LevelManager.WorldLevelSet { worldId = 3, levels = buildings,
                groundTexture = buildingsGround, groundTint = Color.white, groundTile = 14f },   // Binalar: taş/parke döşeme
            new LevelManager.WorldLevelSet { worldId = 4, levels = sweets,
                groundTexture = sweetGround, groundTint = Color.white, groundTile = 14f },   // Tatlılar: pastel krema+şeker deseni (kendi rengi çıksın → beyaz tint)
            new LevelManager.WorldLevelSet { worldId = 5, levels = drinks,
                groundTexture = drinksGround, groundTint = Color.white, groundTile = 14f },   // İçecekler
            new LevelManager.WorldLevelSet { worldId = 6, levels = gifts,
                groundTexture = giftsGround, groundTint = Color.white, groundTile = 14f },    // Hediyeler
            new LevelManager.WorldLevelSet { worldId = 7, levels = books,
                groundTexture = booksGround, groundTint = Color.white, groundTile = 14f },    // Kitaplar
            new LevelManager.WorldLevelSet { worldId = 9, levels = cats,
                groundTexture = catsGround, groundTint = Color.white, groundTile = 14f },     // Kediler
            new LevelManager.WorldLevelSet { worldId = 10, levels = dogs,
                groundTexture = dogsGround, groundTint = Color.white, groundTile = 14f },     // Köpekler
            new LevelManager.WorldLevelSet { worldId = 11, levels = ships,
                groundTexture = shipsGround, groundTint = Color.white, groundTile = 14f },    // Gemiler
            new LevelManager.WorldLevelSet { worldId = 12, levels = planes,
                groundTexture = planesGround, groundTint = Color.white, groundTile = 14f },   // Uçaklar
            new LevelManager.WorldLevelSet { worldId = 13, levels = money,
                groundTexture = moneyGround, groundTint = Color.white, groundTile = 14f },    // Hazine (Para+Altın)
            new LevelManager.WorldLevelSet { worldId = 15, levels = jewelry,
                groundTexture = jewelryGround, groundTint = Color.white, groundTile = 14f },  // Mücevher
            new LevelManager.WorldLevelSet { worldId = 17, levels = mixed,
                groundTexture = mixedGround, groundTint = Color.white, groundTile = 14f },    // Karma (final)
        };
        lm.levels = park;   // geriye-dönük fallback
        EditorUtility.SetDirty(lm);
        EditorSceneManager.MarkSceneDirty(lmGo.scene);
        EditorSceneManager.SaveScene(lmGo.scene);
    }

    // Zemin PNG'sini Default (tile'lanabilir) texture olarak import et + Texture2D döndür.
    static Texture2D LoadGroundTexture(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[WorldContent] zemin texture yok: {path}"); return null; }
        if (imp.textureType != TextureImporterType.Default || imp.wrapMode != TextureWrapMode.Repeat)
        {
            imp.textureType = TextureImporterType.Default;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureDir(string assetPath)
    {
        string full = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        if (!Directory.Exists(full)) { Directory.CreateDirectory(full); AssetDatabase.Refresh(); }
    }
}
