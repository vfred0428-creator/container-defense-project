using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // View only: one fixed map, shared atlas, no physics, meshes, lights, or baked gameplay screenshot.
    public sealed class ArenaView : MonoBehaviour
    {
        // Projected world scale. Houses use one uniform size so the art keeps its proportions.
        private const float ScaleX = .8f, ScaleZ = .85f, RoadWidth = 1.6f, HouseSize = 5.4f, WeaponSize = 1.25f;
        public Camera Camera { get; private set; }
        public CharacterPortraits Portraits { get; private set; }
        private MatchSimulation match;
        private GameSession session;
        private MapDefinition map;
        private readonly CharacterVisualController[] actors = new CharacterVisualController[6];
        private readonly SpriteRenderer[] homes = new SpriteRenderer[12];
        private readonly SpriteRenderer[,] weapons = new SpriteRenderer[12,3];
        private readonly float[,] fireUntil = new float[12,3];
        private readonly LineRenderer[,] shots = new LineRenderer[12,3];
        private readonly float[] hitUntil = new float[12],cheerUntil = new float[6],runningUntil = new float[6];
        private readonly Vector3[] previous = new Vector3[6];
        private readonly bool[] facing = new bool[6];
        private readonly List<Sprite> owned = new List<Sprite>();
        private readonly List<GameObject> scenery = new List<GameObject>();
        private readonly List<KeyValuePair<int,SpriteRenderer>> entryMarkers = new List<KeyValuePair<int,SpriteRenderer>>();
        private Sprite[] atlas;
        private Sprite ground,road,crossing,marker;
        private SpriteRenderer boss;
        private LineRenderer route;
        private Material lines;
        private Sprite solid;
        private Rect mapBounds;
        public void Build(CollectionCatalog collections)
        {
            session = GetComponent<GameSession>();
            Camera = new GameObject("High angle 2D camera").AddComponent<Camera>(); Camera.transform.SetParent(transform);
            Camera.transform.position = new Vector3(0,-1.8f,-30); Camera.tag = "MainCamera"; Camera.orthographic = true; Camera.orthographicSize = 19;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = HudTheme.Hex(0x121829); Camera.allowHDR = false;
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
            // Generated tiles when present; flat colours otherwise.
            ground = TileSprite("ground_tile",256); road = TileSprite("road_straight",256 / RoadWidth); crossing = TileSprite("road_cross",256 / RoadWidth);
            var markerTexture = HudIcons.Get("boss_entry_marker");
            marker = Sprite.Create(markerTexture,new Rect(0,0,markerTexture.width,markerTexture.height),new Vector2(.5f,.5f),markerTexture.width); owned.Add(marker);
            var cloud = Resources.Load<Texture2D>("Art2D/boss"); var cloudSprite = Sprite.Create(cloud,new Rect(0,0,cloud.width,cloud.height),new Vector2(.5f,.5f),cloud.width); owned.Add(cloudSprite);
            boss = SpriteObject("Smoke boss",cloudSprite,50); boss.transform.localScale = Vector3.one * 3.6f;
            lines = new Material(Shader.Find("Sprites/Default")); route = MakeLine("Telegraphed road route",.16f,HudTheme.Hex(0xE86A4A,.8f),-3);
            for (int h = 0; h < 12; h++) for (int slot = 0; slot < 3; slot++) {
                weapons[h,slot] = SpriteObject("Defense " + h + "/" + slot,atlas[6],15);
                shots[h,slot] = MakeLine("Pooled tracer " + h + "/" + slot,.055f,new Color(1,.8f,.42f),100); shots[h,slot].positionCount = 2; shots[h,slot].enabled = false;
            }
        }
        private Sprite TileSprite(string id,float pixelsPerUnit)
        {
            var texture = Resources.Load<Texture2D>("Art2D/Generated/" + id); if (texture == null) return null;
            var sprite = Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),pixelsPerUnit,0,SpriteMeshType.FullRect); owned.Add(sprite); return sprite;
        }
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
            float edge = RoadWidth / 2 + 1.1f;
            var lawn = new Rect(minX - edge,minY - edge,maxX - minX + edge * 2,maxY - minY + edge * 2);
            GroundRect("Neighbourhood border",lawn.center,lawn.width + .5f,lawn.height + .5f,HudTheme.Hex(0x2A3350),-101);
            Tiled("Lawn",ground,lawn.center,lawn.width,lawn.height,0,HudTheme.Hex(0x4E6A57),-100);
            // Roads run exactly along the orthogonal road graph, one strip per row and column.
            var rows = new SortedDictionary<float,bool>(); var columns = new SortedDictionary<float,bool>();
            foreach (var n in map.Nodes) { var p = Project(n.Position); rows[Mathf.Round(p.y * 100) / 100] = true; columns[Mathf.Round(p.x * 100) / 100] = true; }
            foreach (float y in rows.Keys) Tiled("Road east-west",road,new Vector3((minX + maxX) / 2,y,0),maxX - minX + RoadWidth,RoadWidth,0,HudTheme.Hex(0x3D4350),-98);
            foreach (float x in columns.Keys) Tiled("Road north-south",road,new Vector3(x,(minY + maxY) / 2,0),maxY - minY + RoadWidth,RoadWidth,90,HudTheme.Hex(0x3D4350),-98);
            foreach (var n in map.Nodes) {
                if (crossing == null) { GroundRect("Crossing",Project(n.Position),RoadWidth,RoadWidth,HudTheme.Hex(0x3D4350),-97); continue; }
                var c = SpriteObject("Crossing " + n.Id,crossing,-97); c.transform.position = Project(n.Position); scenery.Add(c.gameObject);
            }
            for (int i = 0; i < 12; i++) {
                var spawn = map.HouseSpawns[i]; var point = Project(spawn.Center) + new Vector3(0,.25f,0);
                homes[i] = SpriteObject("House " + (i + 1).ToString("00"),atlas[spawn.ColorIndex],10); scenery.Add(homes[i].gameObject);
                homes[i].transform.position = point; homes[i].transform.localScale = Vector3.one * HouseSize;
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
            mapBounds = new Rect(lawn.x - 1.4f,lawn.y - 1.4f,lawn.width + 2.8f,lawn.height + 2.8f);
        }
        public void Bind(MatchSimulation simulation,SkinDefinition humanSkin = null)
        {
            match = simulation; map = match.Map; BuildMap();
            for (int i = 0; i < 6; i++) {
                if (actors[i] != null) { actors[i].gameObject.SetActive(false); Destroy(actors[i].gameObject); }
                string skin = i == 0 && humanSkin != null ? humanSkin.SkinId : CharacterCatalog.Key(match.Players[i].Character.Id) + "_default";
                actors[i] = new GameObject(match.Players[i].Character.Id + " / " + skin).AddComponent<CharacterVisualController>(); actors[i].transform.SetParent(transform); actors[i].Initialize(Portraits.Set(skin));
                previous[i] = Project(match.Players[i].Position); runningUntil[i] = cheerUntil[i] = 0;
            }
            for (int h = 0; h < 12; h++) { hitUntil[h] = 0; for (int s = 0; s < 3; s++) fireUntil[h,s] = 0; }
        }
        public static Vector3 Project(Point2 p) { return new Vector3(p.X * ScaleX,p.Z * ScaleZ,0); }
        // Weapons are drawn on the roof; the simulation's socket point stays authoritative for range.
        public static Vector3 SocketPosition(Point2 socket) { return Project(socket) + new Vector3(0,.55f,0); }
        public Vector2 ScreenPoint(Point2 p) { return ScreenPoint(Project(p)); }
        public Vector2 ScreenPoint(Vector3 world)
        { var value = Camera.WorldToScreenPoint(world); return new Vector2(value.x,Screen.height - value.y); }
        public void Handle(MatchEvent e)
        {
            if (e.Kind == MatchEventKind.Shot && e.HouseId >= 0) fireUntil[e.HouseId,Mathf.Clamp((int)e.Amount,0,2)] = Time.time + .13f;
            if (e.Kind == MatchEventKind.DoorHit && e.HouseId >= 0) hitUntil[e.HouseId] = Time.time + .22f;
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
            if (focus) { var c = Project(match.Houses[viewed.HouseId].Center); Frame(new Rect(c.x - 7,c.y - 4.2f,14,8.4f),5,out size,out destination); }
            else Frame(mapBounds,8,out size,out destination);
            Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize,size,1 - Mathf.Exp(-Time.unscaledDeltaTime * 7));
            Camera.transform.position = Vector3.Lerp(Camera.transform.position,destination,1 - Mathf.Exp(-Time.unscaledDeltaTime * 7));
            foreach (var entry in entryMarkers) {
                bool active = entry.Key == match.Boss.EntryNode && match.Phase == MatchPhase.Preparation;
                entry.Value.transform.localScale = Vector3.one * (active ? 1.5f + Mathf.Sin(t * 4) * .06f : 1.1f);
                entry.Value.color = new Color(1,1,1,active ? 1 : .45f);
            }
            for (int h = 0; h < 12; h++) {
                var home = match.Houses[h]; homes[h].color = home.Destroyed || home.Vacated ? new Color(.42f,.43f,.5f) : Time.time < hitUntil[h] ? new Color(1,.62f,.62f) : Color.white;
                for (int s = 0; s < 3; s++) {
                    var w = home.Weapons[s]; var render = weapons[h,s]; render.gameObject.SetActive(playing && w != null && home.Occupied);
                    if (w != null) {
                        render.sprite = atlas[WeaponCatalog.Get(w.Kind).SpriteIndex]; render.transform.position = SocketPosition(match.WeaponPoint(h,s));
                        render.transform.localScale = Vector3.one * (w.Building ? WeaponSize * .75f : WeaponSize);
                        render.color = w.Building ? new Color(.6f,.75f,.85f,.6f) : Color.white;
                        Vector3 direction = Project(match.Boss.Position) - render.transform.position;
                        render.transform.rotation = Quaternion.Euler(0,0,Mathf.Clamp(-Mathf.Atan2(direction.x,Mathf.Abs(direction.y) + 2) * Mathf.Rad2Deg,-50,50));
                        if (Time.time < fireUntil[h,s]) render.transform.position -= direction.normalized * .1f;
                    }
                    shots[h,s].enabled = playing && w != null && home.Occupied && Time.time < fireUntil[h,s];
                    if (shots[h,s].enabled) { shots[h,s].SetPosition(0,render.transform.position); shots[h,s].SetPosition(1,Project(match.Boss.Position)); }
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
                actors[i].gameObject.SetActive(playing); actors[i].Present(point,pose,t + i * .3f,facing[i],40);
                actors[i].transform.localScale *= .6f;
            }
            boss.gameObject.SetActive(playing && match.Boss.Phase != BossPhase.Dead);
            boss.transform.position = Project(match.Boss.Position) + Vector3.up * Mathf.Sin(t * 2) * .06f;
            boss.transform.localScale = Vector3.one * (3.6f + Mathf.Sin(t * 2) * .05f);
            route.enabled = playing && (match.Boss.Phase == BossPhase.Telegraphing || match.Boss.Phase == BossPhase.Travelling);
            if (route.enabled) {
                route.positionCount = match.Boss.RoutePath.Length + 1; route.SetPosition(0,Project(match.Boss.Position));
                for (int i = 0; i < match.Boss.RoutePath.Length; i++) route.SetPosition(i + 1,Project(match.Boss.RoutePath[i]));
            }
        }
        // Rooftop number plaques with the owner's HP. Plaques that would sit under a HUD panel are skipped
        // and ones under the boss fade, so numbers never fight the HUD, sockets or the boss.
        public void DrawLabels()
        {
            if (match == null || !session.Started || Event.current.type != EventType.Repaint) return;
            var old = GUI.matrix; GUI.matrix = Matrix4x4.identity;
            float ui = HudTheme.Scale, w = 72 * ui, h = 44 * ui;
            var bossCenter = ScreenPoint(Project(match.Boss.Position));
            float bossRadius = Mathf.Abs(ScreenPoint(Project(match.Boss.Position) + Vector3.right * 1.6f).x - bossCenter.x);
            var style = HudTheme.TextStyle(Mathf.RoundToInt(HudTheme.Label * ui),true,TextAnchor.MiddleCenter,false);
            foreach (var home in match.Houses) {
                var anchor = ScreenPoint(Project(home.Center) + new Vector3(0,.25f + HouseSize * .44f,0));
                var r = new Rect(anchor.x - w / 2,anchor.y - h / 2,w,h);
                if (HudLayout.Blocks(r)) continue;
                bool underBoss = match.Boss.Phase != BossPhase.Dead && Vector2.Distance(r.center,bossCenter) < bossRadius + w / 2;
                var c = GUI.color; GUI.color = new Color(1,1,1,underBoss ? .3f : 1);
                HudTheme.Fill(r,HudTheme.Hex(0x1B2238,.92f),10 * ui);
                bool claimed = home.OwnerId >= 0;
                Color accent = !claimed ? HudTheme.Muted : home.OwnerId == 0 ? HudTheme.Good : HudTheme.Info;
                GUI.color = new Color(accent.r,accent.g,accent.b,GUI.color.a);
                GUI.Label(new Rect(r.x,r.y + 2 * ui,r.width,claimed ? r.height - 12 * ui : r.height),(home.Id + 1).ToString("00"),style);
                GUI.color = new Color(1,1,1,underBoss ? .3f : 1);
                if (claimed) HudTheme.Bar(new Rect(r.x + 8 * ui,r.yMax - 12 * ui,r.width - 16 * ui,6 * ui),home.Occupied ? home.Health / home.MaxHealth : 0,accent);
                GUI.color = c;
            }
            GUI.matrix = old;
        }
        // Draws a weapon's world sprite as a HUD icon, so cards and rooftops share one art style.
        public void DrawWeaponIcon(Rect r,WeaponKind kind)
        {
            var d = WeaponCatalog.Get(kind); if (d == null || Event.current.type != EventType.Repaint) return;
            var sprite = atlas[d.SpriteIndex]; var t = sprite.texture; var s = sprite.rect;
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
