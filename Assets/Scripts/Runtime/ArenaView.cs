using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Presentation only. Domain X/Z maps to a shallow 2D courtyard; no physics or meshes.
    public sealed class ArenaView : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public CharacterPortraits Portraits { get; private set; }
        private MatchSimulation match;
        private GameSession session;
        private readonly CharacterVisualController[] actors = new CharacterVisualController[6];
        private readonly SpriteRenderer[] houses = new SpriteRenderer[6];
        private readonly Vector3[] previous = new Vector3[6];
        private readonly bool[] facingRight = new bool[6];
        private readonly float[] runningUntil = new float[6];
        private readonly float[] shotUntil = new float[6], hitUntil = new float[6], cheerUntil = new float[6];
        private readonly LineRenderer[] shots = new LineRenderer[6];
        private readonly List<Sprite> ownedSprites = new List<Sprite>();
        private SpriteRenderer background, boss;
        private Material lineMaterial;
        private GUIStyle label;
        public void Build(CollectionCatalog collections)
        {
            session = GetComponent<GameSession>();
            Camera = new GameObject("2D arena camera").AddComponent<Camera>();
            Camera.transform.SetParent(transform); Camera.transform.position = new Vector3(0,0,-20);
            Camera.tag = "MainCamera"; Camera.orthographic = true; Camera.orthographicSize = 9.5f;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.12f,.13f,.25f);
            Camera.allowHDR = false; Camera.gameObject.AddComponent<AudioListener>(); RenderSettings.fog = false;
            Portraits = new CharacterPortraits(); Portraits.Build(collections);
            background = Image("Sunset neighborhood","yard",new Rect(0,0,1,1),new Vector2(.5f,.5f),-100);
            boss = Image("Storm cloud","boss",new Rect(0,0,1,1),new Vector2(.5f,.5f),-20);
            boss.transform.position = new Vector3(0,5,0); boss.transform.localScale = Vector3.one * 8;
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < 6; i++)
            {
                houses[i] = Image("House " + (i + 1).ToString("00"),"houses",new Rect(i % 3 / 3f,(1 - i / 3) / 2f,1f / 3,.5f),new Vector2(.5f,.1f),0);
                houses[i].transform.position = new Vector3((i - 2.5f) * 4.8f,-.1f,0);
                houses[i].transform.localScale = Vector3.one * 4.9f;
                var line = new GameObject("Pooled weapon tracer " + i).AddComponent<LineRenderer>(); line.transform.SetParent(transform);
                line.sharedMaterial = lineMaterial; line.positionCount = 2; line.startWidth = .065f; line.endWidth = .025f;
                line.startColor = new Color(1,.83f,.35f); line.endColor = new Color(1,.5f,.18f,.1f);
                line.sortingOrder = 40; line.enabled = false; shots[i] = line;
            }
        }
        private SpriteRenderer Image(string name,string asset,Rect uv,Vector2 pivot,int order)
        {
            var texture = Resources.Load<Texture2D>("Art2D/" + asset);
            if (texture == null) throw new System.InvalidOperationException("Missing 2D environment: " + asset);
            var sprite = Sprite.Create(texture,new Rect(uv.x * texture.width,uv.y * texture.height,uv.width * texture.width,uv.height * texture.height),pivot,uv.width * texture.width);
            ownedSprites.Add(sprite);
            var result = new GameObject(name).AddComponent<SpriteRenderer>(); result.transform.SetParent(transform);
            result.sprite = sprite; result.sortingOrder = order; return result;
        }
        public void Bind(MatchSimulation simulation,SkinDefinition humanSkin = null)
        {
            match = simulation;
            for (int i = 0; i < 6; i++)
            {
                if (actors[i] != null) { actors[i].gameObject.SetActive(false); Destroy(actors[i].gameObject); }
                string skin = i == 0 && humanSkin != null ? humanSkin.SkinId : CharacterCatalog.Key(match.Players[i].Character.Id) + "_default";
                actors[i] = new GameObject(match.Players[i].Character.Id + " / " + skin).AddComponent<CharacterVisualController>();
                actors[i].transform.SetParent(transform); actors[i].Initialize(Portraits.Set(skin));
                previous[i] = Project(match.Players[i].Position); shotUntil[i] = hitUntil[i] = cheerUntil[i] = runningUntil[i] = 0;
            }
        }
        public void Handle(MatchEvent e)
        {
            if (e.Kind == MatchEventKind.Shot && e.HouseId >= 0) shotUntil[e.HouseId] = Time.time + .18f;
            if (e.Kind == MatchEventKind.DoorHit && e.HouseId >= 0) hitUntil[e.HouseId] = Time.time + .25f;
            if (e.Kind == MatchEventKind.Upgraded && e.PlayerId >= 0) cheerUntil[e.PlayerId] = Time.time + .5f;
        }
        private static Vector3 Project(Point2 point) { return new Vector3(point.X,point.Z * .43f - 1,0); }
        private void LateUpdate()
        {
            if (match == null) return;
            Camera.orthographicSize = Mathf.Max(9.5f,15.5f / Camera.aspect);
            float t = match.Elapsed;
            background.transform.localScale = Vector3.one * Mathf.Max(Camera.orthographicSize * 2 * Camera.aspect,Camera.orthographicSize * 3);
            bool playing = session.Started;
            for (int i = 0; i < 6; i++)
            {
                var h = match.Houses[i]; var p = match.Players[i];
                bool hit = Time.time < hitUntil[i];
                houses[i].color = h.Destroyed ? new Color(.42f,.4f,.5f) : hit ? new Color(1,.55f,.55f) : Color.white;
                houses[i].transform.localRotation = Quaternion.Euler(0,0,hit ? Mathf.Sin(t * 70) * 1.5f : 0);
                Vector3 point = Project(p.Position);
                if ((point - previous[i]).sqrMagnitude > .00001f) runningUntil[i] = match.Elapsed + .12f;
                bool moving = match.Elapsed < runningUntil[i];
                if (moving && Mathf.Abs(point.x - previous[i].x) > .001f) facingRight[i] = point.x > previous[i].x;
                previous[i] = point;
                CharacterPose pose = moving ? CharacterPose.Run : CharacterPose.Idle;
                if (p.Sleeping) { pose = CharacterPose.Sleep; point = Project(match.Houses[p.HouseId].Entry) + new Vector3(0,.7f,0); }
                if (p.HouseId >= 0 && Time.time < shotUntil[p.HouseId] && !p.Sleeping) pose = CharacterPose.Attack;
                if (Time.time < cheerUntil[i]) pose = CharacterPose.Upgrade;
                if (p.HouseId >= 0 && Time.time < hitUntil[p.HouseId]) pose = CharacterPose.Hurt;
                if (match.Finished && !p.Eliminated) pose = CharacterPose.Victory;
                if (p.Eliminated) pose = CharacterPose.Eliminated;
                actors[i].gameObject.SetActive(playing);
                actors[i].Present(point,pose,t + i * .3f,facingRight[i],20 - Mathf.RoundToInt(point.y));
                shots[i].enabled = playing && Time.time < shotUntil[i] && !h.Destroyed;
                if (shots[i].enabled) { shots[i].SetPosition(0,houses[i].transform.position + Vector3.up * 2); shots[i].SetPosition(1,boss.transform.position); }
            }
            boss.gameObject.SetActive(match.Boss.Phase != BossPhase.Dead);
            float targetX = match.Boss.TargetHouseId >= 0 ? match.Houses[match.Boss.TargetHouseId].Entry.X * .48f : 0;
            if (!session.Paused) boss.transform.position = Vector3.Lerp(boss.transform.position,new Vector3(targetX,5 + Mathf.Sin(t * 1.5f) * .15f,0),Time.deltaTime * 2);
            boss.transform.localScale = Vector3.one * (8 + Mathf.Sin(t * 2) * .12f);
        }
        public void DrawLabels()
        {
            if (match == null) return;
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.identity;
            if (label == null) label = new GUIStyle { font = Resources.Load<Font>("Fonts/Nunito"),fontStyle = FontStyle.Bold,alignment = TextAnchor.MiddleCenter,normal = { textColor = Color.white } };
            label.fontSize = Mathf.RoundToInt(16 * Screen.height / 900f);
            for (int i = 0; i < 6; i++)
            {
                var h = match.Houses[i]; var pos = Camera.WorldToScreenPoint(houses[i].transform.position + Vector3.up * 3.2f);
                GUI.Label(new Rect(pos.x - 60,Screen.height - pos.y,120,30),(i + 1).ToString("00"),label);
                pos = Camera.WorldToScreenPoint(houses[i].transform.position + Vector3.down * .25f);
                string text = h.Destroyed ? "OUT" : h.OwnerId < 0 ? "CLAIM" : h.OwnerId == 0 ? "MY HOUSE" : match.Players[h.OwnerId].Name;
                if (match.Boss.TargetHouseId == i && !match.Finished) text = "! " + text;
                if (session.Started) GUI.Label(new Rect(pos.x - 65,Screen.height - pos.y,130,25),text,label);
            }
            GUI.matrix = matrix;
        }
        private void OnDestroy()
        { if (Portraits != null) Portraits.Dispose(); foreach (var sprite in ownedSprites) Destroy(sprite); if (lineMaterial != null) Destroy(lineMaterial); }
    }
}
