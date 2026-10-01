using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // View only: one fixed map, sprites, no physics, meshes, lights, or baked gameplay screenshot.
    // Art comes from Resources/Art2D/Generated when present (the vinyl-toy set matched to vhi's mocks),
    // with the original atlas as a fallback so removing a file never breaks the scene.
    public sealed partial class ArenaView : MonoBehaviour
    {
        // Projected world scale: a high-angle look with wide containers, as in the top-down mocks.
        private const float ScaleX = .95f, ScaleZ = .7f, RoadWidth = 1.6f, HouseWidth = 8.4f, AtlasHouseSize = 4.6f, WeaponSize = 1.5f;
        // Normalised positions (from the top-left) on the generated container art.
        private static readonly Vector2 HousePivot = new Vector2(.46f,.1f), NumberPanel = new Vector2(.25f,.435f);
        private static readonly Vector2[] RoofPads = { new Vector2(.248f,.188f),new Vector2(.485f,.222f),new Vector2(.734f,.278f) };
        private static readonly string[] HouseColours = { "blue","pink","yellow","purple","teal","red" };
        private static readonly string[] WeaponNames = { "gatling","cannon","slow","rocket" };
        private static readonly string[] PropNames = { "prop_crate","prop_barrel","prop_plant","prop_cone","prop_pallets" };
        public Camera Camera { get; private set; }
        public CharacterPortraits Portraits { get; private set; }
        private MatchSimulation match;
        private GameSession session;
        private MapDefinition map;
        private readonly CharacterVisualController[] actors = new CharacterVisualController[6];
        private readonly SpriteRenderer[] homes = new SpriteRenderer[12], smoke = new SpriteRenderer[12];
        private readonly Rect[] houseRects = new Rect[12];
        private readonly SpriteRenderer[,] weapons = new SpriteRenderer[12,3], flashes = new SpriteRenderer[12,3];
        private readonly float[,] fireUntil = new float[12,3];
        private readonly LineRenderer[,] shots = new LineRenderer[12,3];
        private readonly float[] hitUntil = new float[12],cheerUntil = new float[6],runningUntil = new float[6];
        private readonly Vector3[] previous = new Vector3[6];
        private readonly bool[] facing = new bool[6];
        private readonly List<Sprite> owned = new List<Sprite>();
        private readonly List<GameObject> scenery = new List<GameObject>();
        private readonly List<KeyValuePair<int,SpriteRenderer>> entryMarkers = new List<KeyValuePair<int,SpriteRenderer>>();
        private readonly SpriteRenderer[] actorShadows = new SpriteRenderer[6], minions = new SpriteRenderer[5];
        private readonly Sprite[] weaponArt = new Sprite[4], houseArt = new Sprite[6];
        private readonly Dictionary<string,Sprite> props = new Dictionary<string,Sprite>();
        private Sprite[] atlas;
        private Sprite ground,road,crossing,marker,lampSprite,edge,dock,plazaSprite,damagedBlue,solid,soft;
        private SpriteRenderer boss,bossShadow;
        private LineRenderer route;
        private Material lines;
        private Rect mapBounds;
        private Texture2D grade;
        // Feedback pools: door-hit sparks, coin pops and the boss hit flash; screen shake on your own house.
        private readonly SpriteRenderer[] sparks = new SpriteRenderer[18], coins = new SpriteRenderer[8];
        private readonly float[] sparkStart = new float[18], coinStart = new float[8];
        private readonly Vector3[] sparkFrom = new Vector3[18], sparkVelocity = new Vector3[18], coinFrom = new Vector3[8];
        private int nextSpark, nextCoin;
        private float shakeUntil, bossFlashUntil, nextCoinAt;
        public void Build(CollectionCatalog collections)
        {
            session = GetComponent<GameSession>();
            Camera = new GameObject("High angle 2D camera").AddComponent<Camera>(); Camera.transform.SetParent(transform);
            Camera.transform.position = new Vector3(0,-1.8f,-30); Camera.tag = "MainCamera"; Camera.orthographic = true; Camera.orthographicSize = 19;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = HudTheme.Hex(0x1C1626); Camera.allowHDR = false;
            Camera.gameObject.AddComponent<AudioListener>(); RenderSettings.fog = false;
            Portraits = new CharacterPortraits(); Portraits.Build(collections);
            var texture = Resources.Load<Texture2D>("Art2D/topdown-atlas");
            if (texture == null) throw new System.InvalidOperationException("Missing top-down atlas.");
            atlas = new Sprite[12];
            for (int i = 0; i < 12; i++) {
                atlas[i] = Sprite.Create(texture,new Rect(i % 3 * texture.width / 3f,(3 - i / 3) * texture.height / 4f,texture.width / 3f,texture.height / 4f),new Vector2(.5f,.5f),texture.width / 3f);
                owned.Add(atlas[i]);
            }
            solid = Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1); owned.Add(solid);
            soft = Sprite.Create(SoftTexture(),new Rect(0,0,64,64),new Vector2(.5f,.5f),64); owned.Add(soft);
            ground = TileSprite("asphalt",512 / 6f) ?? TileSprite("ground_tile",256); road = TileSprite("road_straight",256 / RoadWidth); crossing = TileSprite("road_cross",256 / RoadWidth);
            var markerTexture = HudIcons.Get("boss_entry_marker");
            marker = Sprite.Create(markerTexture,new Rect(0,0,markerTexture.width,markerTexture.height),new Vector2(.5f,.5f),markerTexture.width); owned.Add(marker);
            for (int k = 0; k < 6; k++) houseArt[k] = Generated("house_" + HouseColours[k],HousePivot);
            damagedBlue = Generated("house_blue_damaged",HousePivot);
            for (int k = 0; k < 4; k++) weaponArt[k] = Generated("weapon_" + WeaponNames[k],new Vector2(.5f,.3f)) ?? atlas[6 + k];
            lampSprite = Generated("prop_lamp",new Vector2(.5f,.05f)) ?? atlas[10];
            foreach (var prop in PropNames) { var s = Generated(prop,new Vector2(.5f,.08f)); if (s != null) props[prop] = s; }
            plazaSprite = Generated("plaza",new Vector2(.5f,.5f));
            edge = Strip("edge_strip",0,4.5f); dock = Strip("dock_strip",.33f,5f);
            var cloudSprite = Generated("boss_v2",new Vector2(.5f,.5f)) ?? Whole(Resources.Load<Texture2D>("Art2D/boss"),new Vector2(.5f,.5f));
            var minionSprite = Generated("minion",new Vector2(.5f,.5f)) ?? cloudSprite;
            boss = SpriteObject("Smoke boss",cloudSprite,900); boss.transform.localScale = Vector3.one * 4.2f;
            bossShadow = Tinted("Boss shadow",soft,new Color(0,0,0,.35f),-80);
            for (int i = 0; i < minions.Length; i++) minions[i] = SpriteObject("Smoke minion " + i,minionSprite,899);
            for (int i = 0; i < 6; i++) actorShadows[i] = Tinted("Resident shadow " + i,soft,new Color(0,0,0,.38f),0);
            var coinTexture = HudIcons.Get("icon_coin"); var coinSprite = Sprite.Create(coinTexture,new Rect(0,0,coinTexture.width,coinTexture.height),new Vector2(.5f,.5f),coinTexture.width); owned.Add(coinSprite);
            for (int i = 0; i < sparks.Length; i++) { sparks[i] = Tinted("Hit spark " + i,soft,HudTheme.Hex(0xFFB347),970); sparks[i].enabled = false; sparkStart[i] = -10; }
            for (int i = 0; i < coins.Length; i++) { coins[i] = SpriteObject("Coin pop " + i,coinSprite,970); coins[i].enabled = false; coinStart[i] = -10; }
            lines = new Material(Shader.Find("Sprites/Default")); route = MakeLine("Telegraphed road route",.16f,HudTheme.Hex(0xE86A4A,.8f),-3);
            for (int h = 0; h < 12; h++) {
                smoke[h] = Tinted("Damage smoke " + h,soft,new Color(.15f,.13f,.16f,.7f),0); smoke[h].enabled = false;
                for (int slot = 0; slot < 3; slot++) {
                    weapons[h,slot] = SpriteObject("Defense " + h + "/" + slot,weaponArt[0],15);
                    shots[h,slot] = MakeLine("Pooled tracer " + h + "/" + slot,.06f,new Color(1,.8f,.42f),950); shots[h,slot].positionCount = 2; shots[h,slot].enabled = false;
                    flashes[h,slot] = Tinted("Muzzle flash " + h + "/" + slot,soft,new Color(1,.86f,.45f,.95f),960); flashes[h,slot].enabled = false;
                }
            }
            BuildFeedback();
        }
        private static int Depth(float y) { return 500 - Mathf.RoundToInt(y * 20); }
        private Sprite Generated(string id,Vector2 pivot)
        { var t = Resources.Load<Texture2D>("Art2D/Generated/" + id); return t == null ? null : Whole(t,pivot); }
        private Sprite Whole(Texture2D texture,Vector2 pivot)
        { var s = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),pivot,texture.width); owned.Add(s); return s; }
        // A horizontally tiling strip whose sprite is `height` world units tall.
        private Sprite Strip(string id,float pivotY,float height)
        {
            var t = Resources.Load<Texture2D>("Art2D/Generated/" + id); if (t == null) return null;
            var s = Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,pivotY),t.height / height,0,SpriteMeshType.FullRect); owned.Add(s); return s;
        }
        private Sprite TileSprite(string id,float pixelsPerUnit)
        {
            var texture = Resources.Load<Texture2D>("Art2D/Generated/" + id); if (texture == null) return null;
            var sprite = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),pixelsPerUnit,0,SpriteMeshType.FullRect); owned.Add(sprite); return sprite;
        }
        // Radial falloff used for shadows, lamp glow, smoke and muzzle flashes.
        private static Texture2D SoftTexture()
        {
            var t = new Texture2D(64,64,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,name = "Soft radial" };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
                float d = Vector2.Distance(new Vector2(x + .5f,y + .5f),new Vector2(32,32)) / 32f;
                t.SetPixel(x,y,new Color(1,1,1,Mathf.SmoothStep(1,0,d)));
            }
            t.Apply(); return t;
        }
        private void Prop(string id,Vector3 position,float size)
        {
            Sprite sprite; if (!props.TryGetValue(id,out sprite)) return;
            var r = SpriteObject(id,sprite,Depth(position.y)); r.transform.position = position; r.transform.localScale = Vector3.one * size; scenery.Add(r.gameObject);
        }
        private SpriteRenderer Tinted(string name,Sprite sprite,Color color,int order)
        { var r = SpriteObject(name,sprite,order); r.color = color; return r; }
        private SpriteRenderer SpriteObject(string name,Sprite sprite,int order)
        {
            var result = new GameObject(name).AddComponent<SpriteRenderer>(); result.transform.SetParent(transform); result.sprite = sprite; result.sortingOrder = order; return result;
        }
        private LineRenderer MakeLine(string name,float thickness,Color color,int order)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(transform); line.sharedMaterial = lines;
            line.startWidth = line.endWidth = thickness; line.startColor = line.endColor = color; line.sortingOrder = order; return line;
        }
        private void GroundRect(string name,Vector3 pos,float w,float h,Color color,int order)
        { var sprite = SpriteObject(name,solid,order); sprite.transform.position = pos; sprite.transform.localScale = new Vector3(w,h,1); sprite.color = color; scenery.Add(sprite.gameObject); }
        // A tiled strip when generated art is available, otherwise a flat rectangle of the fallback colour.
        private void Tiled(string name,Sprite tile,Vector3 pos,float w,float h,float angle,Color fallback,int order)
        {
            if (tile == null) { GroundRect(name,pos,angle == 0 ? w : h,angle == 0 ? h : w,fallback,order); return; }
            var sprite = SpriteObject(name,tile,order); sprite.drawMode = SpriteDrawMode.Tiled; sprite.tileMode = SpriteTileMode.Continuous;
            sprite.size = new Vector2(w,h); sprite.transform.position = pos; sprite.transform.rotation = Quaternion.Euler(0,0,angle); scenery.Add(sprite.gameObject);
        }
        private void BuildMap()
        {
            foreach (var go in scenery) { go.SetActive(false); Destroy(go); } scenery.Clear(); entryMarkers.Clear();
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var n in map.Nodes) { var p = Project(n.Position); minX = Mathf.Min(minX,p.x); maxX = Mathf.Max(maxX,p.x); minY = Mathf.Min(minY,p.y); maxY = Mathf.Max(maxY,p.y); }
            float margin = RoadWidth / 2 + 1.1f;
            var yard = new Rect(minX - margin,minY - margin,maxX - minX + margin * 2,maxY - minY + margin * 2);
            GroundRect("Yard border",yard.center,yard.width + .5f,yard.height + .5f,HudTheme.Hex(0x2A2238),-101);
            Tiled("Wet asphalt",ground,yard.center,yard.width,yard.height,0,HudTheme.Hex(0x2E3242),-100);
            // Roads run exactly along the orthogonal road graph, one strip per row and column.
            var rows = new SortedDictionary<float,bool>(); var columns = new SortedDictionary<float,bool>();
            foreach (var n in map.Nodes) { var p = Project(n.Position); rows[Mathf.Round(p.y * 100) / 100] = true; columns[Mathf.Round(p.x * 100) / 100] = true; }
            foreach (float y in rows.Keys) Tiled("Road east-west",road,new Vector3((minX + maxX) / 2,y,0),maxX - minX + RoadWidth,RoadWidth,0,HudTheme.Hex(0x3D4350),-98);
            foreach (float x in columns.Keys) Tiled("Road north-south",road,new Vector3(x,(minY + maxY) / 2,0),maxY - minY + RoadWidth,RoadWidth,90,HudTheme.Hex(0x3D4350),-98);
            foreach (var n in map.Nodes) {
                if (crossing == null) { GroundRect("Crossing",Project(n.Position),RoadWidth,RoadWidth,HudTheme.Hex(0x3D4350),-97); continue; }
                var c = SpriteObject("Crossing " + n.Id,crossing,-97); c.transform.position = Project(n.Position); scenery.Add(c.gameObject);
            }
            // The rabbit plaza where everyone starts, beside house 08 as in the mocks.
            if (plazaSprite != null) {
                var plaza = SpriteObject("Rabbit plaza",plazaSprite,-93); scenery.Add(plaza.gameObject);
                plaza.transform.position = Project(new Point2(0,-.3f)); plaza.transform.localScale = Vector3.one * 4.4f;
            }
            for (int i = 0; i < 12; i++) BuildHouse(i);
            // Warm street lamps beside every crossing, each with a soft glow.
            foreach (var n in map.Nodes) {
                var corner = Project(n.Position) + new Vector3(RoadWidth / 2 + .4f,RoadWidth / 2 + .1f,0);
                if (!yard.Contains(corner)) continue;
                var glow = Tinted("Lamp glow",soft,HudTheme.Hex(0xFFC766,.3f),-92); scenery.Add(glow.gameObject);
                glow.transform.position = corner + new Vector3(0,.4f,0); glow.transform.localScale = new Vector3(4.2f,2.8f,1);
                var lamp = SpriteObject("Street lamp",lampSprite,Depth(corner.y)); scenery.Add(lamp.gameObject);
                lamp.transform.position = corner; lamp.transform.localScale = Vector3.one * 1.25f;
            }
            // Props along the yard's east and west margins and in the corners.
            string[] sideProps = { "prop_crate","prop_barrel","prop_plant","prop_pallets","prop_cone" };
            int k2 = 0;
            foreach (float y in rows.Keys) for (int side = 0; side < 2; side++)
                Prop(sideProps[k2++ % sideProps.Length],new Vector3(side == 0 ? yard.xMin + .55f : yard.xMax - .55f,y + RoadWidth * .9f,0),.95f);
            // Stacked containers with string lights along the north edge, the dock and water to the south.
            if (edge != null) {
                var strip = SpriteObject("North container stacks",edge,-94); strip.drawMode = SpriteDrawMode.Tiled; strip.tileMode = SpriteTileMode.Continuous;
                strip.size = new Vector2(yard.width + 10,4.5f); strip.transform.position = new Vector3(yard.center.x,yard.yMax - .2f,0); scenery.Add(strip.gameObject);
            }
            if (dock != null) {
                var pier = SpriteObject("South dock",dock,Depth(yard.yMin - 3)); pier.drawMode = SpriteDrawMode.Tiled; pier.tileMode = SpriteTileMode.Continuous;
                pier.size = new Vector2(yard.width + 10,5f); pier.transform.position = new Vector3(yard.center.x,yard.yMin + .2f,0); scenery.Add(pier.gameObject);
            }
            // Boss entry markers sit just outside the ring road and point inward.
            foreach (int id in map.EntryNodes) {
                var p = Project(map.Nodes[id].Position); Vector2 outward;
                if (Mathf.Abs(p.y - maxY) < .01f) outward = Vector2.up; else if (Mathf.Abs(p.y - minY) < .01f) outward = Vector2.down;
                else outward = p.x < (minX + maxX) / 2 ? Vector2.left : Vector2.right;
                var m = SpriteObject("Boss entry " + id,marker,-90); scenery.Add(m.gameObject);
                m.transform.position = p + (Vector3)(outward * (RoadWidth / 2 + .85f));
                m.transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(-outward.y,-outward.x) * Mathf.Rad2Deg);
                entryMarkers.Add(new KeyValuePair<int,SpriteRenderer>(id,m));
            }
            mapBounds = new Rect(yard.x - .6f,yard.y - 1.2f,yard.width + 1.2f,yard.height + 3.2f);
        }
        private void BuildHouse(int i)
        {
            var spawn = map.HouseSpawns[i]; var art = houseArt[spawn.ColorIndex % 6];
            if (art != null) {
                // The door steps sit on the house's entry point; the art keeps its own proportions.
                var foot = Project(spawn.Entry) + new Vector3(0,-.15f,0);
                float w = HouseWidth, h = w * art.rect.height / art.rect.width;
                homes[i] = SpriteObject("House " + (i + 1).ToString("00"),art,Depth(foot.y)); homes[i].transform.position = foot; homes[i].transform.localScale = Vector3.one * w;
                houseRects[i] = new Rect(foot.x - HousePivot.x * w,foot.y - HousePivot.y * h,w,h);
            } else {
                var point = Project(spawn.Center) + new Vector3(0,.25f,0);
                homes[i] = SpriteObject("House " + (i + 1).ToString("00"),atlas[spawn.ColorIndex],Depth(point.y - AtlasHouseSize * .4f)); homes[i].transform.position = point; homes[i].transform.localScale = Vector3.one * AtlasHouseSize;
                houseRects[i] = new Rect(point.x - AtlasHouseSize / 2,point.y - AtlasHouseSize / 2,AtlasHouseSize,AtlasHouseSize);
            }
            scenery.Add(homes[i].gameObject);
            var r = houseRects[i];
            var shadow = Tinted("House shadow",soft,new Color(0,0,0,.45f),-91); scenery.Add(shadow.gameObject);
            shadow.transform.position = new Vector3(r.center.x + .4f,r.y + r.height * .22f,0); shadow.transform.localScale = new Vector3(r.width * 1.12f,r.height * .55f,1);
            var doorGlow = Tinted("Door glow",soft,HudTheme.Hex(0xFFB65C,.28f),-91); scenery.Add(doorGlow.gameObject);
            doorGlow.transform.position = new Vector3(r.x + r.width * HousePivot.x,r.y + r.height * .12f,0); doorGlow.transform.localScale = new Vector3(3.4f,1.6f,1);
        }
        // A house's art bounds on screen, in GUI pixels.
        public Rect HouseScreenRect(int house)
        {
            var r = houseRects[house]; var a = ScreenPoint(new Vector3(r.xMin,r.yMax,0)); var b = ScreenPoint(new Vector3(r.xMax,r.yMin,0));
            return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
        }
        // A point on a house's art, given in normalised coordinates from its top-left corner.
        public Vector3 HousePoint(int house,Vector2 fromTopLeft)
        { var r = houseRects[house]; return new Vector3(r.x + r.width * fromTopLeft.x,r.yMax - r.height * fromTopLeft.y,0); }
        private bool GeneratedHouses { get { return houseArt[0] != null; } }
        public void Bind(MatchSimulation simulation,SkinDefinition humanSkin = null)
        {
            match = simulation; map = match.Map; BuildMap();
            System.Array.Clear(houseBaseScale,0,houseBaseScale.Length); lastBossHealth = -1; floaters.Clear(); flyingCoins.Clear();
            for (int i = 0; i < 6; i++) {
                if (actors[i] != null) { actors[i].gameObject.SetActive(false); Destroy(actors[i].gameObject); }
                string skin = i == 0 && humanSkin != null ? humanSkin.SkinId : CharacterCatalog.Key(match.Players[i].Character.Id) + "_default";
                actors[i] = new GameObject(match.Players[i].Character.Id + " / " + skin).AddComponent<CharacterVisualController>(); actors[i].transform.SetParent(transform); actors[i].Initialize(Portraits.Set(skin));
                previous[i] = Project(match.Players[i].Position); runningUntil[i] = cheerUntil[i] = 0;
            }
            for (int h = 0; h < 12; h++) { hitUntil[h] = 0; for (int s = 0; s < 3; s++) fireUntil[h,s] = 0; }
        }
        public static Vector3 Project(Point2 p) { return new Vector3(p.X * ScaleX,p.Z * ScaleZ,0); }
        // Weapons sit on the roof's turret pads; the simulation's socket point stays authoritative for range.
        private Vector3 Socket(int house,int slot)
        { return GeneratedHouses ? HousePoint(house,RoofPads[slot]) : Project(match.WeaponPoint(house,slot)) + new Vector3(0,.55f,0); }
        public Vector2 ScreenPoint(Point2 p) { return ScreenPoint(Project(p)); }
        public Vector2 ScreenPoint(Vector3 world)
        { var value = Camera.WorldToScreenPoint(world); return new Vector2(value.x,Screen.height - value.y); }
        public void Handle(MatchEvent e)
        {
            FeedbackEvent(e);
            if (e.Kind == MatchEventKind.Shot && e.HouseId >= 0) fireUntil[e.HouseId,Mathf.Clamp((int)e.Amount,0,2)] = Time.time + .13f;
            if (e.Kind == MatchEventKind.DoorHit && e.HouseId >= 0) {
                hitUntil[e.HouseId] = Time.time + .22f; SparkBurst(HousePoint(e.HouseId,new Vector2(HousePivot.x,.72f)));
                if (match != null && match.Houses[e.HouseId].OwnerId == 0 && session.View.ViewingOwnBase) shakeUntil = Time.time + .28f;
            }
            if (e.Kind == MatchEventKind.Shot) bossFlashUntil = Time.time + .06f;
            if (e.Kind == MatchEventKind.Sold && e.PlayerId == 0 && match != null && match.Players[0].HouseId >= 0) CoinPop(HousePoint(match.Players[0].HouseId,new Vector2(.5f,.2f)));
            if ((e.Kind == MatchEventKind.Upgraded || e.Kind == MatchEventKind.Placed) && e.PlayerId >= 0) cheerUntil[e.PlayerId] = Time.time + .55f;
        }
        // Fits a world rect into the part of the screen the HUD leaves free (fractions of the screen).
        private void Frame(Rect world,float minimumSize,out float size,out Vector3 position)
        {
            float top = HudLayout.ReservedTop, bottom = HudLayout.ReservedBottom, left = HudLayout.ReservedLeft, right = HudLayout.ReservedRight;
            float freeH = Mathf.Max(.2f,1 - top - bottom), freeW = Mathf.Max(.2f,1 - left - right), aspect = Mathf.Max(.5f,Camera.aspect);
            size = Mathf.Max(minimumSize,world.height / 2 / freeH,world.width / 2 / (aspect * freeW));
            float viewH = size * 2, viewW = viewH * aspect;
            position = new Vector3(world.center.x - (left - right) / 2 * viewW,world.center.y - (bottom - top) / 2 * viewH,-30);
        }
        private void LateUpdate()
        {
            if (match == null) return;
            bool playing = session.Started; float t = match.Elapsed;
            var viewed = match.Players[session.ViewedPlayer]; bool focus = playing && !session.Overview && viewed.HouseId >= 0;
            float size; Vector3 destination;
            if (focus) { var r = houseRects[viewed.HouseId]; Frame(new Rect(r.center.x - 8,r.y - 1.2f,16,r.height + 2.4f),4.5f,out size,out destination); }
            else Frame(mapBounds,8,out size,out destination);
            Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize,size,1 - Mathf.Exp(-Time.unscaledDeltaTime * 7));
            Camera.transform.position = Vector3.Lerp(Camera.transform.position,destination,1 - Mathf.Exp(-Time.unscaledDeltaTime * 7));
            foreach (var entry in entryMarkers) {
                bool active = entry.Key == match.Boss.EntryNode && match.Phase == MatchPhase.Preparation;
                entry.Value.transform.localScale = Vector3.one * (active ? 1.5f + Mathf.Sin(t * 4) * .06f : 1.1f);
                entry.Value.color = new Color(1,1,1,active ? 1 : .45f);
            }
            var bossPoint = Project(match.Boss.Position);
            for (int h = 0; h < 12; h++) {
                var home = match.Houses[h]; var colour = match.Map.HouseSpawns[h].ColorIndex % 6;
                bool damaged = home.Occupied && home.Health < home.MaxHealth * .35f, dead = home.Destroyed || home.Vacated;
                if (GeneratedHouses) homes[h].sprite = (damaged || dead) && colour == 0 && damagedBlue != null ? damagedBlue : houseArt[colour];
                homes[h].color = dead ? new Color(.45f,.44f,.5f) : Time.time < hitUntil[h] ? new Color(1,.62f,.62f) : damaged ? new Color(.82f,.78f,.78f) : Color.white;
                // Damaged and destroyed homes smoke from the roof.
                smoke[h].enabled = playing && (damaged || dead);
                if (smoke[h].enabled) {
                    var roof = HousePoint(h,new Vector2(.6f,.1f)); float puff = Mathf.Repeat(t * .5f + h * .37f,1);
                    smoke[h].transform.position = roof + new Vector3(Mathf.Sin(t + h) * .3f,puff * 1.6f,0); smoke[h].transform.localScale = Vector3.one * (1.2f + puff * 1.6f);
                    smoke[h].color = new Color(.15f,.13f,.16f,.7f * (1 - puff)); smoke[h].sortingOrder = homes[h].sortingOrder + 3;
                }
                for (int s = 0; s < 3; s++) {
                    var w = home.Weapons[s]; var render = weapons[h,s]; render.gameObject.SetActive(playing && w != null && home.Occupied);
                    if (w != null) {
                        render.sprite = weaponArt[(int)w.Kind]; render.transform.position = Socket(h,s); render.sortingOrder = homes[h].sortingOrder + 2;
                        render.transform.localScale = Vector3.one * (w.Building ? WeaponSize * .75f : WeaponSize);
                        render.color = w.Building ? new Color(.6f,.75f,.85f,.6f) : Color.white;
                        Vector3 direction = bossPoint - render.transform.position;
                        // Turret art faces upper-right; mirror it toward a boss on the left.
                        render.flipX = direction.x < 0;
                        if (Time.time < fireUntil[h,s]) render.transform.position -= direction.normalized * .12f;
                    }
                    var muzzle = render.transform.position + new Vector3(render.flipX ? -.55f : .55f,.55f,0);
                    flashes[h,s].enabled = playing && w != null && home.Occupied && Time.time < fireUntil[h,s] - .05f;
                    if (flashes[h,s].enabled) { flashes[h,s].transform.position = muzzle; flashes[h,s].transform.localScale = Vector3.one * (.35f + Random.value * .15f); }
                    // Gatling fires tracer dashes; cannon, slow and rocket fire visible projectiles (ArenaFeedback).
                    shots[h,s].enabled = playing && w != null && w.Kind == WeaponKind.Gatling && home.Occupied && Time.time < fireUntil[h,s];
                    if (shots[h,s].enabled) { shots[h,s].SetPosition(0,muzzle); shots[h,s].SetPosition(1,bossPoint); }
                }
            }
            for (int i = 0; i < 6; i++) {
                var p = match.Players[i]; Vector3 point = Project(p.Position);
                if ((point - previous[i]).sqrMagnitude > .00001f) runningUntil[i] = t + .12f;
                bool moving = t < runningUntil[i]; if (moving && Mathf.Abs(point.x - previous[i].x) > .001f) facing[i] = point.x > previous[i].x; previous[i] = point;
                CharacterPose pose = moving ? CharacterPose.Run : CharacterPose.Idle;
                if (p.Sleeping) pose = CharacterPose.Sleep;
                if (Time.time < cheerUntil[i]) pose = CharacterPose.Upgrade;
                if (p.HouseId >= 0 && Time.time < hitUntil[p.HouseId]) pose = CharacterPose.Hurt;
                if (match.Finished && !p.Eliminated) pose = CharacterPose.Victory;
                if (p.Eliminated) pose = CharacterPose.Eliminated;
                // Sleeping residents are indoors (see VIEW MY ROOM); everyone else is out in the yard.
                bool visible = playing && !p.Sleeping;
                actors[i].gameObject.SetActive(visible); actors[i].Present(point,pose,t + i * .3f,facing[i],Depth(point.y));
                actors[i].transform.localScale *= .6f;
                actorShadows[i].gameObject.SetActive(visible); actorShadows[i].sortingOrder = Depth(point.y) - 1;
                actorShadows[i].transform.position = point + new Vector3(0,.05f,0); actorShadows[i].transform.localScale = new Vector3(1.3f,.42f,1);
            }
            bool bossVisible = playing && match.Boss.Phase != BossPhase.Dead;
            bossShadow.gameObject.SetActive(bossVisible); bossShadow.transform.position = bossPoint + new Vector3(0,-2.2f,0); bossShadow.transform.localScale = new Vector3(4.2f,1.1f,1);
            // Minions drift around the boss on slow independent loops.
            for (int i = 0; i < minions.Length; i++) {
                float phase = i * 1.2566f, a = t * (.45f + i * .07f) + phase;
                minions[i].gameObject.SetActive(bossVisible);
                minions[i].transform.position = bossPoint + new Vector3(Mathf.Cos(a) * (2.9f + i % 2 * .7f),Mathf.Sin(a * 1.3f) * 1.5f - .2f + Mathf.Sin(t * 3 + i) * .12f,0);
                minions[i].transform.localScale = Vector3.one * (.9f + (i % 3) * .15f);
            }
            boss.gameObject.SetActive(bossVisible);
            boss.transform.position = bossPoint + new Vector3(0,.6f + Mathf.Sin(t * 2) * .12f,0);
            boss.transform.localScale = Vector3.one * (4.2f + Mathf.Sin(t * 2) * .06f);
            route.enabled = playing && (match.Boss.Phase == BossPhase.Telegraphing || match.Boss.Phase == BossPhase.Travelling);
            if (route.enabled) {
                route.positionCount = match.Boss.RoutePath.Length + 1; route.SetPosition(0,bossPoint);
                for (int i = 0; i < match.Boss.RoutePath.Length; i++) route.SetPosition(i + 1,Project(match.Boss.RoutePath[i]));
            }
            Feedback(playing,t); TickFeedback(playing);
        }
        private void SparkBurst(Vector3 at)
        {
            for (int k = 0; k < 6; k++) {
                int i = nextSpark++ % sparks.Length; sparkStart[i] = Time.time; sparkFrom[i] = at;
                float a = Random.Range(0f,Mathf.PI * 2); sparkVelocity[i] = new Vector3(Mathf.Cos(a),Mathf.Abs(Mathf.Sin(a)) + .3f,0) * Random.Range(2.5f,4.5f);
            }
        }
        private void CoinPop(Vector3 at) { int i = nextCoin++ % coins.Length; coinStart[i] = Time.time; coinFrom[i] = at + new Vector3(Random.Range(-.8f,.8f),0,0); }
        // Sparks fly and fade, coins rise and fade, the boss flashes when hit, the camera shakes when your door is hit.
        private void Feedback(bool playing,float t)
        {
            for (int i = 0; i < sparks.Length; i++) {
                float age = Time.time - sparkStart[i]; sparks[i].enabled = playing && age < .35f; if (!sparks[i].enabled) continue;
                sparks[i].transform.position = sparkFrom[i] + sparkVelocity[i] * age + Vector3.down * 6 * age * age;
                sparks[i].transform.localScale = Vector3.one * (.55f * (1 - age / .35f) + .1f); sparks[i].color = new Color(1,.75f,.35f,1 - age / .35f);
            }
            var me = match.Players[0];
            if (playing && me.Sleeping && me.HouseId >= 0 && !match.Finished && Time.time >= nextCoinAt && session.View.ViewingOwnBase) { CoinPop(HousePoint(me.HouseId,new Vector2(.55f,.15f))); nextCoinAt = Time.time + 1.1f; }
            for (int i = 0; i < coins.Length; i++) {
                float age = Time.time - coinStart[i]; coins[i].enabled = playing && age < 1f; if (!coins[i].enabled) continue;
                coins[i].transform.position = coinFrom[i] + Vector3.up * (age * 1.6f); coins[i].transform.localScale = Vector3.one * (.7f + Mathf.Sin(age * 12) * .05f);
                coins[i].color = new Color(1,1,1,1 - age * age);
            }
            boss.color = Time.time < bossFlashUntil ? new Color(1,.75f,.7f) : Color.white;
            if (Time.time < shakeUntil) { float k = (shakeUntil - Time.time) / .28f; Camera.transform.position += new Vector3(Random.Range(-1f,1f),Random.Range(-1f,1f),0) * .18f * k; }
        }
        // Golden-hour grade over the world: warm sky light from the top, deep dusk at the bottom and edges.
        private void DrawGrade()
        {
            if (grade == null) {
                grade = new Texture2D(64,64,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,name = "Warm grade" };
                Color warm = HudTheme.Hex(0xFF9A4A), dusk = HudTheme.Hex(0x140C1E);
                for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
                    float v = y / 63f, u = Mathf.Abs(x / 63f - .5f) * 2;
                    float top = Mathf.SmoothStep(0,1,(v - .55f) / .45f) * .2f, bottom = Mathf.SmoothStep(0,1,(.35f - v) / .35f) * .3f, side = Mathf.SmoothStep(0,1,(u - .6f) / .4f) * .25f;
                    Color c = Color.Lerp(dusk,warm,top / Mathf.Max(.001f,top + bottom + side)); c.a = Mathf.Clamp01(top + bottom + side);
                    grade.SetPixel(x,y,c);
                }
                grade.Apply();
            }
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),grade,ScaleMode.StretchToFill,true);
        }
        // House numbers painted on each container's front panel, plus the owner's HP above the roof.
        // Numbers under a HUD panel are skipped and ones under the boss fade.
        public void DrawLabels()
        {
            if (match == null || !session.Started || Event.current.type != EventType.Repaint) return;
            var old = GUI.matrix; GUI.matrix = Matrix4x4.identity;
            DrawGrade();
            DrawFeedbackGui();
            float ui = HudTheme.Scale;
            var bossCenter = ScreenPoint(Project(match.Boss.Position) + new Vector3(0,.6f,0));
            float unit = Mathf.Abs(ScreenPoint(Vector3.right).x - ScreenPoint(Vector3.zero).x);
            float bossRadius = unit * 2.2f;
            foreach (var home in match.Houses) {
                var r = houseRects[home.Id];
                Vector2 panel = ScreenPoint(GeneratedHouses ? HousePoint(home.Id,NumberPanel) : new Vector3(r.center.x,r.y + r.height * .35f,0));
                int fontSize = Mathf.Clamp(Mathf.RoundToInt(unit * r.height * (GeneratedHouses ? .11f : .12f)),Mathf.RoundToInt(16 * ui),Mathf.RoundToInt(64 * ui));
                var box = new Rect(panel.x - fontSize * 1.2f,panel.y - fontSize * .7f,fontSize * 2.4f,fontSize * 1.4f);
                if (HudLayout.Blocks(box)) continue;
                bool underBoss = match.Boss.Phase != BossPhase.Dead && Vector2.Distance(box.center,bossCenter) < bossRadius;
                var c = GUI.color; float alpha = underBoss ? .35f : 1;
                GUI.color = new Color(1,1,1,alpha);
                HudTheme.OutlinedText(box,(home.Id + 1).ToString("00"),fontSize,home.OwnerId == 0 ? HudTheme.Hex(0xC9FFD9) : Color.white,TextAnchor.MiddleCenter);
                if (home.OwnerId >= 0) {
                    var top = ScreenPoint(new Vector3(r.center.x,r.yMax + .2f,0));
                    float bw = unit * r.width * .42f, bh = Mathf.Max(6,8 * ui);
                    var bar = new Rect(top.x - bw / 2,top.y - bh,bw,bh);
                    if (!HudLayout.Blocks(bar)) HudTheme.Bar(bar,home.Occupied ? home.Health / home.MaxHealth : 0,home.OwnerId == 0 ? HudTheme.Good : HudTheme.Info);
                }
                GUI.color = c;
            }
            GUI.matrix = old;
        }
        // The house's own container art as a HUD thumbnail.
        public void DrawHouseThumb(Rect r,int house)
        {
            if (Event.current.type != EventType.Repaint || map == null) return;
            var sprite = homes[house] != null ? homes[house].sprite : null; if (sprite == null) return;
            var t = sprite.texture; var s = sprite.textureRect; float k = Mathf.Min(r.width / s.width,r.height / s.height);
            var dst = new Rect(r.center.x - s.width * k / 2,r.center.y - s.height * k / 2,s.width * k,s.height * k);
            GUI.DrawTextureWithTexCoords(dst,t,new Rect(s.x / t.width,s.y / t.height,s.width / t.width,s.height / t.height));
        }
        // Draws a weapon's world sprite as a HUD icon, so cards and rooftops share one art style.
        public void DrawWeaponIcon(Rect r,WeaponKind kind)
        {
            var d = WeaponCatalog.Get(kind); if (d == null || Event.current.type != EventType.Repaint) return;
            var sprite = weaponArt[(int)kind]; var t = sprite.texture; var s = sprite.rect;
            GUI.DrawTextureWithTexCoords(r,t,new Rect(s.x / t.width,s.y / t.height,s.width / t.width,s.height / t.height));
        }
        // Minimap placement: a house centre as a 0..1 position inside the road network (y down).
        public Vector2 MapFraction(Point2 p)
        {
            var world = Project(p); float x0 = Project(map.Nodes[0].Position).x, x1 = x0, y0 = Project(map.Nodes[0].Position).y, y1 = y0;
            foreach (var n in map.Nodes) { var q = Project(n.Position); x0 = Mathf.Min(x0,q.x); x1 = Mathf.Max(x1,q.x); y0 = Mathf.Min(y0,q.y); y1 = Mathf.Max(y1,q.y); }
            return new Vector2(Mathf.InverseLerp(x0,x1,world.x),1 - Mathf.InverseLerp(y0,y1,world.y));
        }
        private void OnDestroy()
        { if (Portraits != null) Portraits.Dispose(); foreach (var sprite in owned) Destroy(sprite); if (lines != null) Destroy(lines); }
    }
}
