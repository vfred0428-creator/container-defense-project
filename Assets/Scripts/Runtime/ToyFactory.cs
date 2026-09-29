using System.Collections.Generic;
using UnityEngine;

namespace ContainerDefense
{
    public sealed class ToyFactory
    {
        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly Shader shader;
        public ToyFactory() { shader = Resources.Load<Shader>("Toy"); }
        public Material Material(Color color, bool glow = false)
        {
            string key = ColorUtility.ToHtmlStringRGBA(color) + glow;
            Material material;
            if (materials.TryGetValue(key, out material)) return material;
            material = new Material(shader) { color = color, name = "Toy " + key };
            if (glow) material.SetColor("_EmissionColor", color * 1.6f);
            materials.Add(key, material); return material;
        }
        public Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color, bool glow = false)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color, glow);
            Object.Destroy(go.GetComponent<Collider>()); return go.transform;
        }
        public Transform Box(string name, Transform parent, Vector3 p, Vector3 s, Color c, bool glow = false)
        { return Shape(name, PrimitiveType.Cube, parent, p, s, c, glow); }
        public Transform Ball(string name, Transform parent, Vector3 p, Vector3 s, Color c, bool glow = false)
        { return Shape(name, PrimitiveType.Sphere, parent, p, s, c, glow); }
        public Transform Root(string name, Transform parent, Vector3 p)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = p; return go.transform;
        }
        public TextMesh Text(string text, Transform parent, Vector3 p, float size, Color color)
        {
            var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.localPosition = p;
            var mesh = go.AddComponent<TextMesh>(); mesh.text = text; mesh.characterSize = size;
            mesh.fontSize = 64; mesh.anchor = TextAnchor.MiddleCenter; mesh.alignment = TextAlignment.Center; mesh.color = color;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<Renderer>().sharedMaterial = mesh.font.material;
            return mesh;
        }
        public LineRenderer Line(string name, Transform parent, Color color, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.sharedMaterial = Material(color, true); line.positionCount = 2;
            line.startWidth = line.endWidth = width; line.useWorldSpace = true;
            line.numCapVertices = 3; return line;
        }
        public void Dispose() { foreach (var m in materials.Values) Object.Destroy(m); materials.Clear(); }
    }
}
