using System;
using System.Linq;
using ContainerDefense.Domain;

// Own-board rules: weapon build pads (display data only), yard decorations and sleeping indoors.
public static class BoardTests
{
    private const float Step = 1f / 30;
    private static int passed;
    public static string Run()
    {
        passed = 0;
        Check("Weapons take a free pad; explicit pads must be free and in range", () => {
            var m = New(); Claim(m,0,4); var me = m.CommandsFor(0);
            True(me.Place(0,WeaponKind.Gatling,5) && m.Houses[4].Weapons[0].Spot == 5);
            double gold = m.Players[0].Gold;
            False(me.Place(1,WeaponKind.Cannon,5)); False(me.Place(1,WeaponKind.Cannon,8)); False(me.Place(1,WeaponKind.Cannon,-2));
            True(m.Players[0].Gold == gold && m.Houses[4].Weapons[1] == null);
            True(me.Place(1,WeaponKind.Cannon) && m.Houses[4].Weapons[1].Spot == 0);
        });
        Check("Moving to a pad keeps the slot, level, cooldown and damage; no stacking", () => {
            var m = New(); Claim(m,0,4); var me = m.CommandsFor(0);
            me.Place(0,WeaponKind.Gatling,1); me.Place(1,WeaponKind.Cannon,2);
            float damage = m.Damage(4); var w = m.Houses[4].Weapons[0];
            False(me.MoveToSpot(0,2)); True(me.MoveToSpot(0,7) && w.Spot == 7 && ReferenceEquals(m.Houses[4].Weapons[0],w));
            True(m.Damage(4) == damage); False(me.MoveToSpot(2,3)); False(me.MoveToSpot(0,9));
        });
        Check("Pads are your own: other players cannot place or move on your board", () => {
            var m = New(); Claim(m,0,4); Claim(m,1,9); m.CommandsFor(0).Place(0,WeaponKind.Gatling,3);
            False(m.TryMoveWeaponSpot(1,4,0,4)); False(m.TryPlaceWeapon(1,4,1,WeaponKind.Cannon,6));
            True(m.Houses[4].Weapons[0].Spot == 3 && m.Houses[4].Weapons[1] == null);
        });
        Check("The three-weapon cap is unchanged by pads", () => {
            var m = New(); Claim(m,0,4); var me = m.CommandsFor(0);
            for (int s = 0; s < 3; s++) True(me.Place(s,WeaponKind.Gatling));
            True(m.Houses[4].Weapons.Count(w => w != null) == 3 && m.Houses[4].Weapons.Select(w => w.Spot).Distinct().Count() == 3);
            False(me.Place(3,WeaponKind.Gatling,6));
        });
        Check("Sleeping puts the resident at their door, never on the roof", () => {
            var m = New(); Claim(m,0,4); True(m.CommandsFor(0).ToggleSleep());
            True(m.Players[0].Sleeping && m.Players[0].Position.Distance(m.Houses[4].Entry) < .01f);
        });
        Check("Yard decorations stay off the house, path and pads, one per cell", () => {
            var yard = YardLayout.Normalize(new YardData { Items = new[] {
                new YardItem { Prop = "prop_crate",X = 0,Y = 0 },new YardItem { Prop = "prop_crate",X = 0,Y = 0 },
                new YardItem { Prop = "prop_plant",X = 4,Y = 1 },new YardItem { Prop = "prop_lamp",X = 5,Y = 5 },
                new YardItem { Prop = "prop_cone",X = YardLayout.PadX(2),Y = YardLayout.PadY(2) },new YardItem { Prop = "made_up",X = 11,Y = 0 },
                new YardItem { Prop = "prop_barrel",X = 20,Y = 0 },null } });
            True(yard.Items.Length == 1 && yard.Items[0].X == 0 && yard.Items[0].Y == 0);
            foreach (var item in YardLayout.Starter().Items.Concat(YardLayout.RandomPreset(7).Items)) True(YardLayout.CanDecorate(item.X,item.Y));
            True(YardLayout.RandomPreset(3).Items.Length == YardLayout.RandomPreset(3).Items.Length);
        });
        Check("Old saves get the starter yard; edits round-trip without touching progression", () => {
            var old = new AccountProgression(new AccountData { Version = 4,TotalXp = 500 },new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            True(old.Yard.Items.Length == YardLayout.Starter().Items.Length);
            old.SetYard(new YardData { Items = new[] { new YardItem { Prop = "prop_lamp",X = 11,Y = 6 } } });
            var restored = new AccountProgression(old.Snapshot(),new CharacterCatalog(CharacterCatalog.Defaults()),new ProgressionRules());
            True(restored.Yard.Items.Length == 1 && restored.Yard.Items[0].Prop == "prop_lamp" && restored.TotalXp == 500);
        });
        return passed + " board scenarios passed.";
    }
    private static MatchSimulation New() { return new MatchSimulation(new MatchRules { StartingGold = 10000,UpgradeSeconds = 0,PreparationSeconds = 30 },2); }
    private static void Claim(MatchSimulation m,int player,int house)
    {
        for (int i = 0; i < 1500 && m.Players[player].Position.Distance(m.Houses[house].Entry) > .1f; i++) m.Navigate(player,m.Houses[house].Entry,Step);
        True(m.TryClaim(player,house));
    }
    private static void Check(string name,Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool value) { if (!value) throw new Exception("Board assertion failed"); }
    private static void False(bool value) { True(!value); }
}
