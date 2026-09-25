using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace SwapHunter
{
    public sealed class SecurityGate : MonoBehaviour
    {
        public int number; public bool open;
        public GameObject panel;
        public void SetOpen(bool value) { open = value; panel.SetActive(!value); }
    }
    public sealed class Terminal : MonoBehaviour
    {
        public int stage, id;
        public string label;
        public bool activated;
        public Renderer lightPanel;
        public Vector3 Point => transform.position + Vector3.up;
        public void ResetTerminal() { activated = false; if (lightPanel) lightPanel.sharedMaterial = Shapes.Mat(4); }
        public void Activate() { activated = true; if (lightPanel) lightPanel.sharedMaterial = Shapes.Mat(10); }
    }
    public sealed class SupplyPickup : MonoBehaviour
    {
        public Vector3 origin;
        public bool collected;
        Transform visual;
        public void Initialize() { origin = transform.position; visual = transform.GetChild(0); }
        void Update()
        {
            if (collected || !DemoGame.I.IsPlaying) return;
            visual.localPosition = Vector3.up * (.45f + Mathf.Sin(Time.time * 2) * .10f); visual.Rotate(0, Time.deltaTime * 30, 0);
            if (Vector3.Distance(DemoGame.I.player.transform.position, origin) < 1.3f)
            {
                DemoGame.I.player.Supply(30, 36, 8); collected = true; visual.gameObject.SetActive(false);
            }
        }
        public void ResetPickup() { collected = false; if (visual) visual.gameObject.SetActive(true); }
    }
    public static class WorldBuilder
    {
        static DemoGame G;
        static Transform root;
        static GameObject Solid(string name, Vector3 p, Vector3 scale, int mat = 1)
        {
            var proxy = Shapes.Box(name, root, p, scale, mat, true, Layers.World);
            if (name.StartsWith("Deck ") || name == "Connector deck" || name == "Extraction deck" || name == "Elevated deck")
            {
                proxy.GetComponent<Renderer>().enabled = false;
                for (float x = -scale.x / 2; x < scale.x / 2 - .01f; x += 4)
                    for (float z = -scale.z / 2; z < scale.z / 2 - .01f; z += 4)
                    {
                        float w = Mathf.Min(4, scale.x / 2 - x), d = Mathf.Min(4, scale.z / 2 - z);
                        ImportedModels.Create(G.config.models.deck, root, p + new Vector3(x + w / 2, scale.y / 2, z + d / 2), new Vector3(w / 4, 1, d / 4));
                    }
            }
            else if (name == "Outer cargo barrier")
            {
                proxy.GetComponent<Renderer>().enabled = false;
                for (float z = -scale.z / 2; z < scale.z / 2 - .01f; z += 4)
                {
                    float span = Mathf.Min(4, scale.z / 2 - z);
                    var wall = ImportedModels.Create(G.config.models.bulkhead, root, p + new Vector3(0, -scale.y / 2, z + span / 2), new Vector3(span / 4, scale.y / 3.4f, scale.x / .6f));
                    wall.transform.localRotation = Quaternion.Euler(0, p.x > 0 ? 90 : -90, 0);
                }
            }
            return proxy;
        }
        static GameObject Deco(string name, Vector3 p, Vector3 scale, int mat = 3) => Shapes.Box(name, root, p, scale, mat);
        public static void Build(DemoGame game)
        {
            G = game; root = new GameObject("Aerial cargo port · modular environment").transform; G.world = root;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .54f, .65f);
            RenderSettings.ambientEquatorColor = new Color(.30f, .37f, .40f);
            RenderSettings.ambientGroundColor = new Color(.14f, .18f, .20f);
            Shader portSky = Resources.Load<Shader>("PortSky");
            if (portSky) { var sky = new Material(portSky); sky.name = "Original procedural port atmosphere"; RenderSettings.skybox = sky; }
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.38f, .50f, .57f); RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 230;
            Light sun = new GameObject("Afternoon sun").AddComponent<Light>(); sun.transform.SetParent(root); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(32, -42, 0); sun.color = new Color(1, .86f, .70f); sun.intensity = 1.85f; sun.shadows = LightShadows.Soft;
            Room(0, 9, 24, 28, "PHASE LAB", 4);
            Room(1, 48, 32, 36, "01   CARGO YARD", 4);
            Room(2, 92, 36, 36, "02   TRANSFER HALL", 5);
            Room(3, 137, 38, 38, "03   CONTROL TOWER", 6);
            for (int i = 0; i < 4; i++) Bridge(new[] { 26f, 70f, 114f, 160f }[i], i);
            Platform(new Vector3(7, 0, 15), 4, 6, 6);
            Stairs(new Vector3(7, 0, 5), 4, 12, .60f, 3);
            Platform(new Vector3(9, 0, 54), 4, 9, 10);
            Stairs(new Vector3(9, 0, 41.1f), 4, 12, .72f, 3);
            Platform(new Vector3(-11, 0, 96), 4, 8, 15);
            Stairs(new Vector3(-11, 0, 81), 4, 12, .65f, 3);
            Platform(new Vector3(12, 0, 144), 3.5f, 10, 17);
            Stairs(new Vector3(12, 0, 125.8f), 3.5f, 12, .85f, 3);
            Platform(new Vector3(-12, 0, 150), 7, 10, 10);
            Stairs(new Vector3(-12, 0, 128), 7, 24, .73f, 3);
            Crate(new Vector3(-4, 0, 10), new Vector3(2.7f, 1.3f, 2), 2);
            Crate(new Vector3(-8, 0, 44), new Vector3(4, 2.5f, 3), 2);
            Crate(new Vector3(0, 0, 44), new Vector3(3, 1.1f, 1.5f), 3);
            Crate(new Vector3(-6, 0, 56), new Vector3(3.5f, 2.4f, 4), 5);
            Crate(new Vector3(3, 0, 59), new Vector3(2.5f, 1.2f, 2), 2);
            Crate(new Vector3(6, 0, 83), new Vector3(4.5f, 2.5f, 4), 5);
            Crate(new Vector3(-3, 0, 88), new Vector3(2.4f, 1.15f, 2), 2);
            Crate(new Vector3(8, 0, 101), new Vector3(4, 2.6f, 4), 2);
            Crate(new Vector3(1, 0, 99), new Vector3(2, 1.2f, 3), 3);
            Crate(new Vector3(1, 0, 132), new Vector3(3, 1.2f, 2.5f), 2);
            Crate(new Vector3(-4, 0, 142), new Vector3(3, 2.4f, 3), 5);
            Crate(new Vector3(4, 0, 149), new Vector3(2.5f, 1.15f, 3), 3);
            for (int i = 0; i < 4; i++)
            {
                float z = 80 + i * 8;
                Deco("Warehouse beam", new Vector3(0, 8, z), new Vector3(34, .5f, .5f), 3);
                Deco("Warehouse roof light", new Vector3(0, 7.68f, z), new Vector3(8, .08f, .16f), 5);
                Solid("Pillar", new Vector3(16, 4, z), new Vector3(.6f, 8, .6f), 3);
                Solid("Pillar", new Vector3(-16, 4, z), new Vector3(.6f, 8, .6f), 3);
            }
            TerminalAt(new Vector3(0, 0, 20), 0, 0, "TRAINING EXIT");
            TerminalAt(new Vector3(-2, 0, 62), 1, 0, "RELEASE LOCKDOWN");
            TerminalAt(new Vector3(-11, 4, 102), 2, 0, "RELAY A");
            TerminalAt(new Vector3(11, 0, 96), 2, 1, "RELAY B");
            TerminalAt(new Vector3(0, 0, 153), 3, 0, "PHASE CORE");
            Solid("Extraction deck", new Vector3(0, -.4f, 173), new Vector3(18, .8f, 24), 0);
            TerminalAt(new Vector3(0, 0, 177), 4, 0, "EXTRACT");
            Sign("EXTRACTION", new Vector3(0, 5.5f, 180), 1.0f, new Color(.4f, 1, .85f));
            for (int i = 0; i < 7; i++) { Deco("Landing guide", new Vector3(-5, .03f, 166 + i * 2), new Vector3(.12f, .04f, 1), 4); Deco("Landing guide", new Vector3(5, .03f, 166 + i * 2), new Vector3(.12f, .04f, 1), 4); }
            Deco("Shuttle hull", new Vector3(0, 2.8f, 185), new Vector3(9, 2, 6), 8);
            Deco("Shuttle glass", new Vector3(0, 3.2f, 181.9f), new Vector3(5, .85f, .2f), 4);
            Deco("Shuttle wing", new Vector3(0, 2.3f, 186), new Vector3(17, .3f, 3), 3);
            for (int i = -1; i <= 1; i += 2) Deco("Shuttle thruster", new Vector3(i * 4, 1.1f, 185), new Vector3(1.1f, 1.2f, 2.5f), 5);
            Surroundings();
            // Doors are physical blockers in play, but their open passages must exist in the baked navigation.
            foreach (var gate in G.gates) gate.panel.SetActive(false);
            NavMeshSurface surface = root.gameObject.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.All;
            surface.layerMask = Layers.WorldMask; surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.BuildNavMesh();
            foreach (var gate in G.gates) gate.SetOpen(false);
            AnchorAt(new Vector3(7, 4.02f, 14)); AnchorAt(new Vector3(-10, .02f, 60)); AnchorAt(new Vector3(9, .02f, 103)); AnchorAt(new Vector3(12, 3.52f, 148));
            SupplyAt(new Vector3(-7, 0, 39)); SupplyAt(new Vector3(12, 4, 57)); SupplyAt(new Vector3(12, 0, 78)); SupplyAt(new Vector3(-10, 4, 99)); SupplyAt(new Vector3(-14, 0, 122)); SupplyAt(new Vector3(12, 3.5f, 151));
        }
        static void Room(int number, float z, float width, float depth, string title, int accent)
        {
            Solid("Deck " + title, new Vector3(0, -.35f, z), new Vector3(width, .7f, depth), 0);
            for (int side = -1; side <= 1; side += 2)
            {
                Solid("Outer cargo barrier", new Vector3(side * width / 2, 1.7f, z), new Vector3(.6f, 3.4f, depth), 1);
                Deco("Edge illumination", new Vector3(side * (width / 2 - .4f), .10f, z), new Vector3(.1f, .12f, depth - 1), accent);
                for (int i = 0; i < 5; i++)
                {
                    float x = side * (width / 2 - .15f), zz = z - depth / 2 + 2 + i * (depth - 4) / 4;
                    Solid("Structural rib", new Vector3(x, 3, zz), new Vector3(.8f, 6, .65f), 3);
                    Deco("Rib light", new Vector3(x - side * .45f, 3, zz), new Vector3(.07f, 2.4f, .13f), accent);
                }
                for (int end = -1; end <= 1; end += 2)
                    Solid("End wall", new Vector3(side * (width / 4 + 2), 2, z + end * depth / 2), new Vector3(width / 2 - 4, 4, .6f), 1);
            }
            for (int i = 0; i < 6; i++) Deco("Deck seam", new Vector3(0, .011f, z - depth / 2 + 2 + i * (depth - 4) / 5), new Vector3(width - 1, .015f, .04f), 3);
            Deco("Overhead gantry", new Vector3(0, 6, z + depth / 2 - 2), new Vector3(width, .65f, .7f), 3);
            Sign(title, new Vector3(0, 5.9f, z + depth / 2 - 2.45f), number == 0 ? .58f : .53f, new Color(.73f, .88f, .91f));
            if (number == 0) Solid("Lab rear wall", new Vector3(0, 2, -5), new Vector3(width, 4, .6f), 1);
        }
        static void Bridge(float z, int number)
        {
            Solid("Connector deck", new Vector3(0, -.3f, z), new Vector3(8, .6f, 10), 0);
            for (int side = -1; side <= 1; side += 2)
            {
                Solid("Bridge rail", new Vector3(side * 4, .8f, z), new Vector3(.3f, 1.6f, 10), 3);
                Deco("Bridge light", new Vector3(side * 3.7f, .06f, z), new Vector3(.1f, .06f, 9), 4);
                Solid("Gate upright", new Vector3(side * 3.6f, 2.5f, z), new Vector3(.65f, 5, 1), 3);
            }
            Deco("Gate header", new Vector3(0, 4.8f, z), new Vector3(8, .6f, 1), 3);
            GameObject door = new GameObject("Security shutter " + number); door.transform.SetParent(root);
            var gate = door.AddComponent<SecurityGate>(); gate.number = number;
            gate.panel = Shapes.Box("LOCKED", door.transform, new Vector3(0, 2, z), new Vector3(6.6f, 4, .28f), 11, true, Layers.World);
            ImportedModels.ReplaceBox(gate.panel, G.config.models.gate, new Vector3(6.6f, 4, .3f));
            G.gates.Add(gate);
        }
        static void Platform(Vector3 feet, float height, float width, float depth)
        {
            Solid("Elevated deck", feet + Vector3.up * (height - .25f), new Vector3(width, .5f, depth), 1);
            // Fascia and underside ribs sit within the existing platform collider volume.
            for (int side = -1; side <= 1; side += 2)
            {
                Deco("Platform steel fascia", feet + new Vector3(side * (width / 2 - .08f), height - .24f, 0), new Vector3(.12f, .30f, depth - .1f), 3);
                Deco("Platform beam end", feet + new Vector3(0, height - .36f, side * (depth / 2 - .10f)), new Vector3(width - .16f, .14f, .14f), 3);
            }
            for (int i = -1; i <= 1; i += 2)
            {
                Solid("Platform footing", feet + new Vector3(i * (width / 2 - .5f), height / 2, depth / 2 - .5f), new Vector3(.5f, height, .5f), 3);
                Deco("Platform light", feet + new Vector3(i * (width / 2 - .05f), height + .035f, 0), new Vector3(.08f, .06f, depth), 4);
            }
        }
        static void Stairs(Vector3 start, float height, int count, float tread, float width)
        {
            for (int i = 0; i < count; i++)
            {
                float h = height * (i + 1) / count;
                var stairCollision = Solid("Walkable stair", start + new Vector3(0, h / 2, i * tread), new Vector3(width, h, tread + .015f), 3);
                stairCollision.GetComponent<Renderer>().enabled = false;
                Deco("Steel stair tread", start + new Vector3(0, h - .05f, i * tread), new Vector3(width, .10f, tread + .012f), 3);
                Deco("Stair riser", start + new Vector3(0, h - height / count / 2, i * tread - tread / 2 + .035f), new Vector3(width - .12f, height / count, .06f), 1);
                Deco("Stair tread", start + new Vector3(0, h + .006f, i * tread - tread * .4f), new Vector3(width - .1f, .01f, .06f), 5);
            }
            float run = (count - 1) * tread;
            float rise = height - height / count;
            for (int side = -1; side <= 1; side += 2)
            {
                var beam = Deco("Stair side stringer", start + new Vector3(side * (width / 2 - .08f), height / count + rise / 2 - .15f, run / 2), new Vector3(.13f, .18f, Mathf.Sqrt(run * run + rise * rise) + .18f), 3);
                beam.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(rise, run) * Mathf.Rad2Deg, 0, 0);
                // A thin non-walkable navigation strip prevents Recast from connecting
                // the tall stair side faces. Geometry and CharacterController settings
                // remain unchanged; the front and upper landings stay open.
                var navigationEdge = new GameObject("Stair navigation side boundary");
                navigationEdge.layer = Layers.World; navigationEdge.transform.SetParent(root);
                navigationEdge.transform.position = start + new Vector3(side * width / 2, height / 2, run / 2);
                var boundary = navigationEdge.AddComponent<NavMeshModifierVolume>();
                boundary.center = Vector3.zero; boundary.size = new Vector3(.6f, height + .4f, run + tread + .2f); boundary.area = 1;
            }
        }
        static void Crate(Vector3 feet, Vector3 scale, int mat)
        {
            var proxy = Solid("Cargo container", feet + Vector3.up * scale.y / 2, scale, mat == 5 ? 12 : mat);
            ImportedModels.ReplaceBox(proxy, G.config.models.cargo, new Vector3(4, 2.4f, 3));
        }
        public static void Sign(string text, Vector3 point, float scale, Color color)
        {
            var go = new GameObject(text); go.transform.SetParent(root); go.transform.position = point;
            TextMesh label = go.AddComponent<TextMesh>(); label.text = text; label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64; label.characterSize = scale * .25f; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = color;
            label.font.RequestCharactersInTexture(text, 64);
            Material material = new Material(G.config.worldTextMaterial); material.mainTexture = label.font.material.mainTexture;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<WorldFontAtlas>().Initialize(label.font, material);
            Deco("Sign backing", point + Vector3.forward * .11f, new Vector3(text.Length * scale * .65f, scale * 1.65f, .12f), 8);
        }
        static void TerminalAt(Vector3 point, int stage, int id, string label)
        {
            GameObject go = new GameObject(label); go.transform.SetParent(root); go.transform.position = point;
            Terminal terminal = go.AddComponent<Terminal>(); terminal.stage = stage; terminal.id = id; terminal.label = label;
            var proxy = Shapes.Box("Console collision", go.transform, new Vector3(0, .70f, 0), new Vector3(1.02f, 1.4f, .8f), 8, true, Layers.World); proxy.GetComponent<Renderer>().enabled = false;
            var console = ImportedModels.Create(G.config.models.terminal, go.transform, Vector3.zero);
            foreach (Renderer renderer in console.GetComponentsInChildren<Renderer>())
                if (renderer.sharedMaterial && renderer.sharedMaterial.name.Contains("Phase emitter")) { terminal.lightPanel = renderer; break; }
            G.terminals.Add(terminal);
        }
        static void AnchorAt(Vector3 point)
        {
            GameObject go = new GameObject("Phase anchor"); go.layer = Layers.Actor; go.transform.SetParent(root); go.transform.position = point;
            PhaseAnchor anchor = go.AddComponent<PhaseAnchor>(); SphereCollider collider = go.AddComponent<SphereCollider>(); collider.radius = .42f; collider.center = Vector3.up * .95f;
            ImportedModels.Create(G.config.models.beacon, go.transform, Vector3.zero);
            Shapes.Ring(go.transform, point + Vector3.up * .06f, .68f, new Color(.25f, 1, .85f), .03f);
            G.anchors.Add(anchor); G.anchorOrigins.Add(point);
        }
        static void SupplyAt(Vector3 point)
        {
            GameObject go = new GameObject("Health and ammunition"); go.transform.SetParent(root); go.transform.position = point;
            ImportedModels.Create(G.config.models.supply, go.transform, Vector3.up * .45f);
            SupplyPickup pickup = go.AddComponent<SupplyPickup>(); pickup.Initialize(); G.supplies.Add(pickup);
        }
        static void Surroundings()
        {
            var random = new System.Random(820);
            var skyline = new List<GameObject>();
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = side * (49 + random.Next(0, 45));
                float z = random.Next(-40, 240), height = random.Next(24, 62), width = random.Next(8, 16), depth = random.Next(10, 19);
                var center = new Vector3(x, height / 2 - 19, z);
                skyline.Add(Deco("Port skyline stepped tower", center, new Vector3(width, height, depth), i % 3 == 0 ? 1 : 11));
                skyline.Add(Deco("Port skyline upper core", new Vector3(x + side * width * .12f, height - 19 + 3, z + 1), new Vector3(width * .65f, 6, depth * .68f), 3));
                skyline.Add(Deco("Port skyline vertical service spine", center + new Vector3(side * -width * .42f, 0, -depth * .51f), new Vector3(.65f, height - 2, .30f), 3));
                for (int j = 1; j < 4; j++)
                    skyline.Add(Deco("Port skyline floor band", center + new Vector3(0, height * (.2f * j - .42f), -depth * .506f), new Vector3(width * .78f, .13f, .05f), i % 4 == 0 ? 5 : 4));
            }
            for (int side = -1; side <= 1; side += 2)
            {
                skyline.Add(Deco("Crane mast", new Vector3(side * 25, 10, 48), new Vector3(1.2f, 35, 1.2f), 12));
                skyline.Add(Deco("Crane boom", new Vector3(side * 15, 26, 48), new Vector3(24, 1, 1.2f), 12));
                skyline.Add(Deco("Crane upper chord", new Vector3(side * 15, 28, 48), new Vector3(24, .30f, .45f), 3));
                for(int n = 0; n < 7; n++)
                {
                    var brace = Deco("Crane triangular web", new Vector3(side * 15 - 10 + n * 3.1f, 27, 48), new Vector3(.20f, 3.5f, .25f), 12);
                    brace.transform.localRotation = Quaternion.Euler(0, 0, n % 2 == 0 ? 55 : -55); skyline.Add(brace);
                }
                skyline.Add(Deco("Lift cable", new Vector3(side * 7, 18, 48), new Vector3(.06f, 16, .06f), 3));
                skyline.Add(Deco("Suspended lifting frame", new Vector3(side * 7, 10, 48), new Vector3(3, .22f, 2), 12));
            }
            // Merge static distant decoration by material: richer skyline without a renderer per small part.
            var batches = new Dictionary<Material, List<CombineInstance>>();
            foreach (GameObject item in skyline)
            {
                Material material = item.GetComponent<MeshRenderer>().sharedMaterial;
                if (!batches.TryGetValue(material, out var list)) { list = new List<CombineInstance>(); batches.Add(material, list); }
                list.Add(new CombineInstance { mesh = item.GetComponent<MeshFilter>().sharedMesh, transform = root.worldToLocalMatrix * item.transform.localToWorldMatrix });
                item.SetActive(false); Object.Destroy(item);
            }
            foreach (var entry in batches)
            {
                var batch = new GameObject("Batched port skyline / " + entry.Key.name); batch.transform.SetParent(root, false);
                var mesh = new Mesh { name = "Original skyline mesh" }; mesh.CombineMeshes(entry.Value.ToArray(), true, true);
                batch.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = batch.AddComponent<MeshRenderer>(); renderer.sharedMaterial = entry.Key; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
    public sealed class WorldFontAtlas : MonoBehaviour
    {
        Font font; Material material;
        public void Initialize(Font f, Material m) { font = f; material = m; Font.textureRebuilt += Refresh; }
        void Refresh(Font f) { if (f == font && material) material.mainTexture = font.material.mainTexture; }
        void OnDestroy() { Font.textureRebuilt -= Refresh; if (material) Destroy(material); }
    }
}
