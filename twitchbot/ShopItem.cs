using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace twitchbot
{
    public class ShopItem
    {
        // items
        private static ShopItem[] items = new ShopItem[]
        {
            // food
            new ShopItem { Name = "apple", Emote = "OpieOP", Description = "A nice and fresh apple. Yum!", Price = 15, Calories = 50, Edible = true, SingularArticle = "an" },
            new ShopItem { Name = "swiftapple", Description = "Looks like a normal apple but tastes a lot better.", Calories = 50, Edible = true },
            new ShopItem { Name = "swiftapple-juice", RecipeString = "1 swiftapple", Description = "Looks like a normal applejuice but tastes a lot sweeter.", Calories = 100, Edible = true },
            new ShopItem { Name = "bacon", Emote = "OpieOP", Description = "Bacon is the most important feature in an online chat bot.", Price = 20, Calories = 120, Edible = true },
            new ShopItem { Name = "cheeseburger", Emote = "OpieOP", Description = "Cheese, patty, bun. Call me cheese, patty, bun man.", Calories = 300, Price = 40, Edible = true },
            new ShopItem { Name = "chickennugget", Emote = "OpieOP", Description = "Small and filled with protein but not fluffy.", Calories = 45, Price = 5, Edible = true },

            new ShopItem { Name = "hamster", Emote = "KKona", Description = "It's small, fluffy and filled with protein.", Calories = 500, Price = 50, Edible = true },
            new ShopItem { Name = "cat", Emote = "CoolCat", Description = "You can be the cat lady of twich chat CoolCat", Calories = 1400, Price = 500, Edible = true },
            ///new ShopItem { Name = "fat-cat", Emote = "OpieOP", Description = "Don't know if CoolCat or OpieOP", Calories = 1400, Price = 500, Edible = true },

            new ShopItem { Name = "dog", Emote = "FrankerZ", Description = "FrankerZ", Calories = 70000, Price = 5000, Edible = true },
            new ShopItem { Name = "vaping-dog", RecipeString = "1 dog 1 vape", Emote = "FrankerZ", Description = "A dog with a vape stuck in one of his nostrils OhMyDog", Calories = 80000, Edible = true },

            // vape
            new ShopItem { Name = "vape", Emote = "VapeNation", Price = 1000, Description = "VapeNation \\//\\" },
            new ShopItem { Name = "liquid", Flags = ShopItemFlags.Liquid, Price = 5, Emote = "VapeNation", Description = "probably only lasts me a couple hours... VapeNation" },
            new ShopItem { Name = "apple-liquid", Flags = ShopItemFlags.Liquid, Emote = "VapeNation", RecipeString = "1 apple 10 liquid", CraftCount = 10,  Description = "looks and tastes like apple juice... VapeNation" },
            new ShopItem { Name = "hamster-liquid", Flags = ShopItemFlags.Liquid, Emote = "VapeNation", RecipeString = "1 hamster 10 liquid", CraftCount = 10, Description = "looks like someput put a hamster into a blender... VapeNation" },
            new ShopItem { Name = "bacon-liquid", Flags = ShopItemFlags.Liquid, Emote = "VapeNation", RecipeString = "1 bacon 10 liquid", Calories = 50, CraftCount = 10, Description = "the only liquid that you can eat... VapeNation" },
            new ShopItem { Name = "roleplayer-liquid", Flags = ShopItemFlags.Liquid, Emote = "VapeNation", RecipeString = "1 roleplayer 10 liquid", CraftCount = 10, Description = "it's a mix of blood and pieces of human meat... VapeNation" },
            new ShopItem { Name = "questionable-liquid", Flags = ShopItemFlags.Liquid, Emote = "VapeNation", RecipeString = "1 question 10 liquid", CraftCount = 10, Description = "not sure what it looks and tastes like... VapeNation" },
            new ShopItem { Name = "cobra-liquid", Flags = ShopItemFlags.Liquid, Emote = "VapeNation", RecipeString = "1 cobra 10 liquid", CraftCount = 10, Description = "it's a green liquid that looks poisonous... VapeNation" },

            // line
            new ShopItem { Name = "line", Description = "Looks like a message that a user wrote in chat." },

            new ShopItem { Name = "negative-cat", Emote = "CoolCat", Description = "Eating it will have negative effects on your stomach.", Calories = -10000, Price = -500, Edible = true },
            new ShopItem { Name = "cobra", Emote = "OSkomodo", Description = "Tunnel snakes rule!", Calories = 1000, Price = -300, Edible = true },

            new ShopItem { Name = "question", Emote = "OMGScoots", Calories = 20, Edible = true },
            new ShopItem { Name = "dankmeme", Emote = "BrokeBack", Description = "kek" },
            new ShopItem { Name = "roleplayer", Description = "One of many swedish roleplayers that stream on twitch" },
            new ShopItem { Name = "slap", Emote = "gachiGASM", Description = "A really nice slap. Maybe you should give it to someone." },
            new ShopItem { Name = "fisting", Emote = "gachiGASM", Description = "Fisting is 300 bucks. Uh, I mean pointz." },
            new ShopItem { Name = "hug", Description = "Fourtf thinks a hug is an item LUL" },
            new ShopItem { Name = "strent", Description ="Karl. What the heck is strent?" },
            new ShopItem { Name = "rekt", Description = "Nice b8 m8 I r8 8/8." },
            new ShopItem { Name = "american-express-black", SingularArticle = "an", Description = "It's an american express credit card. I wonder who it belongs to." },

            new ShopItem { Name = "whip", Emote = "gachiGASM", Price = -2500, Description = "I can hear the whip sounds in my dreams gachiGASM" },

            new ShopItem { Name = "viewbot", Emote = "MrDestructoid", Description = "MrDestructoid Beep Boop MrDestructoid" },

            // ammo + weapons
            new ShopItem { Name = "bullet", Emote = "WutFace", AmmoFor = new[] { "ak47", "pistol" } , Price = -150 },
            new ShopItem { Name = "rocket", Emote = "WutFace", AmmoFor = new[] { "rocketlauncher" } /*, Price = 5000*/ },

            new ShopItem { Name = "pistol", Emote = "WutFace", WeaponFor = new[] { "bullet" } , Price = -3333 },
            new ShopItem { Name = "ak47", Emote = "WutFace", WeaponFor = new[] { "bullet" }, Price = -10000, SingularArticle = "an" },

            //new ShopItem { Name = "line-pistol", RecipeString = "500 lines 1 pistol", Description = "A gun that can fire lines at users. It has a long cooldown." },
            //new ShopItem { Name = "line-gun", RecipeString = "1500 lines 1 ak47", Description = "A gun that can fire lines at users. It has a medium cooldown." },

            new ShopItem { Name = "rocketlauncher", Emote = "WutFace", WeaponFor = new[] { "rocket" } /*, Price = 5000*/ },
            //new ShopItem { Name = "line-canon",  Description = "A gun that can fire lines at users. Has a long cooldown." },
        };

        public static Dictionary<string, ShopItem> Items { get; private set; } = new Dictionary<string, ShopItem>();

        public static ShopItem GetItem(string name)
        {
            ShopItem item;
            if (Items.TryGetValue(name, out item) || Items.TryGetValue(name.TrimEnd('s'), out item))
            {
                return item;
            }
            return null;
        }

        static ShopItem()
        {
            try
            {
                foreach (ShopItem item in items)
                {
                    Items[item.Name] = item;
                }
            }
            catch { }
        }

        string recipeString = null;
        public string RecipeString
        {
            set
            {
                recipeString = value;
                loadedRecipe = false;
            }
        }

        public int CraftCount { get; set; } = 1;

        private List<Tuple<ShopItem, long>> recipe = null;
        bool loadedRecipe = true;

        public List<Tuple<ShopItem, long>> Recipe
        {
            get
            {
                if (!loadedRecipe)
                {
                    if (recipeString == null)
                    {
                        recipe = null;
                    }
                    else
                    {
                        recipe = new List<Tuple<ShopItem, long>>(2);
                        string[] S = recipeString.ToLower().Split();

                        for (int i = 0; i < S.Length; i++)
                        {
                            long count;
                            ShopItem item;

                            if (S.TryGetInt(i++, false, null, out count) && S.TryGetItemOrPointz(i, out item))
                            {
                                recipe.Add(Tuple.Create(item, count));
                            }
                        }
                    }
                    loadedRecipe = true;
                }
                return recipe;
            }
        }

        // variables
        public string Name { get; set; } = null;
        public string Description { get; set; } = "fourtf forgot to add a description LUL";

        public string Emote { get; set; } = "Kappa";

        public int Price { get; set; }

        public bool Edible { get; set; }
        public int Calories { get; set; }

        public bool IsWeapon { get { return WeaponFor != null; } }
        public bool IsAmmo { get { return AmmoFor != null; } }

        public string[] AmmoFor { get; set; } = null;
        public string[] WeaponFor { get; set; } = null;

        public string SingularArticle { get; set; } = "a";

        public ShopItemFlags Flags { get; set; }

        // ctor
        public ShopItem()
        {

        }

        // functions
        public string GetPlural(long count)
        {
            bool single = count == 1 || count == -1;
            return $"{Name}{(single ? "" : ((Name[Name.Length - 1] == 's') ? "'" : "s"))}";
        }
    }

    public enum ShopItemFlags
    {
        None = 0,
        Liquid = 1,
    }
}
