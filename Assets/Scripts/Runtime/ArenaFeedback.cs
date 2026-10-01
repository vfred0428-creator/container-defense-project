using System.Collections.Generic;
using ContainerDefense.Domain;
using UnityEngine;

namespace ContainerDefense
{
    // Hand-made combat and building feedback in a flat outlined style: no soft glowing particles.
    // Purely visual: reads match events and state, never changes any gameplay value.
    public sealed partial class ArenaView
    {
        private const int ProjectileCount = 32;
        private struct Projectile { public WeaponKind Kind; public Vector3 From, To; public float Start, Duration; public bool Active; }
        private struct Floater { public string Text; public Vector3 At; public float Start; public Color Colour; }
        private struct FlyingCoin { public Vector2 From; public float Start; }
        private readonly Projectile[] projectiles = new Projectile[ProjectileCount];
        private readonly SpriteRenderer[] projectileSprites = new SpriteRenderer[ProjectileCount];
        private readonly SpriteRenderer[,] projectileTrail = new SpriteRenderer[ProjectileCount,3];
        private readonly List<Floater> floaters = new List<Floater>();
        private readonly List<FlyingCoin> flyingCoins = new List<FlyingCoin>();
        private readonly float[] housePopUntil = new float[12];
        private readonly Vector3[] houseBaseScale = new Vector3[12];
        private Sprite ballSprite, rocketSprite, frostSprite, starSprite, targetSprite;
        private SpriteRenderer impactStar, targetMarker;
        private float impactUntil, bossSquashUntil, lastBossHealth = -1;
        private int nextProjectile;

        // Small outlined textures drawn in code: solid fill, one dark outline, no gradients or glow.
        private static Texture2D FlatShape(string kind)
        {
            const int n = 64; var t = new Texture2D(n,n,TextureFormat.RGBA32,false) { filterMode = FilterMode.Bilinear,wrapMode = TextureWrapMode.Clamp,name = "Flat " + kind };
            Color outline = HudTheme.Hex(0x1E1620);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) {
                float u = (x + .5f) / n * 2 - 1, v = (y + .5f) / n * 2 - 1, r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v,u);
                Color c = Color.clear;
                switch (kind) {
                    case "ball": if (r < .9f) c = outline; if (r < .72f) c = HudTheme.Hex(0x3A3D4A); if (r < .72f && u < -.15f && v > .15f && r > .3f) c = HudTheme.Hex(0x5A5E70); break;
                    case "rocket": if (Mathf.Abs(v) < .34f && u > -.85f && u < .8f) c = outline; if (Mathf.Abs(v) < .22f && u > -.75f && u < .6f) c = HudTheme.Hex(0xE8743A); if (Mathf.Abs(v) < .22f && u >= .45f && u < .6f) c = HudTheme.Hex(0xD9434F); break;
                    case "frost": { float bump = .7f + .12f * Mathf.Cos(a * 6); if (r < bump + .12f) c = outline; if (r < bump) c = HudTheme.Hex(0xBFE6FF); if (r < bump * .45f) c = HudTheme.Hex(0xEAF7FF); break; }
                    case "star": { float spike = .45f + .45f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)),3); if (r < spike + .1f) c = outline; if (r < spike) c = HudTheme.Hex(0xFFD04A); if (r < spike * .5f) c = HudTheme.Hex(0xFFF1C2); break; }
                    case "target": { bool ring = r > .62f && r < .9f, cross = (Mathf.Abs(u) < .1f || Mathf.Abs(v) < .1f) && r < .62f && r > .25f; if (ring || cross) c = HudTheme.Hex(0xE8433F); if ((r > .58f && r < .62f) || (r > .9f && r < .95f)) c = outline; break; }
                }
                t.SetPixel(x,y,c);
            }
            t.Apply(); return t;
        }
        private Sprite FlatSprite(string kind,float unitsPerTexture)
        { var t = FlatShape(kind); var s = Sprite.Create(t,new Rect(0,0,t.width,t.height),new Vector2(.5f,.5f),t.width / unitsPerTexture); owned.Add(s); return s; }
        private void BuildFeedback()
        {
            ballSprite = FlatSprite("ball",.55f); rocketSprite = FlatSprite("rocket",.9f); frostSprite = FlatSprite("frost",1.4f); starSprite = FlatSprite("star",1.6f); targetSprite = FlatSprite("target",2.4f);
            for (int i = 0; i < ProjectileCount; i++) {
                projectileSprites[i] = SpriteObject("Projectile " + i,ballSprite,965); projectileSprites[i].enabled = false;
                for (int k = 0; k < 3; k++) { projectileTrail[i,k] = SpriteObject("Rocket trail " + i,frostSprite,964); projectileTrail[i,k].color = HudTheme.Hex(0x8A8F99,.8f); projectileTrail[i,k].enabled = false; }
            }
            impactStar = SpriteObject("Impact star",starSprite,966); impactStar.enabled = false;
            targetMarker = SpriteObject("Boss target marker",targetSprite,955); targetMarker.enabled = false;
            // Muzzle flashes become small outlined stars instead of soft glows.
            for (int h = 0; h < 12; h++) for (int s = 0; s < 3; s++) { flashes[h,s].sprite = starSprite; flashes[h,s].color = Color.white; }
        }
        // Called from Handle for every match event.
        private void FeedbackEvent(MatchEvent e)
        {
            if (match == null) return;
            if (e.Kind == MatchEventKind.Shot && e.HouseId >= 0) {
                var w = match.Houses[e.HouseId].Weapons[Mathf.Clamp((int)e.Amount,0,2)];
                if (w != null) Launch(w.Kind,Socket(e.HouseId,(int)e.Amount) + new Vector3(0,.5f,0),BossAim() + new Vector3(Random.Range(-.6f,.6f),Random.Range(-.3f,.5f),0));
                float dealt = lastBossHealth < 0 ? 0 : lastBossHealth - match.Boss.Health; lastBossHealth = match.Boss.Health;
                // Only while you look at one base: on the full map numbers would be unreadable specks.
                if (dealt > .5f && session.View.Mode == ViewMode.Base && floaters.Count < 8) floaters.Add(new Floater { Text = Mathf.RoundToInt(dealt).ToString(),At = BossAim() + new Vector3(Random.Range(-2.2f,2.2f),2f + Random.Range(0f,.8f),0),Start = Time.time,Colour = HudTheme.Hex(0xFFE27A) });
            }
            if (e.Kind == MatchEventKind.DoorHit && e.HouseId >= 0)
                floaters.Add(new Floater { Text = "-" + Mathf.RoundToInt(e.Amount),At = HousePoint(e.HouseId,new Vector2(.5f,.35f)),Start = Time.time,Colour = HudTheme.Hex(0xFF6B6B) });
            if ((e.Kind == MatchEventKind.Upgraded || e.Kind == MatchEventKind.Placed || e.Kind == MatchEventKind.Repaired || e.Kind == MatchEventKind.Claimed || e.Kind == MatchEventKind.UpgradeStarted) && e.HouseId >= 0)
                housePopUntil[e.HouseId] = Time.time + .3f;
            if (e.Kind == MatchEventKind.Sold && e.PlayerId == 0 && match.Players[0].HouseId >= 0)
                for (int i = 0; i < 4; i++) FlyCoin(HousePoint(match.Players[0].HouseId,new Vector2(.5f,.2f)) + new Vector3(Random.Range(-.6f,.6f),0,0),i * .07f);
        }
        private void FlyCoin(Vector3 world,float delay) { flyingCoins.Add(new FlyingCoin { From = ScreenPoint(world),Start = Time.time + delay }); }
        private void Launch(WeaponKind kind,Vector3 from,Vector3 to)
        {
            int i = nextProjectile++ % ProjectileCount;
            float duration = kind == WeaponKind.Gatling ? .1f : kind == WeaponKind.Cannon ? .45f : kind == WeaponKind.Rocket ? .55f : .3f;
            projectiles[i] = new Projectile { Kind = kind,From = from,To = to,Start = Time.time,Duration = duration,Active = true };
            projectileSprites[i].sprite = kind == WeaponKind.Cannon ? ballSprite : kind == WeaponKind.Rocket ? rocketSprite : frostSprite;
        }
        // Per-frame update; called from LateUpdate after the regular visuals.
        private void TickFeedback(bool playing)
        {
            if (lastBossHealth < 0 && match != null) lastBossHealth = match.Boss.Health;
            for (int i = 0; i < ProjectileCount; i++) {
                var p = projectiles[i]; var sr = projectileSprites[i];
                bool alive = playing && p.Active && p.Kind != WeaponKind.Gatling && Time.time < p.Start + p.Duration;
                sr.enabled = alive; for (int k = 0; k < 3; k++) projectileTrail[i,k].enabled = alive && p.Kind == WeaponKind.Rocket;
                if (p.Active && Time.time >= p.Start + p.Duration) { projectiles[i].Active = false; Impact(p); }
                if (!alive) continue;
                float f = (Time.time - p.Start) / p.Duration;
                var pos = Vector3.Lerp(p.From,p.To,f);
                if (p.Kind == WeaponKind.Cannon) pos.y += Mathf.Sin(f * Mathf.PI) * 1.6f;   // lobbed arc
                sr.transform.position = pos;
                var dir = p.To - p.From; sr.transform.rotation = p.Kind == WeaponKind.Rocket ? Quaternion.Euler(0,0,Mathf.Atan2(dir.y,dir.x) * Mathf.Rad2Deg) : Quaternion.identity;
                sr.transform.localScale = Vector3.one * (p.Kind == WeaponKind.Slow ? .55f + f * .4f : 1);
                for (int k = 0; k < 3; k++) {
                    float back = Mathf.Max(0,f - (k + 1) * .07f); var tp = Vector3.Lerp(p.From,p.To,back);
                    projectileTrail[i,k].transform.position = tp; projectileTrail[i,k].transform.localScale = Vector3.one * (.32f - k * .07f);
                }
            }
            // Boss: squash on impact, cold tint while slowed.
            bool bossVisible = playing && match.Boss.Phase != BossPhase.Dead;
            if (bossVisible) {
                if (Time.time < bossSquashUntil) { float k = (bossSquashUntil - Time.time) / .14f; boss.transform.localScale = Vector3.Scale(boss.transform.localScale,new Vector3(1 + .08f * k,1 - .08f * k,1)); }
                if (match.Boss.SlowRemaining > 0) boss.color = Color.Lerp(boss.color,HudTheme.Hex(0x9FD8FF),.55f);
            }
            impactStar.enabled = playing && Time.time < impactUntil;
            if (impactStar.enabled) { float k = (impactUntil - Time.time) / .16f; impactStar.transform.localScale = Vector3.one * (.6f + (1 - k) * .5f); impactStar.transform.rotation = Quaternion.Euler(0,0,(1 - k) * 25); }
            // Boss attack warning: a red target on the house it is heading for or hitting.
            int target = match.Boss.TargetHouseId;
            bool aiming = playing && target >= 0 && (match.Boss.Phase == BossPhase.Travelling || match.Boss.Phase == BossPhase.Attacking || match.Boss.Phase == BossPhase.Telegraphing);
            targetMarker.enabled = aiming;
            if (aiming) {
                float pulse = 1 + Mathf.Sin(Time.time * (match.Boss.Phase == BossPhase.Attacking ? 14 : 6)) * .08f;
                targetMarker.transform.position = HousePoint(target,new Vector2(HousePivot.x,.62f)); targetMarker.transform.localScale = Vector3.one * pulse * .7f;
                targetMarker.color = new Color(1,1,1,match.Boss.Phase == BossPhase.Telegraphing ? .45f : .8f);
            }
            // Building pop: a quick squash and stretch on the house that changed.
            for (int h = 0; h < 12; h++) {
                if (homes[h] == null) continue;
                if (houseBaseScale[h] == Vector3.zero) houseBaseScale[h] = homes[h].transform.localScale;
                float k = Time.time < housePopUntil[h] ? (housePopUntil[h] - Time.time) / .3f : 0, s = Mathf.Sin(k * Mathf.PI);
                homes[h].transform.localScale = new Vector3(houseBaseScale[h].x * (1 + s * .05f),houseBaseScale[h].y * (1 - s * .04f),1);
            }
        }
        private void Impact(Projectile p)
        {
            impactStar.transform.position = p.To; impactUntil = Time.time + .16f; bossSquashUntil = Time.time + .14f;
        }
        // GUI-space parts: damage numbers in the display font and coins that fly to the gold counter.
        private void DrawFeedbackGui()
        {
            float ui = HudTheme.Scale;
            for (int i = floaters.Count - 1; i >= 0; i--) {
                var f = floaters[i]; float age = Time.time - f.Start;
                if (age > .9f) { floaters.RemoveAt(i); continue; }
                var p = ScreenPoint(f.At + new Vector3(0,age * 1.4f,0)); int size = Mathf.RoundToInt(26 * ui * (age < .12f ? 1 + (.12f - age) * 3 : 1));
                var c = f.Colour; c.a = 1 - Mathf.Clamp01((age - .55f) / .35f);
                HudTheme.OutlinedText(new Rect(p.x - 80 * ui,p.y - 30 * ui,160 * ui,60 * ui),f.Text,size,c,TextAnchor.MiddleCenter);
            }
            for (int i = flyingCoins.Count - 1; i >= 0; i--) {
                var c = flyingCoins[i]; float age = Time.time - c.Start; if (age < 0) continue;
                if (age > .7f) { flyingCoins.RemoveAt(i); continue; }
                float f = age / .7f, e = f * f; var to = HudLayout.GoldTarget;
                var p = Vector2.Lerp(c.From,to,e) + new Vector2(0,-Mathf.Sin(f * Mathf.PI) * 80 * ui);
                float s = 40 * ui; HudIcons.Draw(new Rect(p.x - s / 2,p.y - s / 2,s,s),"icon_coin");
            }
        }
    }
}
