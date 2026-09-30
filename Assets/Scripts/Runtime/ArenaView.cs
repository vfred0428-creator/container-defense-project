using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // View only: one fixed map, shared atlas, no physics, meshes, lights, or baked gameplay screenshot.
    public sealed class ArenaView : MonoBehaviour
    {
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
        private Sprite[] atlas;
        private SpriteRenderer boss;
        private LineRenderer route;
        private Material lines;
        private Sprite solid;
        private GUIStyle label;
        public void Build(CollectionCatalog collections)
        {
            session = GetComponent<GameSession>();
            Camera = new GameObject("High angle 2D camera").AddComponent<Camera>(); Camera.transform.SetParent(transform);
            Camera.transform.position = new Vector3(0,-1.8f,-30); Camera.tag = "MainCamera"; Camera.orthographic = true; Camera.orthographicSize = 19;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.065f,.095f,.16f); Camera.allowHDR = false;
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
            var cloud = Resources.Load<Texture2D>("Art2D/boss"); var cloudSprite = Sprite.Create(cloud,new Rect(0,0,cloud.width,cloud.height),new Vector2(.5f,.5f),cloud.width); owned.Add(cloudSprite);
            boss = SpriteObject("Smoke boss",cloudSprite,50); boss.transform.localScale = Vector3.one * 4.6f;
            lines = new Material(Shader.Find("Sprites/Default")); route = MakeLine("Telegraphed road route",.08f,new Color(1,.48f,.35f,.65f),-3);
            for (int h = 0; h < 12; h++) for (int slot = 0; slot < 3; slot++) {
                weapons[h,slot] = SpriteObject("Defense " + h + "/" + slot,atlas[6],15);
                shots[h,slot] = MakeLine("Pooled tracer " + h + "/" + slot,.055f,new Color(1,.8f,.42f),100); shots[h,slot].positionCount = 2; shots[h,slot].enabled = false;
            }
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
        private void BuildMap()
        {
            foreach (var go in scenery) { go.SetActive(false); Destroy(go); } scenery.Clear();
            GroundRect("Yard foundation",Vector3.zero,43,29,new Color(.17f,.21f,.29f),-100);
            GroundRect("Quiet asphalt",Vector3.zero,39.5f,26.5f,new Color(.28f,.3f,.36f),-99);
            for (int row = 0; row < 5; row++) {
                float y = Project(new Point2(0,16 - row * 8)).y;
                GroundRect("Road lane",new Vector3(0,y,0),39.5f,1.65f,new Color(.34f,.35f,.4f),-98);
                for (int dash = 0; dash < 17; dash++) GroundRect("Lane dash",new Vector3(-18 + dash * 2.2f,y,0),.8f,.08f,new Color(.56f,.55f,.52f),-97);
            }
            for (int col = 0; col < 4; col++) {
                float x = Project(new Point2(-16.5f + col * 11,0)).x;
                GroundRect("North south lane",new Vector3(x,0,0),1.65f,26.5f,new Color(.34f,.35f,.4f),-98);
                for (int dash = 0; dash < 13; dash++) GroundRect("Lane dash",new Vector3(x,-12 + dash * 2,0),.08f,.65f,new Color(.56f,.55f,.52f),-97);
            }
            // Open common starting area between rows two and three.
            GroundRect("Common plaza",Project(new Point2(0,0)),6.2f,2.15f,new Color(.39f,.39f,.44f),-95);
            for (int i = 0; i < 12; i++) {
                var spawn = map.HouseSpawns[i]; var point = Project(spawn.Center);
                GroundRect("House pad",point + Vector3.down * .5f,8.8f,4.7f,new Color(.21f,.24f,.3f),-80);
                homes[i] = SpriteObject("House " + (i + 1).ToString("00"),atlas[spawn.ColorIndex],10); scenery.Add(homes[i].gameObject);
                homes[i].transform.position = point; homes[i].transform.localScale = new Vector3(8.5f,5.6f,1);
                for (int side = -1; side <= 1; side += 2) {
                    var lamp = SpriteObject("Warm street lamp",atlas[10],18); scenery.Add(lamp.gameObject);
                    lamp.transform.position = point + new Vector3(side * 4.4f,-1.5f,0); lamp.transform.localScale = Vector3.one * 1.25f;
                }
            }
            for (int i = 0; i < 12; i++) {
                float x = i % 2 == 0 ? -21 : 21, y = -12 + i / 2 * 4.6f;
                GroundRect("Peripheral shipping container",new Vector3(x,y,0),2.1f,3.6f,new Color(.13f + (i % 3) * .025f,.17f,.26f),-60);
                var decor = SpriteObject("Crates and shrub",atlas[11],15); scenery.Add(decor.gameObject); decor.transform.position = new Vector3(x * .97f,y + 1.2f,0); decor.transform.localScale = Vector3.one * 1.8f;
            }
            for (int side = -1; side <= 1; side += 2) {
                GroundRect("Fence",new Vector3(side * 20.2f,0,0),.12f,27,new Color(.08f,.12f,.2f),-50);
                for (int i = 0; i < 15; i++) GroundRect("Fence post",new Vector3(side * 20.2f,-13 + i * 1.9f,0),.22f,.4f,new Color(.1f,.15f,.23f),-49);
            }
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
        public static Vector3 Project(Point2 p) { return new Vector3(p.X * 1.15f,p.Z * .68f,0); }
        public Vector2 ScreenPoint(Point2 p)
        { var value = Camera.WorldToScreenPoint(Project(p)); return new Vector2(value.x,Screen.height - value.y); }
        public void Handle(MatchEvent e)
        {
            if (e.Kind == MatchEventKind.Shot && e.HouseId >= 0) fireUntil[e.HouseId,Mathf.Clamp((int)e.Amount,0,2)] = Time.time + .13f;
            if (e.Kind == MatchEventKind.DoorHit && e.HouseId >= 0) hitUntil[e.HouseId] = Time.time + .22f;
            if ((e.Kind == MatchEventKind.Upgraded || e.Kind == MatchEventKind.Placed) && e.PlayerId >= 0) cheerUntil[e.PlayerId] = Time.time + .55f;
        }
        private void LateUpdate()
        {
            if (match == null) return;
            bool playing = session.Started; float t = match.Elapsed;
            var viewed = match.Players[session.ViewedPlayer]; bool focus = playing && !session.Overview && viewed.HouseId >= 0;
            float size = focus ? Mathf.Max(8.5f,11 / Camera.aspect) : Mathf.Max(18,23 / Camera.aspect);
            Vector3 destination = focus ? Project(match.Houses[viewed.HouseId].Center) + new Vector3(0,-.7f,-30) : new Vector3(0,-1.2f,-30);
            Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize,size,1 - Mathf.Exp(-Time.unscaledDeltaTime * 7));
            Camera.transform.position = Vector3.Lerp(Camera.transform.position,destination,1 - Mathf.Exp(-Time.unscaledDeltaTime * 7));
            for (int h = 0; h < 12; h++) {
                var home = match.Houses[h]; homes[h].color = home.Destroyed ? new Color(.38f,.39f,.45f) : Time.time < hitUntil[h] ? new Color(1,.62f,.62f) : Color.white;
                for (int s = 0; s < 3; s++) {
                    var w = home.Weapons[s]; var render = weapons[h,s]; render.gameObject.SetActive(playing && w != null && home.Occupied);
                    if (w != null) {
                        render.sprite = atlas[WeaponCatalog.Get(w.Kind).SpriteIndex]; render.transform.position = Project(match.WeaponPoint(h,s));
                        render.transform.localScale = Vector3.one * (w.Building ? 1.25f : 1.8f);
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
                actors[i].transform.localScale *= .65f;
            }
            boss.gameObject.SetActive(playing && match.Boss.Phase != BossPhase.Dead);
            boss.transform.position = Project(match.Boss.Position) + Vector3.up * Mathf.Sin(t * 2) * .06f;
            boss.transform.localScale = Vector3.one * (4.2f + Mathf.Sin(t * 2) * .06f);
            route.enabled = playing && (match.Boss.Phase == BossPhase.Telegraphing || match.Boss.Phase == BossPhase.Travelling);
            if (route.enabled) {
                route.positionCount = match.Boss.RoutePath.Length;
                for (int i = 0; i < match.Boss.RoutePath.Length; i++) route.SetPosition(i,Project(match.Boss.RoutePath[i]));
            }
        }
        public void DrawLabels()
        {
            if (match == null || !session.Started) return;
            var old = GUI.matrix; GUI.matrix = Matrix4x4.identity;
            if (label == null) label = new GUIStyle { font = Resources.Load<Font>("Fonts/NunitoBold"),alignment = TextAnchor.MiddleCenter,normal = { textColor = Color.white } };
            label.fontSize = Mathf.Clamp(Mathf.RoundToInt(Screen.height / Camera.orthographicSize * .35f),12,28);
            foreach (var home in match.Houses) {
                var p = ScreenPoint(new Point2(home.Center.X,home.Center.Z - .65f));
                GUI.Label(new UnityEngine.Rect(p.x - 46,p.y - 14,92,30),(home.Id + 1).ToString("00"),label);
                if (home.OwnerId >= 0) {
                    p = ScreenPoint(new Point2(home.Center.X,home.Center.Z + 3.4f));
                    Color oldColor = GUI.color; GUI.color = new Color(.06f,.09f,.15f); GUI.DrawTexture(new UnityEngine.Rect(p.x - 40,p.y,80,6),Texture2D.whiteTexture);
                    GUI.color = home.OwnerId == 0 ? new Color(.3f,.95f,.69f) : new Color(.44f,.65f,.93f); GUI.DrawTexture(new UnityEngine.Rect(p.x - 40,p.y,80 * home.Health / home.MaxHealth,6),Texture2D.whiteTexture); GUI.color = oldColor;
                }
            }
            GUI.matrix = old;
        }
        private void OnDestroy()
        { if (Portraits != null) Portraits.Dispose(); foreach (var sprite in owned) Destroy(sprite); if (lines != null) Destroy(lines); }
    }
}

