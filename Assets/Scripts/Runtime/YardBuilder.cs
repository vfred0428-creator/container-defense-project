using UnityEngine;

namespace ContainerDefense
{
    public sealed class YardBuilder
    {
        public static readonly Color[] Colors = {
            new Color(0.27f,0.53f,0.85f), new Color(0.9f,0.39f,0.52f), new Color(0.95f,0.69f,0.24f),
            new Color(0.24f,0.68f,0.61f), new Color(0.58f,0.43f,0.8f), new Color(0.91f,0.47f,0.32f)
        };
        public static readonly Color Navy = new Color(0.13f, 0.16f, 0.24f);
        public static readonly Color Warm = new Color(1, 0.73f, 0.32f);
        private readonly ToyFactory art;
        private readonly Transform root;
        public YardBuilder(ToyFactory factory, Transform parent) { art = factory; root = parent; }

        public void BuildGround()
        {
            art.Box("Yard foundation", root, new Vector3(0, -0.5f, 1), new Vector3(35, 0.8f, 24), Navy);
            Color asphalt = new Color(0.24f, 0.27f, 0.33f);
            art.Box("Asphalt", root, new Vector3(0, -0.07f, 0), new Vector3(34, 0.12f, 21), asphalt);
            for (int x = -15; x <= 15; x += 3)
            {
                art.Box("Paving seam", root, new Vector3(x, 0, 0), new Vector3(0.025f, 0.012f, 19), Navy);
                for (int z = -7; z <= 7; z += 3)
                    art.Box("Paver joint", root, new Vector3(x + 1.5f, 0, z), new Vector3(3, 0.015f, 0.025f), Navy);
            }
            for (int x = -15; x <= 15; x++)
                art.Box("Safety marking", root, new Vector3(x, 0.02f, -8.7f), new Vector3(0.6f, 0.04f, 0.3f), Warm);
            var spawn = art.Shape("Shared starting pad", PrimitiveType.Cylinder, root, new Vector3(0, 0, -7), new Vector3(5, 0.025f, 2.6f), new Color(0.31f,0.35f,0.42f));
            spawn.name = "Shared starting area";
            for (int i = 0; i < 9; i++)
            {
                float x = (i - 4) * 4;
                var stack = art.Root("Background freight", root, new Vector3(x, 1.7f + (i % 2) * 2.8f, 12));
                art.Box("Freight", stack, Vector3.zero, new Vector3(3.8f, 3.2f, 3), Colors[i % 6] * 0.65f);
                for (int rib = -4; rib <= 4; rib++)
                    art.Box("Rib", stack, new Vector3(rib * 0.4f, 0, -1.54f), new Vector3(0.07f, 3, 0.09f), Navy);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 15.9f;
                art.Box("Crane mast", root, new Vector3(x, 6.5f, 11), new Vector3(0.6f, 13, 0.6f), Navy);
                art.Box("Crane arm", root, new Vector3(x - side * 3, 12.6f, 11), new Vector3(7, 0.5f, 0.6f), Navy);
                art.Box("Crane cable", root, new Vector3(x - side * 5, 10, 11), new Vector3(0.04f, 5, 0.04f), Navy);
                for (int z = -5; z <= 7; z += 6) Lamp(new Vector3(x, 0, z));
            }
            StringLights(-16, 16, 8.5f, 5.5f);
            StringLights(-16, 16, -3, 5.5f);
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                Crate(new Vector3(side * (14.7f + (i % 3) * 0.5f), 0.4f, -6 + i * 1.1f));
            }
        }

        public Transform BuildHouse(int id, float x, out Transform door, out Transform barrel, out Transform health, out TextMesh label)
        {
            Color paint = Colors[id], trim = paint * 0.58f;
            var house = art.Root("Container " + (id + 1).ToString("00"), root, new Vector3(x, 0, 5.2f));
            art.Box("Raised floor", house, new Vector3(0, 0.18f, 0), new Vector3(4.3f, 0.35f, 4.2f), trim);
            art.Box("Wood floor", house, new Vector3(0, 0.39f, 0), new Vector3(4.1f, 0.08f, 4), new Color(0.48f,0.32f,0.22f));
            art.Box("Rear wall", house, new Vector3(0, 1.9f, 2), new Vector3(4.3f, 3.3f, 0.18f), paint);
            art.Box("Side wall L", house, new Vector3(-2.1f, 1.9f, 0), new Vector3(0.15f, 3.3f, 4), paint);
            art.Box("Side wall R", house, new Vector3(2.1f, 1.9f, 0), new Vector3(0.15f, 3.3f, 4), paint);
            art.Box("Roof back lip", house, new Vector3(0, 3.6f, 1.7f), new Vector3(4.4f, 0.16f, 0.8f), trim);
            art.Box("Front lintel", house, new Vector3(0, 3, -2), new Vector3(4.3f, 1.05f, 0.2f), paint);
            for (int side = -1; side <= 1; side += 2)
            {
                art.Box("Facade", house, new Vector3(side * 1.52f, 1.4f, -2), new Vector3(1.24f, 2.3f, 0.22f), paint);
                for (int rib = 0; rib < 4; rib++)
                    art.Box("Corrugation", house, new Vector3(side * (1 + rib * 0.32f), 1.85f, -2.16f), new Vector3(0.065f, 3.2f, 0.09f), trim);
                art.Box("Corner post", house, new Vector3(side * 2.06f, 1.9f, -2.2f), new Vector3(0.15f, 3.5f, 0.15f), trim);
            }
            art.Box("Door frame", house, new Vector3(0, 1.42f, -2.08f), new Vector3(1.7f, 2.4f, 0.15f), Warm);
            door = art.Box("Armored door", house, new Vector3(0, 1.35f, -2.2f), new Vector3(1.38f, 2.15f, 0.18f), Navy);
            art.Box("Door inset", door, new Vector3(0, 0.07f, -0.58f), new Vector3(0.76f, 0.69f, 0.15f), trim);
            art.Ball("Door handle", door, new Vector3(0.32f, -0.1f, -0.7f), Vector3.one * 0.12f, Warm, true);
            art.Box("Step", house, new Vector3(0, 0.12f, -2.7f), new Vector3(2.2f, 0.22f, 0.8f), trim);
            art.Box("Window glow", house, new Vector3(-1.47f, 1.55f, -2.24f), new Vector3(0.6f, 0.75f, 0.08f), Warm, true);
            art.Box("Window cross", house, new Vector3(-1.47f, 1.55f, -2.3f), new Vector3(0.05f, 0.78f, 0.05f), trim);
            art.Ball("Porch bulb", house, new Vector3(0, 2.53f, -2.45f), Vector3.one * 0.23f, Warm, true);
            art.Box("Porch hood", house, new Vector3(0, 2.72f, -2.39f), new Vector3(0.5f, 0.1f, 0.4f), trim);
            art.Text((id + 1).ToString("00"), house, new Vector3(0, 3.07f, -2.2f), 0.23f, Color.white);
            // Open roofs expose the core bed / door / weapon loop in the test arena.
            art.Box("Bed frame", house, new Vector3(-0.85f, 0.65f, 0.25f), new Vector3(1.5f, 0.45f, 2.35f), Navy);
            art.Box("Mattress", house, new Vector3(-0.85f, 0.94f, 0.25f), new Vector3(1.44f, 0.3f, 2.2f), new Color(1,0.9f,0.78f));
            art.Box("Duvet", house, new Vector3(-0.85f, 1.13f, -0.15f), new Vector3(1.45f, 0.18f, 1.5f), paint * 1.15f);
            art.Ball("Pillow", house, new Vector3(-0.85f, 1.2f, 1), new Vector3(1.1f, 0.22f, 0.6f), Color.white);
            art.Box("Weapon bench", house, new Vector3(1.05f, 0.8f, 0.4f), new Vector3(1.3f, 0.2f, 1.4f), trim);
            art.Shape("Turret mount", PrimitiveType.Cylinder, house, new Vector3(1.38f, 2.77f, -1.6f), new Vector3(0.65f, 0.2f, 0.65f), Navy);
            barrel = art.Box("Turret", house, new Vector3(1.38f, 3.07f, -1.85f), new Vector3(0.36f, 0.36f, 1.2f), Warm);
            art.Box("Health backing", house, new Vector3(0, 3.91f, -2.08f), new Vector3(3.7f, 0.2f, 0.16f), Navy);
            health = art.Box("Door health", house, new Vector3(0, 3.92f, -2.18f), new Vector3(3.55f, 0.12f, 0.08f), new Color(0.35f,0.95f,0.63f), true);
            label = art.Text("AVAILABLE", house, new Vector3(0, 4.4f, -1.8f), 0.095f, Color.white);
            label.transform.rotation = Quaternion.Euler(32, 0, 0);
            Plant(house, new Vector3(1.65f, 0.5f, -2.65f));
            return house;
        }

        private void Plant(Transform parent, Vector3 p)
        {
            art.Shape("Plant pot", PrimitiveType.Cylinder, parent, p, new Vector3(0.45f, 0.3f, 0.45f), new Color(0.67f,0.35f,0.23f));
            for (int i = 0; i < 3; i++)
                art.Ball("Leaf", parent, p + new Vector3((i - 1) * 0.18f, 0.5f + (i % 2) * 0.15f, 0), new Vector3(0.22f,0.7f,0.22f), new Color(0.22f,0.43f + i * 0.05f,0.23f));
        }
        private void Crate(Vector3 p)
        {
            Color wood = new Color(0.53f,0.35f,0.24f);
            art.Box("Cargo crate", root, p, Vector3.one * 0.8f, wood);
            for (int side = -1; side <= 1; side += 2)
                art.Box("Crate band", root, p + new Vector3(side * 0.28f,0,-0.42f), new Vector3(0.08f,0.8f,0.04f), Warm * 0.6f);
        }
        private void Lamp(Vector3 p)
        {
            art.Box("Lamp post", root, p + Vector3.up * 2.3f, new Vector3(0.14f,4.6f,0.14f), Navy);
            art.Box("Lantern", root, p + Vector3.up * 4.6f, new Vector3(0.35f,0.65f,0.35f), Warm, true);
            art.Box("Lantern cap", root, p + Vector3.up * 5, new Vector3(0.5f,0.13f,0.5f), Navy);
        }
        private void StringLights(float from, float to, float z, float y)
        {
            Vector3 last = Vector3.zero;
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                Vector3 point = new Vector3(Mathf.Lerp(from,to,t), y - Mathf.Sin(t * Mathf.PI) * 0.8f,z);
                art.Ball("String bulb", root, point, Vector3.one * 0.13f, Warm, true);
                if (i > 0)
                {
                    var cable = art.Line("Light cable", root, Navy * 0.3f, 0.025f);
                    cable.SetPosition(0, last); cable.SetPosition(1,point);
                }
                last = point;
            }
        }
    }
}
