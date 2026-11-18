using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

internal sealed class MicroWindowPath
{
    public string Name;
    public Vector2[] Points;
    public float[] Stations;
    public float Deck;
    public float Length => Stations[Stations.Length - 1];
    public readonly List<MicroWindowDoor> Doors = new List<MicroWindowDoor>();
    public readonly List<float> Posts = new List<float>();
    public Vector2 Point(float station)
    {
        station = Mathf.Clamp(station, 0, Length);
        int i = 1; while (i < Stations.Length - 1 && Stations[i] < station) i++;
        return Vector2.Lerp(Points[i - 1], Points[i], Mathf.InverseLerp(Stations[i - 1], Stations[i], station));
    }
    public Vector2 Tangent(float station) => (Point(Mathf.Min(Length, station + .015f)) - Point(Mathf.Max(0, station - .015f))).normalized;
    public float Closest(Vector2 position)
    {
        float best = float.MaxValue, station = 0;
        for (int i = 1; i < Points.Length; i++)
        {
            var d = Points[i] - Points[i - 1];
            float t = Mathf.Clamp01(Vector2.Dot(position - Points[i - 1], d) / d.sqrMagnitude);
            float error = (position - (Points[i - 1] + d * t)).sqrMagnitude;
            if (error < best) { best = error; station = Mathf.Lerp(Stations[i - 1], Stations[i], t); }
        }
        return station;
    }
}

internal sealed class MicroWindowDoor
{
    public string Name;
    public float Start, End;
    public float Center => (Start + End) * .5f;
    public bool Inside(float station) => station > Start + .001f && station < End - .001f;
}

internal static class MicroLairWindowGeometry
{
    public const float Inset = .05f, BayWidth = 2f, PostWidth = .04f, JambWidth = .055f;
    public const float DoorClearWidth = 1.85f, DoorClearHeight = 3.6f, RailHeight = .028f;
    public const int PaneRows = 6;
    public const string MeshFolder = "Assets/Environment/MicroLair/Meshes/SteelWindows/LargePanesSixRows";
    public static int MemberCount;

    // Read and weld the real top-face boundary, so frames follow the edited platform meshes.
    public static MicroWindowPath Outline(MeshFilter filter, string name, bool sideBay, float scale)
    {
        var mesh = filter.sharedMesh;
        var vertices = mesh.vertices; var triangles = mesh.GetTriangles(0);
        var points = new List<Vector2>(); var ids = new Dictionary<string, int>();
        int Weld(int index)
        {
            var v = filter.transform.TransformPoint(vertices[index]) / scale;
            string key = Mathf.RoundToInt(v.x * 10000) + ":" + Mathf.RoundToInt(v.z * 10000);
            if (!ids.TryGetValue(key, out int id)) { id = points.Count; ids.Add(key, id); points.Add(new Vector2(v.x, v.z)); }
            return id;
        }
        var edges = new Dictionary<(int, int), int>();
        void Edge(int a, int b) { var key = a < b ? (a, b) : (b, a); edges[key] = edges.TryGetValue(key, out var n) ? n + 1 : 1; }
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = Weld(triangles[i]), b = Weld(triangles[i + 1]), c = Weld(triangles[i + 2]); Edge(a, b); Edge(b, c); Edge(c, a);
        }
        var adjacent = new Dictionary<int, List<int>>();
        void Link(Dictionary<int, List<int>> graph, int a, int b)
        {
            if (!graph.ContainsKey(a)) graph[a] = new List<int>();
            if (!graph.ContainsKey(b)) graph[b] = new List<int>();
            graph[a].Add(b); graph[b].Add(a);
        }
        foreach (var edge in edges.Where(e => e.Value == 1)) Link(adjacent, edge.Key.Item1, edge.Key.Item2);
        Require(adjacent.Values.All(v => v.Count == 2), "Closed platform boundary: " + name);
        var boundary = new List<Vector2>(); int first = adjacent.Keys.First(), current = first, previous = -1;
        do
        {
            boundary.Add(points[current]); int next = adjacent[current].First(n => n != previous);
            previous = current; current = next;
            Require(boundary.Count <= adjacent.Count, "Ordered platform boundary: " + name);
        } while (current != first);
        float area = 0;
        for (int i = 0; i < boundary.Count; i++) { var a = boundary[i]; var b = boundary[(i + 1) % boundary.Count]; area += a.x * b.y - b.x * a.y; }
        if (area < 0) boundary.Reverse();
        var inset = new Vector2[boundary.Count];
        for (int i = 0; i < boundary.Count; i++)
        {
            var incoming = (boundary[i] - boundary[(i + boundary.Count - 1) % boundary.Count]).normalized;
            var outgoing = (boundary[(i + 1) % boundary.Count] - boundary[i]).normalized;
            var n1 = new Vector2(-incoming.y, incoming.x); var n2 = new Vector2(-outgoing.y, outgoing.x);
            inset[i] = boundary[i] + (n1 + n2) * (Inset / Mathf.Max(.1f, 1 + Vector2.Dot(n1, n2)));
        }
        float rear = boundary.Max(p => p.y);
        float outer = boundary.Average(p => p.x) < 0 ? boundary.Min(p => p.x) : boundary.Max(p => p.x);
        var open = new Dictionary<int, List<int>>();
        for (int i = 0; i < boundary.Count; i++)
        {
            int j = (i + 1) % boundary.Count;
            bool back = Mathf.Abs(boundary[i].y - rear) < .002f && Mathf.Abs(boundary[j].y - rear) < .002f;
            bool wall = sideBay && Mathf.Abs(boundary[i].x - outer) < .002f && Mathf.Abs(boundary[j].x - outer) < .002f;
            if (!back && !wall) Link(open, i, j);
        }
        var ends = open.Keys.Where(i => open[i].Count == 1).ToArray();
        Require(ends.Length == 2, "Two ends for window run: " + name);
        current = sideBay ? ends.OrderBy(i => boundary[i].y).First() : ends.OrderBy(i => boundary[i].x).First();
        previous = -1; var path = new List<Vector2>();
        while (true)
        {
            path.Add(inset[current]);
            var next = open[current].Where(i => i != previous).ToArray(); if (next.Length == 0) break;
            Require(next.Length == 1 && path.Count <= open.Count, "Continuous open frame path.");
            previous = current; current = next[0];
        }
        var stations = new float[path.Count];
        for (int i = 1; i < path.Count; i++) stations[i] = stations[i - 1] + Vector2.Distance(path[i - 1], path[i]);
        return new MicroWindowPath { Name = name, Points = path.ToArray(), Stations = stations, Deck = filter.GetComponent<MeshRenderer>().bounds.max.y / scale };
    }

    public static void AddDoor(MicroWindowPath path, string name, Vector2 near)
    {
        float middle = path.Closest(near), half = (DoorClearWidth + JambWidth) * .5f;
        Require(middle - half > .2f && middle + half < path.Length - .2f, "Door fits on window run: " + name);
        var door = new MicroWindowDoor { Name = name, Start = middle - half, End = middle + half };
        Require(Vector2.Dot(path.Tangent(door.Start), path.Tangent(door.End)) > .999f, "Door occupies a straight section: " + name);
        path.Doors.Add(door);
    }

    public static List<float> RowLevels(float height)
    {
        var rows = new List<float>(); const float sill = .025f, header = DoorClearHeight + .025f;
        Require(height > header + .1f, "Window fascia leaves space above the taller door header.");
        const int lower = 4, upper = PaneRows - lower;
        for (int i = 0; i <= lower; i++) rows.Add(Mathf.Lerp(sill, header, i / (float)lower));
        for (int i = 1; i <= upper; i++) rows.Add(Mathf.Lerp(header, height - .025f, i / (float)upper));
        return rows;
    }

    public static void Build(MicroWindowPath path, Transform parent, Material material, float top, float scale)
    {
        var run = new GameObject(path.Name).transform; run.SetParent(parent, false);
        var posts = new GameObject("VerticalMullions_And_DoorJambs").transform; posts.SetParent(run, false);
        var rails = new GameObject("HorizontalRails_OpenDoorways").transform; rails.SetParent(run, false);
        var cuts = new List<float> { 0, path.Length };
        foreach (var d in path.Doors) { cuts.Add(d.Start); cuts.Add(d.End); }
        cuts.Sort(); path.Posts.Clear();
        for (int i = 1; i < cuts.Count; i++)
        {
            int count = Mathf.CeilToInt((cuts[i] - cuts[i - 1]) / BayWidth);
            for (int j = 0; j < count; j++) path.Posts.Add(Mathf.Lerp(cuts[i - 1], cuts[i], j / (float)count));
        }
        path.Posts.Add(path.Length);
        for (int i = 0; i < path.Posts.Count; i++)
        {
            float station = path.Posts[i]; var point = path.Point(station); var t = path.Tangent(station);
            bool jamb = path.Doors.Any(d => Mathf.Abs(station - d.Start) < .001f || Mathf.Abs(station - d.End) < .001f);
            bool aboveDoor = path.Doors.Any(d => d.Inside(station));
            float bottom = path.Deck + (aboveDoor ? DoorClearHeight + .05f : 0);
            float width = jamb || i % 4 == 0 ? JambWidth : PostWidth;
            var tangent = new Vector3(t.x, 0, t.y);
            var mesh = new SteelMesh();
            mesh.Box(new Vector3(point.x, (bottom + top) * .5f, point.y), tangent * (width * .5f), Vector3.up * ((top - bottom) * .5f),
                Vector3.Cross(tangent, Vector3.up) * (jamb ? .0325f : .025f), new Vector2(i * .371f, i * .137f));
            Emit(mesh, posts, (jamb ? "DoorJamb_" : aboveDoor ? "TransomStile_" : "Mullion_") + i.ToString("000"), path.Name, material, scale);
        }
        var levels = RowLevels(top - path.Deck);
        for (int row = 0; row < levels.Count; row++)
        {
            float level = levels[row]; bool header = Mathf.Abs(level - DoorClearHeight - .025f) < .001f;
            float thickness = row == 0 || row == levels.Count - 1 || header ? .05f : RailHeight;
            var intervals = new List<(float a, float b)>(); float cursor = 0;
            foreach (var door in path.Doors.OrderBy(d => d.Start))
            {
                if (level - thickness * .5f >= DoorClearHeight - .001f) continue;
                intervals.Add((cursor, door.Start)); cursor = door.End;
            }
            intervals.Add((cursor, path.Length));
            var mesh = new SteelMesh();
            foreach (var interval in intervals)
            {
                var stops = path.Stations.Where(s => s > interval.a + .001f && s < interval.b - .001f).ToList();
                stops.Insert(0, interval.a); stops.Add(interval.b);
                for (int i = 1; i < stops.Count; i++)
                {
                    var a = path.Point(stops[i - 1]); var b = path.Point(stops[i]); var t = (b - a).normalized;
                    var axis = new Vector3(t.x, 0, t.y);
                    mesh.Box(new Vector3((a.x + b.x) * .5f, path.Deck + level, (a.y + b.y) * .5f),
                        axis * ((Vector2.Distance(a, b) + .003f) * .5f), Vector3.up * (thickness * .5f), Vector3.Cross(axis, Vector3.up) * .02f,
                        new Vector2(stops[i - 1] + .29f, level + .17f));
                }
            }
            Emit(mesh, rails, (header ? "DoorHeaderBand_" : row == 0 ? "Sill_" : row == levels.Count - 1 ? "Head_" : "TransomRail_") + row.ToString("00"), path.Name, material, scale);
        }
        foreach (var door in path.Doors)
        {
            string dimensions = DoorClearWidth.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x" + DoorClearHeight.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            var marker = new GameObject(door.Name + "_CLEAR_" + dimensions.Replace('.', 'p') + "m").transform; marker.SetParent(run, false);
            var p = path.Point(door.Center); marker.position = new Vector3(p.x, path.Deck, p.y) * scale;
        }
    }

    sealed class SteelMesh
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        public void Box(Vector3 center, Vector3 u, Vector3 v, Vector3 w, Vector2 offset)
        {
            Face(center + w, u, v, offset); Face(center - w, -u, v, offset);
            Face(center + u, -w, v, offset); Face(center - u, w, v, offset);
            Face(center + v, u, -w, offset); Face(center - v, u, w, offset); MemberCount++;
        }
        void Face(Vector3 c, Vector3 u, Vector3 v, Vector2 offset)
        {
            int n = Vertices.Count; Vertices.AddRange(new[] { c - u - v, c + u - v, c + u + v, c - u + v });
            var normal = Vector3.Cross(u, v).normalized; normals.AddRange(Enumerable.Repeat(normal, 4));
            float x = u.magnitude * 2, y = v.magnitude * 2;
            uv.AddRange(new[] { offset, offset + new Vector2(x, 0), offset + new Vector2(x, y), offset + new Vector2(0, y) });
            triangles.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3 });
        }
        public Mesh Create(Vector3 pivot, float scale)
        {
            var mesh = new Mesh { indexFormat = Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(Vertices.Select(p => (p - pivot) * scale).ToList()); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents(); Unwrapping.GenerateSecondaryUVSet(mesh); return mesh;
        }
    }
    static void Emit(SteelMesh builder, Transform parent, string name, string run, Material material, float scale)
    {
        if (builder.Vertices.Count == 0) return;
        var bounds = new Bounds(builder.Vertices[0], Vector3.zero); foreach (var vertex in builder.Vertices) bounds.Encapsulate(vertex);
        var mesh = builder.Create(bounds.center, scale); mesh.name = run + "_" + name;
        string asset = MeshFolder + "/" + run + "/" + name + ".asset";
        Directory.CreateDirectory(Path.GetDirectoryName(asset));
        var previous = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
        if (previous) { EditorUtility.CopySerialized(mesh, previous); Object.DestroyImmediate(mesh); mesh = previous; }
        else AssetDatabase.CreateAsset(mesh, asset);
        var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.position = bounds.center * scale;
        obj.AddComponent<MeshFilter>().sharedMesh = mesh; obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        var collider = obj.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.convex = false; collider.contactOffset = .001f;
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
