using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Desktop prototype HUD. The simulation and input port remain independent of IMGUI.
    public sealed partial class MatchHud : MonoBehaviour
    {
        private GameSession session;
        private GUIStyle title, heading, body, small, button, number, brand;
        private readonly Color cream = new Color(1,.95f,.87f);
        private Texture2D rounded;
        private readonly Color panel = new Color(0.055f,0.075f,0.135f,0.96f);
        private readonly Color muted = new Color(0.66f,0.73f,0.84f);
        private readonly Color gold = new Color(1,0.76f,0.34f);
        private readonly Color green = new Color(0.36f,0.9f,0.66f);
        private readonly Color red = new Color(1,0.35f,0.4f);
        private float width, height;
        public void Initialize(GameSession game) { session = game; }

        private void EnsureStyles()
        {
            if (title != null) return;
            Font font = Resources.Load<Font>("Fonts/Nunito");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title = Style(font,43,FontStyle.Bold); heading = Style(font,24,FontStyle.Bold);
            brand = Style(font,39,FontStyle.Bold);
            body = Style(font,18,FontStyle.Normal); small = Style(font,14,FontStyle.Normal);
            number = Style(font,30,FontStyle.Bold);
            rounded = new Texture2D(32,32,TextureFormat.RGBA32,false) { name = "HUD rounded panel", filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            {
                float dx = Mathf.Max(7 - x, x - 24), dy = Mathf.Max(7 - y, y - 24);
                float distance = Mathf.Sqrt(Mathf.Max(0,dx) * Mathf.Max(0,dx) + Mathf.Max(0,dy) * Mathf.Max(0,dy));
                rounded.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(8 - distance)));
            }
            rounded.Apply();
            button = Style(font,18,FontStyle.Bold); button.alignment = TextAnchor.MiddleCenter;
            button.normal.background = rounded; button.hover.background = rounded; button.active.background = rounded;
            button.normal.textColor = new Color(0.07f,0.1f,0.16f);
            button.hover.textColor = button.active.textColor = button.normal.textColor;
            button.border = new RectOffset(10,10,10,10); button.wordWrap = true;
        }
        private static GUIStyle Style(Font font, int size, FontStyle weight)
        { return new GUIStyle { font = weight == FontStyle.Bold ? Resources.Load<Font>("Fonts/NunitoBold") ?? font : font, fontSize = size, fontStyle = FontStyle.Normal, normal = { textColor = Color.white }, wordWrap = true }; }

        private void OnGUI()
        {
            if (session == null || session.Match == null) return;
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            width = Screen.width / scale; height = Screen.height / scale;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero,Quaternion.identity,Vector3.one * scale);
            session.Arena.DrawLabels();
            if (!session.Started) { if (collectionOpen) CollectionScreen(); else TitleScreen(); return; }
            HouseInterior(); TopBar(); BottomBar(); RoomButton();
            if (session.Paused) PauseScreen();
            else if (session.Match.Finished) Results();
        }

        private void TopBar()
        {
            var m = session.Match; var p = m.Players[0];
            for (int i = 0; i < 6; i++)
            {
                var resident = m.Players[i]; float x = 20 + i * 70;
                Box(new Rect(x,20,65,104),i == 0 ? new Color(.5f,.37f,.15f,.95f) : panel);
                Color previous = GUI.color; if (resident.Eliminated) GUI.color = new Color(.4f,.4f,.45f);
                Portrait(new Rect(x + 4,24,57,62),i == 0 ? session.Inventory.Equipped(resident.Character.Id) : session.Collections.DefaultSkin(resident.Character.Id),true);
                GUI.color = previous;
                float hp = resident.HouseId < 0 ? 1 : m.Houses[resident.HouseId].Health / m.Houses[resident.HouseId].MaxHealth;
                Bar(new Rect(x + 5,88,55,6),resident.Eliminated ? 0 : hp,green);
                Label(new Rect(x + 5,99,60,23),resident.Eliminated ? "OUT" : resident.Character.Id.ToString(),small,cream);
            }
            float center = width / 2;
            Box(new Rect(center - 265,22,530,88),panel);
            bool prep = m.Phase == MatchPhase.Preparation;
            string target = m.Boss.TargetHouseId >= 0 ? "  /  TARGET " + (m.Boss.TargetHouseId + 1).ToString("00") : "";
            Label(new Rect(center - 245,34,490,26),prep ? (p.HouseId < 0 ? "CLAIM A HOME" : "PREPARE YOUR HOUSE") : "WAVE 1" + target,heading,prep ? gold : Color.white);
            Bar(new Rect(center - 245,73,490,13),m.Boss.Health / m.Boss.MaxHealth,red);
            Box(new Rect(width - 323,22,222,88),panel);
            Label(new Rect(width - 305,32,185,32),((int)p.Gold).ToString("N0") + "  GOLD",heading,gold);
            Label(new Rect(width - 305,73,185,24),prep ? Mathf.CeilToInt(m.PreparationRemaining) + "s until storm" : Clock(m.CombatSeconds) + "  /  " + m.LivingHouses() + " alive",small,Color.white);
            if (Button(new Rect(width - 83,22,60,60),"II",new Color(0.65f,0.72f,0.85f))) session.TogglePause();
        }

        private void BottomBar()
        {
            var m = session.Match; var human = m.Players[0];
            bool spectator = human.Eliminated;
            var p = m.Players[spectator ? session.SpectatedPlayer : 0];
            float y = height - 212;
            string notice = session.CurrentNotice;
            if (!string.IsNullOrEmpty(notice) && !m.Finished)
            {
                Box(new Rect(width / 2 - 385,y - 55,770,42),panel);
                Label(new Rect(width / 2 - 366,y - 46,732,28),notice,body,gold);
            }
            Box(new Rect(24,y,width - 48,188),panel);
            if (p.HouseId < 0)
            {
                Label(new Rect(48,y + 22,700,32),"YOUR HOME IS WAITING",heading,gold);
                Label(new Rect(48,y + 67,780,66),"Move to the steps in front of a free door.\nPress E to claim it, then E again to sleep.",body,Color.white);
                int nearby = session.NearbyHouse();
                string action = nearby < 0 ? "MOVE TO A DOOR" : m.Houses[nearby].OwnerId < 0 ? "[E] CLAIM " + (nearby + 1).ToString("00") : "ALREADY CLAIMED";
                if (Button(new Rect(width - 370,y + 45,312,60),action,gold,nearby >= 0 && m.Houses[nearby].OwnerId < 0 && !spectator)) session.Interact();
            }
            else
            {
                var h = m.Houses[p.HouseId];
                Label(new Rect(48,y + 16,325,34),(spectator ? "WATCHING " : "YOUR HOUSE ") + (h.Id + 1).ToString("00"),heading,gold);
                Label(new Rect(48,y + 55,325,24),p.Sleeping ? "SLEEPING  /  +" + m.Income(h.Id).ToString("0.##") + " gold / sec" : "AWAKE  /  income paused",body,p.Sleeping ? green : muted);
                Label(new Rect(48,y + 86,310,22),"DOOR  " + Mathf.CeilToInt(h.Health) + " / " + h.MaxHealth,small,Color.white);
                Bar(new Rect(48,y + 115,285,10),h.Health / h.MaxHealth,green);
                if (Button(new Rect(48,y + 139,285,32),spectator ? "[TAB] NEXT RESIDENT" : p.Sleeping ? "[E] WAKE UP" : "[E] SLEEP",new Color(0.66f,0.75f,0.86f),!m.Finished))
                { if (spectator) session.CycleSpectator(); else session.Interact(); }
                float cardWidth = (width - 416) / 3;
                UpgradeCard(new Rect(365,y + 17,cardWidth - 14,155),h,UpgradeKind.Bed,"BED",h.BedLevel,"+" + m.Income(h.Id).ToString("0.##") + " gold / sec",1,spectator);
                UpgradeCard(new Rect(365 + cardWidth,y + 17,cardWidth - 14,155),h,UpgradeKind.Door,"DOOR",h.DoorLevel,h.MaxHealth.ToString("0.#") + " maximum HP",2,spectator);
                UpgradeCard(new Rect(365 + cardWidth * 2,y + 17,cardWidth - 14,155),h,UpgradeKind.Weapon,"WEAPON",h.WeaponLevel,m.Damage(h.Id).ToString("0.#") + " damage / shot",3,spectator);
            }
            Label(new Rect(30,height - 20,1000,20),"WASD / arrows  Move     E / Space  Interact     1 / 2 / 3  Upgrade     Tab  Spectate     Esc  Pause",small,muted);
        }

        private void UpgradeCard(Rect rect, HouseState house, UpgradeKind kind, string name, int level, string effect, int key, bool spectator)
        {
            Box(rect,new Color(0.13f,0.18f,0.27f));
            Label(new Rect(rect.x + 16,rect.y + 12,rect.width - 32,28),name + "  LV." + (level + 1),heading,Color.white);
            Label(new Rect(rect.x + 16,rect.y + 49,rect.width - 32,28),effect,body,muted);
            int cost = session.Match.UpgradeCost(house.Id,kind);
            bool allowed = !spectator && !session.Match.Finished && !house.IsBuilding && cost >= 0 && session.Match.Players[0].Gold >= cost;
            string action = cost < 0 ? "MAX LEVEL" : spectator ? "SPECTATING" : "[" + key + "]  UPGRADE  /  " + cost;
            if (house.IsBuilding)
            {
                action = house.BuildingKind == kind ? "BUILDING  " + house.BuildRemaining.ToString("0.0") + "s" : "BUILDER BUSY";
                if (house.BuildingKind == kind) Bar(new Rect(rect.x + 16,rect.y + 83,rect.width - 32,6),1 - house.BuildRemaining / house.BuildDuration,green);
            }
            if (Button(new Rect(rect.x + 12,rect.y + 99,rect.width - 24,44),action,gold,allowed)) session.Buy(kind);
        }

        private void PauseScreen()
        {
            Overlay(); float x = width / 2 - 220, y = height / 2 - 190;
            Box(new Rect(x,y,440,380),panel);
            Label(new Rect(x + 35,y + 34,370,50),"TAKE A BREATHER",heading,gold);
            Label(new Rect(x + 35,y + 92,370,50),"Local match paused.",body,muted);
            if (Button(new Rect(x + 35,y + 152,370,52),"RESUME",gold)) session.TogglePause();
            if (Button(new Rect(x + 35,y + 220,370,52),"RESTART MATCH",new Color(0.65f,0.75f,0.87f))) session.Play();
            if (Button(new Rect(x + 35,y + 288,370,52),"BACK TO TITLE",new Color(0.65f,0.75f,0.87f))) session.ReturnToTitle();
        }

        private void Results()
        {
            Overlay(); var m = session.Match; var p = m.Players[0];
            bool won = m.Phase == MatchPhase.Victory && !p.Eliminated;
            float x = width / 2 - 290, y = height / 2 - 305;
            Box(new Rect(x,y,580,610),panel);
            Label(new Rect(x + 40,y + 34,500,54),won ? "STORM SURVIVED" : "ELIMINATED",title,won ? gold : red);
            Label(new Rect(x + 40,y + 101,500,52),won ? "Your little home held on." : m.Phase == MatchPhase.Victory ? "The remaining residents defeated the boss." : "The storm claimed every home.",body,muted);
            Label(new Rect(x + 40,y + 173,310,166),"Survival time\n\nBoss damage\n\nGold earned\n\nUpgrades purchased",body,muted);
            Label(new Rect(x + 360,y + 173,180,166),Clock(p.SurvivalSeconds) + "\n\n" + Mathf.RoundToInt(p.DamageDealt) + "\n\n" + ((int)p.GoldEarned).ToString("N0") + "\n\n" + p.UpgradesPurchased,body,Color.white);
            var reward = session.LastReward;
            if (reward != null)
            {
                Label(new Rect(x + 40,y + 355,500,33),"+" + reward.Xp + " XP  /  ACCOUNT LEVEL " + reward.NewLevel,heading,gold);
                string unlocked = reward.Unlocked.Length == 0 ? (reward.Xp == 0 ? "Claim a house to earn match XP." : session.SaveStatus) :
                    "UNLOCKED: " + string.Join(", ",System.Array.ConvertAll(reward.Unlocked,id => session.Characters.Get(id).Name));
                Label(new Rect(x + 40,y + 397,500,46),unlocked,body,reward.Unlocked.Length > 0 ? green : muted);
                var account = session.Account;
                Bar(new Rect(x + 40,y + 450,500,9),account.XpNeeded == 0 ? 1 : (float)account.XpInLevel / account.XpNeeded,green);
            }
            if (Button(new Rect(x + 40,y + 485,500,55),"PLAY AGAIN",gold)) session.Play();
            if (Button(new Rect(x + 40,y + 555,500,36),"CHARACTER SELECTION",new Color(0.65f,0.75f,0.87f))) session.ReturnToTitle();
        }
        private void Overlay() { Color old = GUI.color; GUI.color = new Color(0.015f,0.025f,0.06f,0.75f); GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture); GUI.color = old; }
        private void Box(Rect r, Color color)
        {
            GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,10);
        }
        private void Label(Rect r, string text, GUIStyle style, Color color)
        { Color old = GUI.color; GUI.color = color; GUI.Label(r,text,style); GUI.color = old; }
        private bool Button(Rect r, string text, Color color, bool enabled = true)
        {
            bool oldEnabled = GUI.enabled; Color oldColor = GUI.backgroundColor;
            GUI.enabled = enabled; GUI.backgroundColor = enabled ? color : new Color(0.35f,0.4f,0.48f);
            bool clicked = GUI.Button(r,text,button); GUI.backgroundColor = oldColor; GUI.enabled = oldEnabled; return clicked;
        }
        private void Bar(Rect r, float ratio, Color color)
        {
            Box(r,new Color(0.02f,0.04f,0.08f));
            if (ratio > 0) Box(new Rect(r.x,r.y,r.width * Mathf.Clamp01(ratio),r.height),color);
        }
        private static string Clock(float seconds) { int s = Mathf.FloorToInt(seconds); return (s / 60).ToString("00") + ":" + (s % 60).ToString("00"); }
        private void OnDestroy() { if (rounded != null) Destroy(rounded); if (stickerIcons != null) stickerIcons.Dispose(); }
    }
}
