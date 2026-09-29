using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    public enum CharacterId { Milo, Lumi, Kiko, Nori, Pip, Mochi, Yume }
    public enum PersonalPassive { GoldGeneration, DoorHealth, WeaponDamage, BedSpeed, MovementSpeed, UpgradeDiscount, BuildSpeed }

    [Serializable]
    public sealed class CharacterDefinition
    {
        public CharacterId Id;
        public string Name;
        public int UnlockLevel;
        public PersonalPassive Passive;
        public float Bonus;
        public string Description;
        public CharacterDefinition Copy() { return (CharacterDefinition)MemberwiseClone(); }
    }

    public sealed class CharacterCatalog
    {
        private readonly CharacterDefinition[] definitions = new CharacterDefinition[7];
        public CharacterCatalog(CharacterDefinition[] source)
        {
            if (source == null || source.Length != 7) throw new ArgumentException("Exactly seven core characters are required.");
            foreach (var d in source)
            {
                if (d == null || (int)d.Id < 0 || (int)d.Id >= 7 || definitions[(int)d.Id] != null ||
                    (int)d.Passive != (int)d.Id || d.UnlockLevel < 1 || string.IsNullOrWhiteSpace(d.Name) ||
                    !MatchRules.Finite(d.Bonus) || d.Bonus <= 0 || d.Bonus > 1)
                    throw new ArgumentException("Invalid or duplicate character definition.");
                definitions[(int)d.Id] = d.Copy();
            }
            if (definitions[0].UnlockLevel != 1) throw new ArgumentException("Milo must be available at level one.");
        }
        public CharacterDefinition Get(CharacterId id)
        {
            if (!Valid(id)) throw new ArgumentOutOfRangeException("id");
            return definitions[(int)id].Copy();
        }
        public static bool Valid(CharacterId id) { return (int)id >= 0 && (int)id < 7; }
        public static string Key(CharacterId id) { return id.ToString().ToLowerInvariant(); }
        public static bool TryId(string key, out CharacterId id)
        { return Enum.TryParse(key, true, out id) && Valid(id) && Key(id) == (key ?? "").ToLowerInvariant(); }
        public static CharacterDefinition[] Defaults()
        {
            string[] names = { "Milo", "Lumi", "Kiko", "Nori", "Pip", "Mochi", "Yume" };
            int[] levels = { 1, 3, 5, 7, 9, 12, 15 };
            float[] bonuses = { .08f, .15f, .10f, .12f, .12f, .08f, .10f };
            string[] descriptions = {
                "+8% personal gold generation.", "+15% maximum HP for your door.", "+10% damage from your weapons.",
                "Your bed generates gold 12% faster.", "+12% personal movement speed.",
                "8% chance to pay half price for an upgrade.", "Your upgrades build 10% faster."
            };
            var result = new CharacterDefinition[7];
            for (int i = 0; i < 7; i++) result[i] = new CharacterDefinition {
                Id = (CharacterId)i, Name = names[i], UnlockLevel = levels[i], Passive = (PersonalPassive)i,
                Bonus = bonuses[i], Description = descriptions[i]
            };
            return result;
        }
    }

    public sealed class CharacterPassive
    {
        public CharacterId Id { get; private set; }
        public float IncomeMultiplier { get; private set; }
        public float DoorMultiplier { get; private set; }
        public float DamageMultiplier { get; private set; }
        public float MoveMultiplier { get; private set; }
        public float BuildMultiplier { get; private set; }
        public float DiscountChance { get; private set; }
        public CharacterPassive(CharacterDefinition character)
        {
            Id = character.Id;
            IncomeMultiplier = DoorMultiplier = DamageMultiplier = MoveMultiplier = BuildMultiplier = 1;
            switch (character.Passive)
            {
                case PersonalPassive.GoldGeneration:
                case PersonalPassive.BedSpeed: IncomeMultiplier += character.Bonus; break;
                case PersonalPassive.DoorHealth: DoorMultiplier += character.Bonus; break;
                case PersonalPassive.WeaponDamage: DamageMultiplier += character.Bonus; break;
                case PersonalPassive.MovementSpeed: MoveMultiplier += character.Bonus; break;
                case PersonalPassive.BuildSpeed: BuildMultiplier += character.Bonus; break;
                case PersonalPassive.UpgradeDiscount: DiscountChance = character.Bonus; break;
            }
        }
    }
}
