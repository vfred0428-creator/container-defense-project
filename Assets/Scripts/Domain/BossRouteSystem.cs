using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    public sealed partial class MatchSimulation
    {
        private int lastTarget = -1, routeHouseIndex, routePointIndex, visitAttacks;
        private float routeTelegraph = 4;
        public const float TargetProtectionSeconds = 18;
        private void PlanBossRoute()
        {
            var candidates = new List<int>();
            foreach (var h in houses) if (h.OwnerId >= 0 && !h.Destroyed && Elapsed >= h.ProtectedUntil) candidates.Add(h.Id);
            if (candidates.Count == 0) { Boss.Phase = BossPhase.Recovery; Boss.RecoveryRemaining = 1; Boss.TargetHouseId = -1; return; }
            if (candidates.Count > 1) candidates.Remove(lastTarget);
            var eligible = new List<BossRouteDefinition>(); double totalWeight = 0;
            foreach (var option in map.Routes) if (Boss.Wave >= option.MinimumWave && Boss.Wave <= option.MaximumWave) { eligible.Add(option); totalWeight += option.Weight; }
            // A malformed wave schedule must not strand a live match without a boss.
            if (eligible.Count == 0) { eligible.Add(map.Routes[0]); totalWeight = map.Routes[0].Weight; }
            double roll = random.NextDouble() * totalWeight; var route = eligible[eligible.Count - 1];
            foreach (var option in eligible) { roll -= option.Weight; if (roll < 0) { route = option; break; } }
            Boss.RouteId = route.RouteId; routeTelegraph = route.TelegraphTime;
            var ordered = new List<int>();
            foreach (int node in route.NodeSequence)
            {
                if (candidates.Count == 0 || ordered.Count == 3) break;
                int best = 0; float distance = float.MaxValue;
                for (int i = 0; i < candidates.Count; i++) {
                    float d = houses[candidates[i]].Entry.Distance(map.Nodes[node].Position);
                    if (d < distance) { best = i; distance = d; }
                }
                ordered.Add(candidates[best]); candidates.RemoveAt(best);
            }
            Boss.RouteHouses = ordered.ToArray(); routeHouseIndex = 0;
            StartRouteTarget(route.TelegraphTime);
        }
        private void StartRouteTarget(float telegraph)
        {
            while (routeHouseIndex < Boss.RouteHouses.Length) {
                var next = houses[Boss.RouteHouses[routeHouseIndex]];
                if (!next.Destroyed && next.OwnerId >= 0 && Elapsed >= next.ProtectedUntil) break;
                routeHouseIndex++;
            }
            if (routeHouseIndex >= Boss.RouteHouses.Length) { Boss.TargetHouseId = -1; Boss.Phase = BossPhase.Selecting; return; }
            Boss.TargetHouseId = Boss.RouteHouses[routeHouseIndex]; var h = houses[Boss.TargetHouseId];
            Boss.RoutePath = map.RoadPath(Boss.Position,new Point2(h.Entry.X,h.Entry.Z - 1.5f)); routePointIndex = 0; visitAttacks = 0;
            Boss.TelegraphRemaining = telegraph; Boss.Phase = BossPhase.Telegraphing;
            Emit(MatchEventKind.RoutePlanned,h.OwnerId,h.Id);
        }
        private void EndBossVisit(HouseState house)
        {
            house.ProtectedUntil = Elapsed + TargetProtectionSeconds; lastTarget = house.Id;
            Boss.TargetHouseId = -1; Boss.Phase = BossPhase.Recovery; Boss.RecoveryRemaining = 3;
            routeHouseIndex++;
        }
        private void TickBossRoute(float dt)
        {
            Boss.Wave = 1 + (int)(CombatSeconds / 60);
            Boss.SlowRemaining = Math.Max(0,Boss.SlowRemaining - dt);
            if (Boss.Phase == BossPhase.Selecting) { PlanBossRoute(); return; }
            if (Boss.Phase == BossPhase.Recovery) {
                Boss.RecoveryRemaining -= dt; if (Boss.RecoveryRemaining > 0) return;
                if (routeHouseIndex < Boss.RouteHouses.Length) StartRouteTarget(routeTelegraph); else PlanBossRoute(); return;
            }
            if (Boss.Phase == BossPhase.Telegraphing) {
                Boss.TelegraphRemaining = Math.Max(0,Boss.TelegraphRemaining - dt);
                if (Boss.TelegraphRemaining <= 0) Boss.Phase = BossPhase.Travelling; return;
            }
            if (Boss.TargetHouseId < 0) { Boss.Phase = BossPhase.Selecting; return; }
            var target = houses[Boss.TargetHouseId];
            if (target.Destroyed || target.OwnerId < 0) { EndBossVisit(target); return; }
            if (Boss.Phase == BossPhase.Travelling) {
                float budget = rules.BossMoveSpeed * (Boss.SlowRemaining > 0 ? .65f : 1) * dt;
                while (budget > 0 && routePointIndex < Boss.RoutePath.Length) {
                    var node = Boss.RoutePath[routePointIndex]; float distance = Boss.Position.Distance(node);
                    Boss.Position = Boss.Position.Towards(node,budget); budget -= distance;
                    if (Boss.Position.Distance(node) < .01f) routePointIndex++; else break;
                }
                if (routePointIndex >= Boss.RoutePath.Length) { Boss.Phase = BossPhase.Attacking; Boss.AttackTimer = rules.BossAttackInterval; }
                return;
            }
            if (Boss.Phase != BossPhase.Attacking) return;
            Boss.AttackTimer -= dt; if (Boss.AttackTimer > 0) return;
            Boss.AttackTimer += rules.BossAttackInterval; visitAttacks++; target.AttacksReceived++;
            float damage = rules.BossDamage * (1 + CombatSeconds * rules.BossEnragePerSecond);
            target.Health = Math.Max(0,target.Health - damage); Emit(MatchEventKind.DoorHit,target.OwnerId,target.Id,damage);
            if (target.Destroyed) Eliminate(players[target.OwnerId]);
            if (target.Destroyed || visitAttacks >= 3) EndBossVisit(target);
            if (LivingHouses() == 0) Finish(false);
        }
    }
}
