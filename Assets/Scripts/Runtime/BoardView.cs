using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // TFT-style own board: a separate stage far from the neighbourhood map that shows ONE house (yours, or the
    // one you are scouting) large, with its yard, build pads, weapons and decorations, framed by container
    // stacks, the dock and crate walls so no other house is ever in view. Pure presentation over the same
    // MatchSimulation state; houses, routes and rules are untouched.
    public sealed partial class ArenaView
    {
        public static readonly Vector3 BoardCenter = new Vector3(1000,0,0);
        private const float Cell = 2, BoardHouseWidth = 11.5f;
        private static float BoardLeft { get { return BoardCenter.x - YardLayout.Width * Cell / 2; } }
        private static float BoardTop { get { return BoardCenter.y + YardLayout.Height * Cell / 2; } }
        private readonly SpriteRenderer[] boardPads = new SpriteRenderer[YardLayout.PadCount], boardPadRings = new SpriteRenderer[YardLayout.PadCount], boardDecor = new SpriteRenderer[24], boardDecorShadow = new SpriteRenderer[24];
        private SpriteRenderer boardHouse, boardBoss, boardBossShadow;
        private Sprite padSprite, padRingSprite;
        private Rect boardHouseRect, boardFrame;
        private readonly int[] padGlowFrame = new int[YardLayout.PadCount];
        private int boardHouseId = -1, boardYardOwner = -2;
        private YardData[] yards = new YardData[6];
        private Vector3 boardBossPoint;
        private bool boardBossVisible;
        public int BoardHouse { get { return boardHouseId; } }
        // World centre of a yard cell (x right, y down from the top-left of the board).
        public static Vector3 CellCenter(int x,int y) { return new Vector3(BoardLeft + (x + .5f) * Cell,BoardTop - (y + .5f) * Cell,0); }
        private static Vector3 PadPoint(int pad) { return CellCenter(YardLayout.PadX(pad),YardLayout.PadY(pad)); }
        public void SetYards(YardData[] perPlayer) { yards = perPlayer ?? new YardData[6]; boardYardOwner = -2; }

        private static Texture2D PadTexture(bool ring)
        {
            const int n = 96; var t = new Texture2D(n,n,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,name = ring ? "Pad ring" : "Pad" };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) {
                float u = (x + .5f) / n * 2 - 1, v = ((y + .5f) / n * 2 - 1) * 1.7f, r = Mathf.Sqrt(u * u + v * v); Color c = Color.clear;
                if (ring) { if (r > .78f && r < .95f) c = HudTheme.Hex(0xFFD04A); }
                else { if (r < .95f) c = HudTheme.Hex(0x2A2433); if (r < .86f) c = HudTheme.Hex(0x5B5E6E); if (r < .7f) c = HudTheme.Hex(0x6E7283); if (r < .25f) c = HudTheme.Hex(0x4B4E5C); }
                c.a *= Mathf.Clamp01((1 - r) * 12); t.SetPixel(x,y,c);
            }
            t.Apply(); return t;
        }
        private void BuildBoard()
        {
            var pt = PadTexture(false); padSprite = Sprite.Create(pt,new Rect(0,0,pt.width,pt.height),new Vector2(.5f,.5f),pt.width / 1.9f); owned.Add(padSprite);
            var rt = PadTexture(true); padRingSprite = Sprite.Create(rt,new Rect(0,0,rt.width,rt.height),new Vector2(.5f,.5f),rt.width / 1.9f); owned.Add(padRingSprite);
            float w = YardLayout.Width * Cell, h = YardLayout.Height * Cell;
            // Ground and frame: asphalt yard, container stacks behind, the dock in front, crate walls at the sides.
            var yard = new Rect(BoardLeft,BoardTop - h,w,h);
            Board(Tiled2("Board ground",ground,yard.center,w + 2,h + 2,HudTheme.Hex(0x2E3242),-120));
            if (edge != null) { var s = SpriteObject("Board north stacks",edge,-110); s.drawMode = SpriteDrawMode.Tiled; s.size = new Vector2(w + 12,4.5f); s.transform.position = new Vector3(yard.center.x,yard.yMax + 1.6f,0); }
            if (dock != null) { var s = SpriteObject("Board south dock",dock,Depth(yard.yMin - 4)); s.drawMode = SpriteDrawMode.Tiled; s.size = new Vector2(w + 12,4f); s.transform.position = new Vector3(yard.center.x,yard.yMin - 2.6f,0); }
            Sprite crate, pallet; props.TryGetValue("prop_crate",out crate); props.TryGetValue("prop_pallets",out pallet);
            for (int side = -1; side <= 1; side += 2) for (int i = 0; i < 9; i++) {
                var sprite = i % 3 == 1 ? pallet : crate; if (sprite == null) continue;
                var p = new Vector3(yard.center.x + side * (w / 2 + .9f),yard.yMin + .6f + i * (h / 8.5f),0);
                var r = SpriteObject("Board wall",sprite,Depth(p.y)); r.transform.position = p; r.transform.localScale = Vector3.one * 1.6f;
            }
            boardFrame = new Rect(yard.x - 2.2f,yard.y - 2.8f,yard.width + 4.4f,yard.height + 5.6f);
            boardHouse = SpriteObject("Board house",houseArt[0] ?? atlas[0],0);
            var houseShadow = Tinted("Board house shadow",soft,new Color(0,0,0,.45f),-100);
            for (int i = 0; i < YardLayout.PadCount; i++) {
                var shadow = Tinted("Pad shadow",soft,new Color(0,0,0,.3f),-101); shadow.transform.position = PadPoint(i) + new Vector3(0,-.15f,0); shadow.transform.localScale = new Vector3(2.2f,.8f,1);
                boardPads[i] = SpriteObject("Build pad " + i,padSprite,-99); boardPads[i].transform.position = PadPoint(i);
                boardPadRings[i] = SpriteObject("Pad highlight " + i,padRingSprite,-98); boardPadRings[i].transform.position = PadPoint(i); boardPadRings[i].enabled = false;
            }
            for (int i = 0; i < boardDecor.Length; i++) {
                boardDecorShadow[i] = Tinted("Decor shadow",soft,new Color(0,0,0,.4f),0); boardDecorShadow[i].enabled = false;
                boardDecor[i] = SpriteObject("Decoration " + i,padSprite,0); boardDecor[i].enabled = false;
            }
            boardBoss = SpriteObject("Board boss",boss.sprite,950); boardBoss.enabled = false;
            boardBossShadow = Tinted("Board boss shadow",soft,new Color(0,0,0,.35f),-90); boardBossShadow.enabled = false;
            // Place the house once so its rect is valid before the first frame.
            LayoutBoardHouse(houseArt[0] != null ? houseArt[0] : atlas[0]);
            houseShadow.transform.position = new Vector3(boardHouseRect.center.x + .5f,boardHouseRect.y + boardHouseRect.height * .2f,0); houseShadow.transform.localScale = new Vector3(boardHouseRect.width * 1.1f,boardHouseRect.height * .5f,1);
        }
        private SpriteRenderer Tiled2(string name,Sprite tile,Vector2 center,float w,float h,Color fallback,int order)
        {
            var r = SpriteObject(name,tile ?? solid,order);
            if (tile != null) { r.drawMode = SpriteDrawMode.Tiled; r.size = new Vector2(w,h); } else { r.transform.localScale = new Vector3(w,h,1); r.color = fallback; }
            r.transform.position = center; return r;
        }
        private static void Board(SpriteRenderer r) { }
        // The house art sits in the top-centre cells with its door on the path.
        private void LayoutBoardHouse(Sprite art)
        {
            boardHouse.sprite = art; float aspect = art.rect.height / art.rect.width, h = BoardHouseWidth * aspect;
            var foot = new Vector3(CellCenter(5,2).x + Cell / 2,BoardTop - 3 * Cell + .55f,0);
            boardHouse.transform.position = foot; boardHouse.transform.localScale = Vector3.one * BoardHouseWidth;
            boardHouse.sortingOrder = Depth(foot.y);
            var pivot = art == atlas[0] ? new Vector2(.5f,.5f) : HousePivot;
            boardHouseRect = new Rect(foot.x - pivot.x * BoardHouseWidth,foot.y - pivot.y * h,BoardHouseWidth,h);
        }
        private bool OnBoard(int house) { return house >= 0 && house == boardHouseId; }
        private Vector3 BoardHousePoint(Vector2 fromTopLeft)
        { var r = boardHouseRect; return new Vector3(r.x + r.width * fromTopLeft.x,r.yMax - r.height * fromTopLeft.y,0); }
        // Where effects should aim at the boss in the current view.
        private Vector3 BossAim()
        {
            if (boardHouseId < 0) return Project(match.Boss.Position) + new Vector3(0,.6f,0);
            if (boardBossVisible) return boardBossPoint;
            var dir = (Vector2)(Project(match.Boss.Position) - Project(match.Houses[boardHouseId].Center)); if (dir.sqrMagnitude < .01f) dir = Vector2.up;
            return BoardCenter + (Vector3)(dir.normalized * 22);
        }
        // Called from LateUpdate: which house the board shows, its paint, pads, weapons, decorations and the boss.
        private void UpdateBoard(bool playing)
        {
            var view = session.View; int house = playing && view.Mode == ViewMode.Base ? match.Players[view.ViewedPlayer].HouseId : -1;
            boardHouseId = house;
            boardBoss.enabled = boardBossShadow.enabled = boardBossVisible = false;
            if (house < 0) return;
            var home = match.Houses[house]; var art = homes[house] != null ? homes[house].sprite : houseArt[0];
            if (boardHouse.sprite != art) LayoutBoardHouse(art);
            boardHouse.color = homes[house] != null ? homes[house].color : Color.white;
            int owner = home.OwnerId;
            if (owner != boardYardOwner) { ShowYard(owner >= 0 && owner < yards.Length ? yards[owner] : null); boardYardOwner = owner; }
            // The HUD asks for glows during OnGUI, which runs after rendering, so honour last frame's request.
            for (int i = 0; i < YardLayout.PadCount; i++) {
                boardPadRings[i].enabled = padGlowFrame[i] > 0 && padGlowFrame[i] >= Time.frameCount - 1;
                boardPadRings[i].transform.localScale = Vector3.one * (1.15f + Mathf.Sin(Time.unscaledTime * 6) * .08f);
            }
            // Boss: enters from the top edge as it travels to this house, hovers above it while attacking.
            if (match.Boss.TargetHouseId == house && (match.Boss.Phase == BossPhase.Travelling || match.Boss.Phase == BossPhase.Attacking)) {
                var approach = new Point2(home.Entry.X,home.Entry.Z - 1.5f); float dist = match.Boss.Position.Distance(approach);
                float k = match.Boss.Phase == BossPhase.Attacking ? 0 : Mathf.Clamp01(dist / 14f);
                boardBossPoint = BoardHousePoint(new Vector2(.5f,.05f)) + new Vector3(0,1.5f + k * 9,0);
                boardBossVisible = true; boardBoss.enabled = true; boardBossShadow.enabled = true;
                boardBoss.sprite = boss.sprite; boardBoss.color = boss.color; boardBoss.transform.position = boardBossPoint + Vector3.up * Mathf.Sin(match.Elapsed * 2) * .12f;
                boardBoss.transform.localScale = Vector3.one * 5.6f;
                boardBossShadow.transform.position = boardBossPoint + new Vector3(0,-3.2f,0); boardBossShadow.transform.localScale = new Vector3(5.5f,1.4f,1);
            }
        }
        private void ShowYard(YardData yard)
        {
            var items = yard != null && yard.Items != null ? yard.Items : new YardItem[0];
            for (int i = 0; i < boardDecor.Length; i++) {
                bool on = i < items.Length; Sprite sprite = null;
                if (on && !props.TryGetValue(items[i].Prop,out sprite)) { if (items[i].Prop == "prop_lamp") sprite = lampSprite; else on = false; }
                if (on && sprite == null) on = false;
                boardDecor[i].enabled = boardDecorShadow[i].enabled = on; if (!on) continue;
                var p = CellCenter(items[i].X,items[i].Y) + new Vector3(0,-.75f,0);
                boardDecor[i].sprite = sprite; boardDecor[i].transform.position = p; boardDecor[i].transform.localScale = Vector3.one * (items[i].Prop == "prop_lamp" ? 1.6f : 1.35f);
                boardDecor[i].sortingOrder = Depth(p.y);
                boardDecorShadow[i].transform.position = p + new Vector3(0,.05f,0); boardDecorShadow[i].transform.localScale = new Vector3(1.3f,.42f,1); boardDecorShadow[i].sortingOrder = Depth(p.y) - 1;
            }
        }
        // Screen rect (GUI pixels) of a build pad, for the placing flow.
        public Rect PadScreenRect(int pad)
        {
            var c = ScreenPoint(PadPoint(pad)); float half = Mathf.Abs(ScreenPoint(PadPoint(pad) + Vector3.right * 1.1f).x - c.x);
            return new Rect(c.x - half,c.y - half,half * 2,half * 2);
        }
        public void HighlightPad(int pad,bool on)
        {
            if (pad >= 0 && pad < padGlowFrame.Length && on) padGlowFrame[pad] = Time.frameCount;
        }
        // Board GUI: Zzz from the window while the owner sleeps, and a compass to the boss when it is off the board.
        private void DrawBoardGui()
        {
            if (boardHouseId < 0) return;
            float ui = HudTheme.Scale; var home = match.Houses[boardHouseId];
            // House number on the container panel, as on the map.
            { var n = ScreenPoint(BoardHousePoint(NumberPanel)); int fs = Mathf.RoundToInt(Mathf.Abs(ScreenPoint(BoardHousePoint(new Vector2(1,0))).x - ScreenPoint(BoardHousePoint(Vector2.zero)).x) * .1f);
              HudTheme.OutlinedText(new Rect(n.x - fs * 1.3f,n.y - fs * .75f,fs * 2.6f,fs * 1.5f),(boardHouseId + 1).ToString("00"),fs,home.OwnerId == 0 ? HudTheme.Hex(0xC9FFD9) : Color.white,TextAnchor.MiddleCenter); }
            if (home.OwnerId >= 0 && match.Players[home.OwnerId].Sleeping) {
                var w = ScreenPoint(BoardHousePoint(new Vector2(.7f,.5f))); float t = Time.time;
                for (int i = 0; i < 3; i++) {
                    float f = Mathf.Repeat(t * .5f + i / 3f,1);
                    var c = new Color(1,1,1,1 - f); HudTheme.OutlinedText(new Rect(w.x + f * 40 * ui,w.y - f * 90 * ui - 20 * ui,80 * ui,40 * ui),"z",Mathf.RoundToInt((22 + f * 18) * ui),c,TextAnchor.MiddleCenter);
                }
            }
            if (!boardBossVisible && match.Phase == MatchPhase.Combat && match.Boss.Phase != BossPhase.Dead) {
                var dir = (Vector2)(Project(match.Boss.Position) - Project(home.Center)); if (dir.sqrMagnitude < .01f) dir = Vector2.up; dir.Normalize();
                var center = ScreenPoint(BoardCenter); var frameEdge = ScreenPoint(BoardCenter + new Vector3(YardLayout.Width,0,0));
                float radius = Mathf.Abs(frameEdge.x - center.x) * .9f, radiusY = radius * .5f;
                var p = new Vector2(center.x + dir.x * radius,center.y - dir.y * radiusY);
                float s = 64 * ui; bool targeted = match.Boss.TargetHouseId == boardHouseId;
                HudTheme.Fill(new Rect(p.x - s / 2,p.y - s / 2,s,s),targeted ? HudTheme.Hex(0xD9434F,.95f) : HudTheme.Hex(0x1B2238,.92f),s / 2);
                HudIcons.Draw(new Rect(p.x - s * .35f,p.y - s * .35f,s * .7f,s * .7f),"icon_skull");
                HudTheme.OutlinedText(new Rect(p.x - 90 * ui,p.y + s / 2,180 * ui,30 * ui),targeted ? "COMING!" : "BOSS",Mathf.RoundToInt(20 * ui),targeted ? HudTheme.Bad : HudTheme.Ink,TextAnchor.MiddleCenter);
            }
        }
        // Development check: in board view no neighbourhood house may be inside the camera view.
        private void AuditBoardView()
        {
            if (!HudAudit.Enabled || boardHouseId < 0) return;
            float h = Camera.orthographicSize * 2, w = h * Camera.aspect; var c = Camera.transform.position;
            var view = new Rect(c.x - w / 2,c.y - h / 2,w,h);
            foreach (var r in houseRects) if (r.Overlaps(view)) { Debug.LogWarning("[HUD audit] A neighbourhood house is visible in board view"); return; }
        }
    }
}
