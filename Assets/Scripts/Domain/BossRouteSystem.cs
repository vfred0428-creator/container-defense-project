using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    public struct RouteCandidate
    {
        public readonly int HouseId; public readonly Point2 Entry; public readonly int Visits;
        public RouteCandidate(int houseId,Point2 entry,int visits) { HouseId = houseId; Entry = entry; Visits = visits; }
    }
    public sealed class BossRoutePlan
    {
        public string RouteId; public BossRouteKind Kind; public float Telegraph; public int[] Houses;
    }
    // Pure and seeded: identical inputs and Random state always produce the identical plan.
    public static class BossRoutePlanner
    {
        public const int MaxTargets = 3;
        public static BossRoutePlan Plan(MapDefinition map,IList<RouteCandidate> available,int lastTarget,int wave,Random random)
        {
            if (map == null || available == null || random == null || available.Count == 0) return null;
            var pool = new List<RouteCandidate>(available);
            // One other target before a repeat whenever another is available.
            if (pool.Count > 1) pool.RemoveAll(c => c.HouseId == lastTarget);
            var eligible = new List<BossRouteDefinition>(); double totalWeight = 0;
            foreach (var option in map.Routes) if (wave >= option.MinimumWave && wave <= option.MaximumWave) { eligible.Add(option); totalWeight += option.Weight; }
            // A malformed wave schedule must not strand a live match without a boss.
            if (eligible.Count == 0) { eligible.Add(map.Routes[0]); totalWeight = map.Routes[0].Weight; }
            double roll = random.NextDouble() * totalWeight; var route = eligible[eligible.Count - 1];
            foreach (var option in eligible) { roll -= option.Weight; if (roll < 0) { route = option; break; } }
            // Sweeps and loops are meaningless for one or two targets.
            if (available.Count <= 2 && route.Kind != BossRouteKind.Straight) route = Straight(eligible) ?? Straight(map.Routes) ?? route;
            // Least-visited first keeps per-house pressure bounded; route proximity and id break ties.
            pool.Sort((a,b) => {
                if (a.Visits != b.Visits) return a.Visits.CompareTo(b.Visits);
                int byRoute = RouteDistance(map,route,a.Entry).CompareTo(RouteDistance(map,route,b.Entry));
                return byRoute != 0 ? byRoute : a.HouseId.CompareTo(b.HouseId);
            });
            if (pool.Count > MaxTargets) pool.RemoveRange(MaxTargets,pool.Count - MaxTargets);
            // Visit the chosen houses in the order the authored route passes them.
            pool.Sort((a,b) => {
                int byIndex = RouteIndex(map,route,a.Entry).CompareTo(RouteIndex(map,route,b.Entry));
                return byIndex != 0 ? byIndex : a.HouseId.CompareTo(b.HouseId);
            });
            return new BossRoutePlan { RouteId = route.RouteId,Kind = route.Kind,Telegraph = Math.Max(MapDefinition.MinimumTelegraph,route.TelegraphTime),Houses = pool.ConvertAll(c => c.HouseId).ToArray() };
        }
        private static BossRouteDefinition Straight(IEnumerable<BossRouteDefinition> routes)
        { foreach (var r in routes) if (r.Kind == BossRouteKind.Straight) return r; return null; }
        private static float RouteDistance(MapDefinition map,BossRouteDefinition route,Point2 entry)
        { float best = float.MaxValue; foreach (int node in route.NodeSequence) best = Math.Min(best,entry.Distance(map.Nodes[node].Position)); return best; }
        private static int RouteIndex(MapDefinition map,BossRouteDefinition route,Point2 entry)
        {
            int best = 0; float distance = float.MaxValue;
            for (int i = 0; i < route.NodeSequence.Length; i++) { float d = entry.Distance(map.Nodes[route.NodeSequence[i]].Position); if (d < distance) { best = i; distance = d; } }
            return best;
        }
    }

    public sealed partial class MatchSimulation
    {
        public const float TargetProtectionSeconds = 18, RecoverySeconds = 3;
        public const int MaxAttacksPerVisit = 3;
        private int lastTarget = -1, routeHouseIndex, routePointIndex, visitAttacks;
        private float routeTelegraph = MapDefinition.MinimumTelegraph;
        private bool Available(HouseState h,double now) { return h.Occupied && now >= h.ProtectedUntil; }
        private void PlanBossRoute(double now)
        {
            var candidates = new List<RouteCandidate>(); float earliest = float.MaxValue;
            foreach (var h in houses) {
                if (!h.Occupied) continue;
                if (Available(h,now)) candidates.Add(new RouteCandidate(h.Id,h.Entry,h.VisitsReceived));
                else earliest = Math.Min(earliest,h.ProtectedUntil);
            }
            Boss.RouteHouses = new int[0]; routeHouseIndex = 0; Boss.TargetHouseId = -1;
            var plan = BossRoutePlanner.Plan(map,candidates,lastTarget,Boss.Wave,random);
            if (plan == null) {
                // Every living house is protected: wait at match time until the first protection lapses.
                Boss.Phase = BossPhase.Waiting; Boss.RoutePath = new Point2[0];
                Boss.RecoveryRemaining = earliest < float.MaxValue ? (float)Math.Max(0,earliest - now) : 1;
                return;
            }
            Boss.RouteId = plan.RouteId; Boss.RouteKind = plan.Kind; routeTelegraph = plan.Telegraph; Boss.RouteHouses = plan.Houses;
            BeginTarget(plan.Houses[0]);
        }
        private void NextRouteTarget(double now)
        {
            while (routeHouseIndex < Boss.RouteHouses.Length && !Available(houses[Boss.RouteHouses[routeHouseIndex]],now)) routeHouseIndex++;
            if (routeHouseIndex < Boss.RouteHouses.Length) BeginTarget(Boss.RouteHouses[routeHouseIndex]); else PlanBossRoute(now);
        }
        private void BeginTarget(int houseId)
        {
            Boss.TargetHouseId = houseId; var h = houses[houseId];
            Boss.RoutePath = map.RoadPath(Boss.Position,new Point2(h.Entry.X,h.Entry.Z - 1.5f)); routePointIndex = 0; visitAttacks = 0;
            Boss.TelegraphRemaining = routeTelegraph; Boss.Phase = BossPhase.Telegraphing;
            Emit(MatchEventKind.RoutePlanned,h.OwnerId,h.Id);
        }
        // A target that emptied, died or became protected before the first hit is skipped:
        // no visit, no protection, and the next target gets a fresh telegraph.
        private void SkipTarget()
        {
            routeHouseIndex++; Boss.TargetHouseId = -1; Boss.Phase = BossPhase.Selecting;
        }
        private void EndBossVisit(HouseState house,double now)
        {
            house.ProtectedUntil = (float)(now + TargetProtectionSeconds); house.VisitsReceived++; lastTarget = house.Id;
            Boss.TargetHouseId = -1; Boss.Phase = BossPhase.Recovery; Boss.RecoveryRemaining = RecoverySeconds;
            routeHouseIndex++;
        }
        // Consumes the whole step, carrying leftover time across phase changes so results do not depend on step size.
        private void TickBossRoute(double dt)
        {
            Boss.SlowRemaining = Math.Max(0,Boss.SlowRemaining - (float)dt);
            double remaining = dt;
            for (int guard = 0; guard < 256 && remaining > 1e-9 && !Finished; guard++) {
                double now = clock - remaining;
                Boss.Wave = 1 + (int)(Math.Max(0,now - rules.PreparationSeconds) / 60);
                switch (Boss.Phase) {
                    case BossPhase.Selecting:
                        if (routeHouseIndex < Boss.RouteHouses.Length) NextRouteTarget(now); else PlanBossRoute(now);
                        break;
                    case BossPhase.Waiting:
                    case BossPhase.Recovery: {
                        double used = Math.Min(remaining,Boss.RecoveryRemaining);
                        Boss.RecoveryRemaining = (float)Math.Max(0,Boss.RecoveryRemaining - used); remaining -= used;
                        if (Boss.RecoveryRemaining <= 1e-6f) Boss.Phase = BossPhase.Selecting;
                        break;
                    }
                    case BossPhase.Telegraphing: {
                        if (!Available(houses[Boss.TargetHouseId],now)) { SkipTarget(); break; }
                        double used = Math.Min(remaining,Boss.TelegraphRemaining);
                        Boss.TelegraphRemaining = (float)Math.Max(0,Boss.TelegraphRemaining - used); remaining -= used;
                        if (Boss.TelegraphRemaining <= 1e-6f) { Boss.TelegraphRemaining = 0; Boss.Phase = BossPhase.Travelling; }
                        break;
                    }
                    case BossPhase.Travelling: {
                        if (!Available(houses[Boss.TargetHouseId],now)) { SkipTarget(); break; }
                        double speed = rules.BossMoveSpeed * (Boss.SlowRemaining > 0 ? .65 : 1), budget = speed * remaining;
                        // Walk node by node; a large step can finish several segments but never cuts a corner.
                        while (budget > 1e-7 && routePointIndex < Boss.RoutePath.Length) {
                            var node = Boss.RoutePath[routePointIndex]; double distance = Boss.Position.Distance(node);
                            if (distance <= budget) { Boss.Position = node; budget -= distance; routePointIndex++; }
                            else { Boss.Position = Boss.Position.Towards(node,(float)budget); budget = 0; }
                        }
                        while (routePointIndex < Boss.RoutePath.Length && Boss.Position.Distance(Boss.RoutePath[routePointIndex]) < 1e-4f) routePointIndex++;
                        remaining = budget / speed;
                        if (routePointIndex >= Boss.RoutePath.Length) {
                            // Re-check on arrival before any damage.
                            if (!Available(houses[Boss.TargetHouseId],clock - remaining)) { SkipTarget(); break; }
                            Boss.Phase = BossPhase.Attacking; Boss.AttackTimer = rules.BossAttackInterval;
                        }
                        break;
                    }
                    case BossPhase.Attacking: {
                        var target = houses[Boss.TargetHouseId];
                        if (!target.Occupied) {
                            if (visitAttacks > 0) EndBossVisit(target,now); else SkipTarget();
                            break;
                        }
                        double used = Math.Min(remaining,Boss.AttackTimer);
                        Boss.AttackTimer = (float)Math.Max(0,Boss.AttackTimer - used); remaining -= used;
                        if (Boss.AttackTimer > 1e-6f) break;
                        HitTarget(target,clock - remaining);
                        break;
                    }
                    default: remaining = 0; break;
                }
            }
        }
        private void HitTarget(HouseState target,double now)
        {
            visitAttacks++; target.AttacksReceived++;
            float damage = rules.BossDamage * (1 + (float)Math.Max(0,now - rules.PreparationSeconds) * rules.BossEnragePerSecond);
            target.Health = Math.Max(0,target.Health - damage); Emit(MatchEventKind.DoorHit,target.OwnerId,target.Id,damage);
            if (target.Destroyed) Eliminate(players[target.OwnerId]);
            if (target.Destroyed || visitAttacks >= MaxAttacksPerVisit) EndBossVisit(target,now);
            else Boss.AttackTimer = rules.BossAttackInterval;
            if (LivingHouses() == 0) Finish(false);
        }
    }
}
