using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    public sealed class ArenaView : MonoBehaviour
    {
        public Camera Camera { get; private set; }
        public CharacterPortraits Portraits { get; private set; }
        private Transform worldRoot;
        private ToyFactory art;
        private MatchSimulation match;
        private readonly Transform[] doors = new Transform[6], barrels = new Transform[6], healthBars = new Transform[6];
        private readonly Transform[] avatars = new Transform[6], markers = new Transform[6];
        private readonly TextMesh[] labels = new TextMesh[6], sleepLabels = new TextMesh[6];
        private readonly LineRenderer[] shots = new LineRenderer[6];
        private readonly float[] shotUntil = new float[6], hitUntil = new float[6];
        private readonly bool[] wasSleeping = new bool[6];
        private Transform boss, targetMarker;
        private LineRenderer attack;
        private float attackUntil;

        public void Build(CollectionCatalog collections)
        {
            art = new ToyFactory();
            var root = art.Root("Container yard", transform, Vector3.zero);
            worldRoot = root;
            var yard = new YardBuilder(art, root); yard.BuildGround();
            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) * 4.8f;
                yard.BuildHouse(i, x, out doors[i], out barrels[i], out healthBars[i], out labels[i]);
                avatars[i] = ResidentFactory.Create(art,CharacterId.Milo,root);
                sleepLabels[i] = art.Text("z z z", root, Vector3.zero, 0.09f, YardBuilder.Warm);
                sleepLabels[i].transform.rotation = Quaternion.Euler(36, 0, 0);
                markers[i] = art.Shape("Claim marker", PrimitiveType.Cylinder, root, new Vector3(x,0.04f,2.1f), new Vector3(1.3f,0.03f,1.3f), YardBuilder.Colors[i], true);
                shots[i] = art.Line("Weapon tracer", root, YardBuilder.Warm, 0.065f); shots[i].enabled = false;
            }
            boss = CreateBoss(root);
            targetMarker = art.Shape("Boss target", PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(2.5f,0.025f,2.5f), new Color(0.9f,0.18f,0.28f));
            attack = art.Line("Boss impact", root, new Color(1,0.18f,0.25f), 0.18f); attack.enabled = false;
            SetupCamera();
            Portraits = new CharacterPortraits(); Portraits.Build(art,transform,collections);
        }

        private void SetupCamera()
        {
            Camera = new GameObject("Arena camera").AddComponent<Camera>(); Camera.transform.SetParent(transform);
            Camera.tag = "MainCamera"; Camera.transform.position = new Vector3(0,21,-25);
            Camera.transform.LookAt(new Vector3(0,0,1)); Camera.orthographic = true;
            Camera.orthographicSize = 11.2f; Camera.nearClipPlane = 0.1f; Camera.farClipPlane = 100;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(0.31f,0.28f,0.43f);
            Camera.allowHDR = true; Camera.gameObject.AddComponent<AudioListener>();
            Camera.cullingMask &= ~(1 << 8);
            RenderSettings.ambientLight = new Color(0.6f,0.56f,0.7f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 37; RenderSettings.fogEndDistance = 72;
            RenderSettings.fogColor = Camera.backgroundColor;
            var sun = new GameObject("Warm sunset").AddComponent<Light>(); sun.transform.SetParent(transform);
            sun.type = LightType.Directional; sun.color = new Color(1,0.72f,0.49f); sun.intensity = 1.35f;
            sun.transform.rotation = Quaternion.Euler(36,-38,0); sun.shadows = LightShadows.Soft;
            QualitySettings.shadowDistance = 65;
            var fill = new GameObject("Cool fill").AddComponent<Light>(); fill.transform.SetParent(transform);
            fill.type = LightType.Directional; fill.color = new Color(0.5f,0.62f,1); fill.intensity = 0.55f;
            fill.transform.rotation = Quaternion.Euler(50,145,0);
        }

        private Transform CreateBoss(Transform parent)
        {
            var cloud = art.Root("Storm cloud boss", parent, Vector3.zero);
            Color smoke = new Color(0.19f,0.14f,0.28f);
            art.Ball("Cloud core", cloud, Vector3.zero, new Vector3(3.5f,2.8f,2.3f), smoke);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * 2 / 10;
                art.Ball("Cloud puff", cloud, new Vector3(Mathf.Cos(angle) * 1.5f,Mathf.Sin(angle) * 1.15f,0.15f), Vector3.one * (1.1f + i % 3 * 0.18f), smoke * (0.85f + i % 3 * 0.12f));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = art.Ball("Glowing eye", cloud, new Vector3(side * 0.65f,0.2f,-1.12f), new Vector3(0.53f,0.38f,0.14f), new Color(1,0.18f,0.17f), true);
                eye.localRotation = Quaternion.Euler(0,0,-side * 22);
            }
            for (int i = 0; i < 5; i++)
            {
                var tooth = art.Box("Jagged grin", cloud, new Vector3((i - 2) * 0.24f,-0.52f + i % 2 * 0.1f,-1.15f), new Vector3(0.23f,0.22f,0.1f), new Color(1,0.2f,0.18f), true);
                tooth.localRotation = Quaternion.Euler(0,0,45);
            }
            return cloud;
        }

        public void Bind(MatchSimulation simulation, SkinDefinition humanSkin = null)
        {
            match = simulation;
            for (int i = 0; i < 6; i++)
            {
                shotUntil[i] = hitUntil[i] = 0; doors[i].localRotation = Quaternion.identity;
                avatars[i].gameObject.SetActive(false); Destroy(avatars[i].gameObject);
                avatars[i] = ResidentFactory.Create(art,match.Players[i].Character.Id,worldRoot,i == 0 ? humanSkin : null);
                wasSleeping[i] = false;
            }
            attackUntil = 0;
        }
        public void Handle(MatchEvent e)
        {
            if (e.Kind == MatchEventKind.Shot) shotUntil[e.HouseId] = Time.time + 0.13f;
            if (e.Kind == MatchEventKind.DoorHit) { hitUntil[e.HouseId] = Time.time + 0.22f; attackUntil = Time.time + 0.16f; }
        }

        private void LateUpdate()
        {
            if (match == null) return;
            Camera.orthographicSize = Mathf.Max(11.2f, 18f / Mathf.Max(0.5f,Camera.aspect));
            float t = Time.time;
            for (int i = 0; i < 6; i++)
            {
                HouseState h = match.Houses[i]; PlayerState p = match.Players[i];
                float ratio = Mathf.Clamp01(h.Health / h.MaxHealth);
                healthBars[i].localScale = new Vector3(3.55f * ratio,0.12f,0.08f);
                healthBars[i].localPosition = new Vector3(-1.775f * (1 - ratio),3.92f,-2.18f);
                healthBars[i].gameObject.SetActive(h.OwnerId >= 0 && !h.Destroyed);
                doors[i].localRotation = Quaternion.Euler(0,0,h.Destroyed ? 76 : t < hitUntil[i] ? Mathf.Sin(t * 90) * 5 : 0);
                labels[i].text = h.Destroyed ? "ELIMINATED" : h.OwnerId < 0 ? "AVAILABLE" : h.OwnerId == 0 ? "YOUR HOUSE" : match.Players[h.OwnerId].Name;
                labels[i].color = h.OwnerId == 0 ? YardBuilder.Warm : Color.white;
                markers[i].gameObject.SetActive(h.OwnerId < 0 && match.Phase == MatchPhase.Preparation);
                avatars[i].gameObject.SetActive(!p.Eliminated);
                Vector3 next = new Vector3(p.Position.X,p.Sleeping ? 1.4f : 0,p.Position.Z);
                Vector3 motion = next - avatars[i].position;
                motion.y = 0;
                bool moving = motion.sqrMagnitude > .0001f;
                if (!p.Sleeping && moving) next.y += Mathf.Abs(Mathf.Sin(t * 11 + i)) * .07f;
                avatars[i].position = next;
                if (p.Sleeping) avatars[i].rotation = Quaternion.Euler(90,0,0);
                else if (moving) avatars[i].rotation = Quaternion.Slerp(avatars[i].rotation,Quaternion.LookRotation(-new Vector3(motion.x,0,motion.z)),Time.deltaTime * 14);
                else if (wasSleeping[i]) avatars[i].rotation = Quaternion.identity;
                wasSleeping[i] = p.Sleeping;
                sleepLabels[i].gameObject.SetActive(p.Sleeping);
                if (p.HouseId >= 0)
                    sleepLabels[i].transform.position = new Vector3(match.Houses[p.HouseId].Entry.X,5.3f + Mathf.Sin(t * 2 + i) * 0.1f,3.4f);
                shots[i].enabled = t < shotUntil[i] && !h.Destroyed;
                if (shots[i].enabled) { shots[i].SetPosition(0,barrels[i].position); shots[i].SetPosition(1,boss.position); }
            }
            bool waiting = match.Phase == MatchPhase.Preparation;
            boss.gameObject.SetActive(match.Boss.Phase != BossPhase.Dead);
            boss.position = waiting ? new Vector3(0,4.2f,8) : new Vector3(match.Boss.Position.X,3.2f + Mathf.Sin(t * 2) * 0.2f,match.Boss.Position.Z);
            boss.localScale = Vector3.one * (waiting ? 1.15f : 1);
            bool targeting = match.Boss.TargetHouseId >= 0 && !match.Finished;
            targetMarker.gameObject.SetActive(targeting);
            if (targeting)
            {
                var target = match.Houses[match.Boss.TargetHouseId];
                targetMarker.position = new Vector3(target.Entry.X,0.06f,target.Entry.Z);
                targetMarker.localScale = new Vector3(2.1f + Mathf.Sin(t * 7) * 0.25f,0.025f,2.1f + Mathf.Sin(t * 7) * 0.25f);
            }
            attack.enabled = targeting && t < attackUntil;
            if (attack.enabled) { attack.SetPosition(0,boss.position); attack.SetPosition(1,doors[match.Boss.TargetHouseId].position); }
        }
        private void OnDestroy() { if (Portraits != null) Portraits.Dispose(); if (art != null) art.Dispose(); }
    }
}
