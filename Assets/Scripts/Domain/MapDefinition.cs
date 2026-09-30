using System;
using System.Collections.Generic;

namespace ContainerDefense.Domain
{
    [Serializable] public sealed class HouseSpawnPoint
    {
        public int Id; public float X, Z; public int ColorIndex;
        public Point2 Center { get { return new Point2(X,Z); } }
        public Point2 Entry { get { return new Point2(X,Z - 2.5f); } }
    }
    [Serializable] public sealed class RouteNode
    { public int Id; public float X,Z; public Point2 Position { get { return new Point2(X,Z); } } }
    public enum BossRouteKind { Straight, Sweep, ZigZag, Regional, Outer }
    [Serializable] public sealed class BossRouteDefinition
    {
        public string RouteId; public BossRouteKind Kind; public int[] NodeSequence;
        public float TelegraphTime = 4; public int MinimumWave = 1, MaximumWave = 999; public int Weight = 1;
    }
    [Serializable] public sealed class MapDefinition
    {
        public const float MinimumTelegraph = 4, MaxHouseNodeDistance = 7;
        public string MapId = "container_neighborhood_v1";
        public HouseSpawnPoint[] HouseSpawns;
        public RouteNode[] Nodes;
        public int[] EntryNodes;
        public BossRouteDefinition[] Routes;
        public float MinX = -18, MaxX = 18, MinZ = -18, MaxZ = 18;
        [NonSerialized] private int[][] adjacency;
        public MapDefinition Snapshot()
        {
            Validate();
            var copy = (MapDefinition)MemberwiseClone(); copy.adjacency = null; copy.HouseSpawns = Array.ConvertAll(HouseSpawns,h => new HouseSpawnPoint { Id = h.Id,X = h.X,Z = h.Z,ColorIndex = h.ColorIndex });
            copy.Nodes = Array.ConvertAll(Nodes,n => new RouteNode { Id = n.Id,X = n.X,Z = n.Z }); copy.EntryNodes = (int[])EntryNodes.Clone();
            copy.Routes = Array.ConvertAll(Routes,r => new BossRouteDefinition { RouteId = r.RouteId,Kind = r.Kind,NodeSequence = (int[])r.NodeSequence.Clone(),TelegraphTime = r.TelegraphTime,MinimumWave = r.MinimumWave,MaximumWave = r.MaximumWave,Weight = r.Weight });
            copy.Validate(); return copy;
        }
        public void Validate()
        {
            if (HouseSpawns == null || HouseSpawns.Length != 12 || Nodes == null || Nodes.Length != 20 || EntryNodes == null || EntryNodes.Length == 0 || Routes == null || Routes.Length == 0)
                throw new ArgumentException("The fixed neighborhood requires 12 houses and a 20-node road graph.");
            for (int i = 0; i < 12; i++) if (HouseSpawns[i] == null || HouseSpawns[i].Id != i || !MatchRules.Finite(HouseSpawns[i].X) || !MatchRules.Finite(HouseSpawns[i].Z)) throw new ArgumentException("Invalid fixed house identity.");
            if (!MatchRules.Finite(MinX) || !MatchRules.Finite(MaxX) || !MatchRules.Finite(MinZ) || !MatchRules.Finite(MaxZ) || MinX >= MaxX || MinZ >= MaxZ) throw new ArgumentException("Invalid map bounds.");
            for (int i = 0; i < Nodes.Length; i++) if (Nodes[i] == null || Nodes[i].Id != i || !MatchRules.Finite(Nodes[i].X) || !MatchRules.Finite(Nodes[i].Z)) throw new ArgumentException("Invalid road node.");
            foreach (int entry in EntryNodes) if (entry < 0 || entry >= Nodes.Length) throw new ArgumentException("Invalid boss entry.");
            foreach (var route in Routes) {
                if (route == null || string.IsNullOrEmpty(route.RouteId) || route.NodeSequence == null || route.NodeSequence.Length == 0 || !MatchRules.Finite(route.TelegraphTime) || route.TelegraphTime < MinimumTelegraph || route.Weight <= 0 || route.MinimumWave < 1 || route.MaximumWave < route.MinimumWave) throw new ArgumentException("Invalid boss route.");
                foreach (int id in route.NodeSequence) if (id < 0 || id >= Nodes.Length) throw new ArgumentException("Unknown route node.");
            }
            ValidateGraph();
        }
        // Road edges are orthogonal: two nodes share a row or column with no node strictly between them.
        public int[] Neighbors(int node)
        {
            if (adjacency == null) BuildAdjacency();
            return (int[])adjacency[node].Clone();
        }
        private void BuildAdjacency()
        {
            var result = new int[Nodes.Length][];
            for (int a = 0; a < Nodes.Length; a++) {
                var list = new List<int>();
                for (int b = 0; b < Nodes.Length; b++) if (a != b && Edge(a,b)) list.Add(b);
                result[a] = list.ToArray();
            }
            adjacency = result;
        }
        private bool Edge(int a,int b)
        {
            var p = Nodes[a].Position; var q = Nodes[b].Position;
            bool row = Math.Abs(p.Z - q.Z) < .001f, column = Math.Abs(p.X - q.X) < .001f;
            if (row == column) return false;
            foreach (var n in Nodes) {
                if (n.Id == a || n.Id == b) continue;
                if (row && Math.Abs(n.Z - p.Z) < .001f && n.X > Math.Min(p.X,q.X) && n.X < Math.Max(p.X,q.X)) return false;
                if (column && Math.Abs(n.X - p.X) < .001f && n.Z > Math.Min(p.Z,q.Z) && n.Z < Math.Max(p.Z,q.Z)) return false;
            }
            return true;
        }
        private void ValidateGraph()
        {
            BuildAdjacency();
            var seen = new bool[Nodes.Length]; var queue = new Queue<int>(); queue.Enqueue(0); seen[0] = true; int reached = 1;
            while (queue.Count > 0) foreach (int next in adjacency[queue.Dequeue()]) if (!seen[next]) { seen[next] = true; reached++; queue.Enqueue(next); }
            if (reached != Nodes.Length) throw new ArgumentException("The road graph must be connected.");
            foreach (var route in Routes)
                for (int i = 1; i < route.NodeSequence.Length; i++)
                    if (Array.IndexOf(adjacency[route.NodeSequence[i - 1]],route.NodeSequence[i]) < 0) throw new ArgumentException("Boss route " + route.RouteId + " leaves the road graph.");
            foreach (var house in HouseSpawns)
                if (house.Entry.Distance(Nodes[ClosestNode(house.Entry)].Position) > MaxHouseNodeDistance) throw new ArgumentException("House " + house.Id + " is not adjacent to a road node.");
        }
        // Everyone starts on the plaza between houses 05 and 08.
        public Point2 Spawn(int player) { return new Point2((player - 2.5f) * .7f,0); }
        public Point2 ClampMove(Point2 from,Point2 next)
        {
            next = new Point2(Math.Max(MinX,Math.Min(MaxX,next.X)),Math.Max(MinZ,Math.Min(MaxZ,next.Z)));
            if (Walkable(next)) return next;
            var x = new Point2(next.X,from.Z); if (Walkable(x)) return x;
            var z = new Point2(from.X,next.Z); return Walkable(z) ? z : from;
        }
        public bool Walkable(Point2 point)
        {
            foreach (var h in HouseSpawns) if (Math.Abs(point.X - h.X) < 3.45f && point.Z > h.Z - 2.1f && point.Z < h.Z + 2.4f) return false;
            return true;
        }
        // Equal distances resolve to the lower node id.
        public int ClosestNode(Point2 position)
        {
            int best = -1; float distance = float.MaxValue;
            foreach (var n in Nodes) { float d = position.Distance(n.Position); if (best < 0 || d < distance - .0001f) { best = n.Id; distance = d; } }
            return best;
        }
        // Deterministic shortest road path; equal-cost predecessors resolve to the lower node id.
        public Point2[] RoadPath(Point2 from,Point2 to)
        {
            if (adjacency == null) BuildAdjacency();
            int start = ClosestNode(from), end = ClosestNode(to);
            var cost = new float[Nodes.Length]; var previous = new int[Nodes.Length]; var done = new bool[Nodes.Length];
            for (int i = 0; i < cost.Length; i++) { cost[i] = float.MaxValue; previous[i] = -1; }
            cost[start] = 0;
            while (true) {
                int current = -1;
                for (int i = 0; i < cost.Length; i++) if (!done[i] && cost[i] < float.MaxValue && (current < 0 || cost[i] < cost[current] - .0001f)) current = i;
                if (current < 0 || current == end) break;
                done[current] = true;
                foreach (int next in adjacency[current]) {
                    if (done[next]) continue;
                    float c = cost[current] + Nodes[current].Position.Distance(Nodes[next].Position);
                    if (c < cost[next] - .0001f || (Math.Abs(c - cost[next]) <= .0001f && current < previous[next])) { cost[next] = c; previous[next] = current; }
                }
            }
            var nodes = new List<int>(); for (int n = end; n >= 0; n = n == start ? -1 : previous[n]) nodes.Add(n);
            nodes.Reverse(); var result = new List<Point2>();
            foreach (int n in nodes) result.Add(Nodes[n].Position);
            result.Add(to); return result.ToArray();
        }
        // Astra's layout from vhi's top-down mocks: three houses per row, four rows, the rabbit plaza at the centre.
        public static MapDefinition Default()
        {
            var map = new MapDefinition { HouseSpawns = new HouseSpawnPoint[12],Nodes = new RouteNode[20],EntryNodes = new[] { 0,1,2,3,7,11,15,19,18,17,16,12,8,4 } };
            // Container colours follow the top-down mocks: 01 blue, 02 pink, 03 yellow, 04 purple, 05 teal, 06 pink, 07 red...
            int[] colors = { 0,1,2,3,4,1,5,0,1,3,4,2 };
            for (int i = 0; i < 12; i++) map.HouseSpawns[i] = new HouseSpawnPoint { Id = i,X = (i % 3 - 1) * 11,Z = 12 - i / 3 * 8,ColorIndex = colors[i] };
            // House 08 sits just southwest of the plaza so the common spawn area remains open.
            map.HouseSpawns[7].X = -1.5f; map.HouseSpawns[7].Z = -4.5f;
            for (int i = 0; i < 20; i++) map.Nodes[i] = new RouteNode { Id = i,X = -16.5f + i % 4 * 11,Z = 16 - i / 4 * 8 };
            map.Routes = new[] {
                new BossRouteDefinition { RouteId = "east_straight",Kind = BossRouteKind.Straight,NodeSequence = new[] { 3,7,11,15,19 } },
                new BossRouteDefinition { RouteId = "west_sweep",Kind = BossRouteKind.Sweep,NodeSequence = new[] { 0,4,8,12,16,17,18,19 } },
                new BossRouteDefinition { RouteId = "zig_zag",Kind = BossRouteKind.ZigZag,NodeSequence = new[] { 0,1,2,3,7,6,5,4,8,9,10,11,15,14,13,12 } },
                new BossRouteDefinition { RouteId = "north_cluster",Kind = BossRouteKind.Regional,NodeSequence = new[] { 0,1,2,3,7,6,5,4 } },
                new BossRouteDefinition { RouteId = "outer_loop",Kind = BossRouteKind.Outer,NodeSequence = new[] { 0,1,2,3,7,11,15,19,18,17,16,12,8,4,0 } } };
            map.Validate(); return map;
        }
    }
}
