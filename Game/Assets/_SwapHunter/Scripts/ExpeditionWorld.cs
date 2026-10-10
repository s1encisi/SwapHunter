using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SwapHunter
{
    public enum ExpeditionNodeKind { Objective, Extraction, Bonus, Jammer, Launch }

    public sealed class ExpeditionNode : MonoBehaviour
    {
        public ExpeditionNodeKind kind;
        public bool used, repeatable;
        public string label;
        public Renderer lamp;
        public int objectiveIndex;
        public TextMesh worldLabel;
        public LineRenderer marker;
        public Func<bool> useAction;
        bool extractionReady;

        public Vector3 Point => transform.position + Vector3.up;
        public string DisplayLabel => kind == ExpeditionNodeKind.Objective
            ? "[" + objectiveIndex.ToString("00") + "] " + label : label;

        public void SetLabel(string value, Color color)
        {
            if (worldLabel)
            {
                worldLabel.text = value;
                worldLabel.color = color;
                worldLabel.font.RequestCharactersInTexture(value, worldLabel.fontSize);
            }
            if (marker)
            {
                marker.startColor = marker.endColor = color;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                marker.SetPropertyBlock(block);
            }
        }

        public void Activate()
        {
            used = true;
            if (lamp) lamp.sharedMaterial = Shapes.Mat(10);
            SetLabel(DisplayLabel + " / 已完成", new Color(.52f, 1, .64f));
        }

        public void SetExtractionReady(bool ready)
        {
            if (kind != ExpeditionNodeKind.Extraction || extractionReady == ready) return;
            extractionReady = ready;
            if (lamp) lamp.sharedMaterial = Shapes.Mat(ready ? 10 : 4);
            SetLabel(label + " / " + (ready ? "可撤离" : "任务未完成"),
                ready ? new Color(.52f, 1, .64f) : new Color(.4f, .9f, 1));
        }
    }

    public sealed class ExpeditionWorldLabel : MonoBehaviour
    {
        public GameObject visibleWith;
        public float maximumDistance = 18;
        Renderer textRenderer;
        float nextVisibility;
        bool visible;

        void LateUpdate()
        {
            var game = DemoGame.I;
            if (!game || !game.player || !game.player.cameraView) return;
            if (!textRenderer) textRenderer = GetComponent<Renderer>();
            var camera = game.player.cameraView;
            Transform view = camera.transform;
            float distance = Vector3.Distance(transform.position, view.position);
            transform.rotation = view.rotation;
            transform.localScale = Vector3.one * Mathf.Clamp(distance / 14f, .28f, 1);
            bool ownerVisible = !visibleWith || visibleWith.activeInHierarchy;
            if (Time.unscaledTime >= nextVisibility)
            {
                nextVisibility = Time.unscaledTime + .16f;
                Vector3 viewport = camera.WorldToViewportPoint(transform.position);
                visible = ownerVisible && distance <= maximumDistance && viewport.z > 0
                    && viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1;
                if (visible && textRenderer)
                {
                    // This text plane faces the camera; test the complete label, not only its centre.
                    Bounds bounds = textRenderer.localBounds;
                    Vector3 low = camera.WorldToViewportPoint(transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z)));
                    Vector3 high = camera.WorldToViewportPoint(transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)));
                    visible = low.x >= .015f && low.y >= .015f && high.x <= .985f && high.y <= .985f;
                }
                if (visible && Physics.Linecast(view.position, transform.position, Layers.WorldMask, QueryTriggerInteraction.Ignore))
                    visible = false;
            }
            if (textRenderer) textRenderer.enabled = ownerVisible && visible;
        }
    }

    public sealed class ExpeditionMap
    {
        public Transform root;
        public Vector3 spawn = new Vector3(0, .04f, 3);
        public readonly List<ExpeditionNode> nodes = new List<ExpeditionNode>();
        public readonly List<Vector3> enemySpawns = new List<Vector3>();
        public readonly List<PhaseAnchor> anchors = new List<PhaseAnchor>();
        public readonly List<Vector3> walkingCheckpoints = new List<Vector3>();
        public int objectiveCount;
        public GameObject returnBridge;
        public NavMeshSurface navigation;
        public bool returnBridgeReady;
    }

    // Geometry and authored encounter placements share one coordinate system.
    // Gameplay conditions live in MissionRuntime, not in these mesh helpers.
    public static partial class ExpeditionWorld
    {
        static DemoGame g;
        static Transform root;
        static ExpeditionMap map;
        static MissionLayout layout;
        static int visualLevel;
        static readonly Color Cyan = new Color(.1f, .85f, 1);
        static readonly Color Amber = new Color(1, .55f, .15f);

        static GameObject Solid(string name, Vector3 position, Vector3 size, int material = 1)
        {
            var proxy=Shapes.Box(name,root,position,size,material,true,Layers.World);
            FinalEnvironmentModels.Structure(proxy);
            return proxy;
        }

        static void Floor(float x, float z, float width, float depth, float height = 0)
        {
            var proxy = Solid("Walkable deck", new Vector3(x, height - .35f, z), new Vector3(width, .7f, depth), 0);
            proxy.GetComponent<Renderer>().enabled = false;
            FinalEnvironmentModels.Create("RoofTile_Empty",root,new Vector3(x,height-.395f,z),new Vector3(width,.61f,depth));
            for (float xx = -width / 2; xx < width / 2 - .01f; xx += 4)
                for (float zz = -depth / 2; zz < depth / 2 - .01f; zz += 4)
                {
                    float w = Mathf.Min(4, width / 2 - xx), d = Mathf.Min(4, depth / 2 - zz);
                    FinalEnvironmentModels.Create("FloorTile_Basic",root,new Vector3(x+xx+w/2,height-.045f,z+zz+d/2),new Vector3(w,.09f,d));
                }
        }

        static void Cover(float x, float z, float height = 1.15f, float width = 2.6f, float floor = 0, float depth = 1.8f)
        {
            var cover = Solid("Tactical cover", new Vector3(x, floor + height / 2, z), new Vector3(width, height, depth), 2);
            cover.AddComponent<CoverSite>();
            FinalEnvironmentModels.Replace(cover,"Props_ContainerFull");
            for (int side = -1; side <= 1; side += 2)
                Shapes.Box("Steel cover trim", root, new Vector3(x, floor + height + .018f, z + side * depth * .5f),
                    new Vector3(width, .035f, .055f), 3);
        }

        static void Tower(float x, float z, float height, float width = 8, float depth = 10)
        {
            Floor(x, z, width, depth, height);
            // Visible fascia makes the raised walkable edge legible without adding blockers.
            for (int side = -1; side <= 1; side += 2)
            {
                FinalEnvironmentModels.Replace(Shapes.Box("Platform steel fascia", root, new Vector3(x + side * width * .5f, height - .22f, z), new Vector3(.12f, .44f, depth), 3),"Column_Slim",Quaternion.Euler(90,0,0));
                FinalEnvironmentModels.Replace(Shapes.Box("Platform steel fascia", root, new Vector3(x, height - .22f, z + side * depth * .5f), new Vector3(width, .44f, .12f), 3),"Column_Slim",Quaternion.Euler(0,0,90));
            }
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var support=Solid("Platform support", new Vector3(x + sx * (width / 2 - .5f), height / 2 - .1f, z + sz * (depth / 2 - .5f)),
                        new Vector3(.5f, height, .5f), 3);
                    
                }
        }

        // Stair tops overlap the platform edge; side rails preserve a clear walking lane.
        static void Stairs(float x, float startZ, float rise, float width = 3, float baseY = 0, float yaw = 0)
        {
            Transform previous = root;
            var group = new GameObject("Permanent stair route").transform;
            group.SetParent(root, false);
            group.position = new Vector3(x, baseY, startZ);
            group.rotation = Quaternion.Euler(0, yaw, 0);
            root = group;
            int steps = Mathf.CeilToInt(rise / .2f);
            float step = rise / steps, run = .5f;
            for (int i = 0; i < steps; i++)
            {
                float h = (i + 1) * step;
                Solid("Stair tread", new Vector3(0, h / 2, i * run), new Vector3(width, h, run + .025f), 1);
                Shapes.Box("Visible stair edge", root, new Vector3(0, h + .009f, i * run - .22f), new Vector3(width, .025f, .055f), 4);
            }
            float span = (steps - 1) * run;
            float angle = -Mathf.Atan2(rise - step, span) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = Solid("Stair safety rail", new Vector3(side * (width / 2 + .07f), (rise + step) / 2 + .55f, span / 2),
                    new Vector3(.14f, 1.2f, Mathf.Sqrt(span * span + (rise - step) * (rise - step)) + .25f), 3);
                rail.transform.localRotation = Quaternion.Euler(angle, 0, 0);
                var exclusion = new GameObject("Stair side navigation exclusion");
                exclusion.layer = Layers.World;
                exclusion.transform.SetParent(root, false);
                exclusion.transform.localPosition = new Vector3(side * width / 2, rise / 2, span / 2);
                var volume = exclusion.AddComponent<NavMeshModifierVolume>();
                volume.area = 1; volume.center = Vector3.zero; volume.size = new Vector3(.45f, rise + 1.4f, steps * run + .2f);
            }
            root = previous;
        }

        static void Ramp(float x, float z, float height, float length, float width, float baseY = 0, float yaw = 0, bool guideSides = false)
        {
            var ramp = Solid("Permanent access ramp", new Vector3(x, baseY + height / 2 - .15f, z),
                new Vector3(width, .3f, Mathf.Sqrt(length * length + height * height)), 1);
            ramp.transform.localRotation = Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(-Mathf.Atan2(height, length) * Mathf.Rad2Deg, 0, 0);
            if (!guideSides) return;
            // A navmesh may connect to a raised side edge which the player cannot step over.
            // Permanent refuges use rails and matching navigation exclusion along those edges.
            var guide = new GameObject("Refuge ramp edge guides").transform;
            guide.SetParent(root, false); guide.localPosition = new Vector3(x, baseY, z);
            guide.localRotation = Quaternion.Euler(0, yaw, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = Shapes.Box("Ramp safety rail", guide, new Vector3(side * (width / 2 + .06f), height / 2 + .45f, 0),
                    new Vector3(.12f, .9f, Mathf.Sqrt(length * length + height * height)), 3, true, Layers.World);
                rail.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(height, length) * Mathf.Rad2Deg, 0, 0);
                FinalEnvironmentModels.Replace(rail,"Column_Slim",Quaternion.Euler(90,0,0));
                var edge = new GameObject("Ramp side navigation exclusion"); edge.layer = Layers.World;
                edge.transform.SetParent(guide, false); edge.transform.localPosition = new Vector3(side * width / 2, height / 2, 0);
                var exclusion = edge.AddComponent<NavMeshModifierVolume>(); exclusion.area = 1; exclusion.center = Vector3.zero;
                exclusion.size = new Vector3(.5f, height + 1.8f, length + .2f);
            }
        }

        static TextMesh Label(string value, Vector3 position, float size = .5f, bool amber = false)
        {
            var go = new GameObject("Wayfinding");
            go.transform.SetParent(root, false);
            go.transform.localPosition = position;
            var text = go.AddComponent<TextMesh>();
            text.text = value; text.font = g.font; text.fontSize = 64;
            text.characterSize = size / 8; text.anchor = TextAnchor.MiddleCenter;
            text.color = amber ? Amber : Cyan;
            text.font.RequestCharactersInTexture(value, 64);
            if (g.config.worldTextMaterial)
            {
                var material = new Material(g.config.worldTextMaterial);
                material.mainTexture = text.font.material.mainTexture;
                text.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.AddComponent<WorldFontAtlas>().Initialize(text.font, material);
            }
            go.AddComponent<ExpeditionWorldLabel>();
            return text;
        }

        static PhaseAnchor Anchor(Vector3 position)
        {
            var go = new GameObject("Movable phase anchor");
            go.transform.SetParent(root, false); go.transform.position = position; go.layer = Layers.Actor;
            FinalEnvironmentModels.Anchor(go.transform);
            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * .68f; collider.radius = .32f; collider.height = 1.35f;
            var anchor = go.AddComponent<PhaseAnchor>();
            map.anchors.Add(anchor); g.anchors.Add(anchor); g.anchorOrigins.Add(position);
            Shapes.Ring(go.transform, position + Vector3.up * .035f, .85f, Cyan, .04f);
            return anchor;
        }

        static ExpeditionNode Node(Vector3 position, ExpeditionNodeKind kind, string label, bool flat = false)
        {
            var go = new GameObject(label);
            go.transform.SetParent(root, false); go.transform.position = position; go.layer = Layers.World;
            if (!flat)
            {
                var collider = go.AddComponent<BoxCollider>();
                collider.center = Vector3.up * .7f; collider.size = new Vector3(1.02f, 1.4f, .8f);
                FinalEnvironmentModels.Terminal(go.transform);
            }
            if(flat)FinalEnvironmentModels.Create("Props_Teleporter_1",go.transform,new Vector3(0,.035f,0),new Vector3(1.3f,.07f,1.3f));
            var node = go.AddComponent<ExpeditionNode>();
            node.kind = kind; node.label = label; map.nodes.Add(node);
            if (kind == ExpeditionNodeKind.Objective) node.objectiveIndex = ++map.objectiveCount;
            node.lamp = Shapes.Box("Node light", go.transform, new Vector3(0, flat ? .06f : 1.45f, 0),
                new Vector3(flat ? 1.3f : .85f, .08f, flat ? 1.3f : .1f), kind == ExpeditionNodeKind.Bonus ? 5 : 4).GetComponent<Renderer>();
            node.marker = Shapes.Ring(go.transform, position + Vector3.up * .04f, 1, kind == ExpeditionNodeKind.Bonus ? Amber : Cyan, .045f);
            node.worldLabel = Label(kind == ExpeditionNodeKind.Extraction ? label + " / 任务未完成" : node.DisplayLabel,
                position + Vector3.up * (flat ? 2.1f : 2.7f), .44f, kind == ExpeditionNodeKind.Bonus);
            return node;
        }

        static ExpeditionNode Objective(string id, Vector3 position, string title)
        {
            var node = Node(position, ExpeditionNodeKind.Objective, title);
            layout.BindObjective(node, id);
            return node;
        }

        static void Exit(Vector3 position, string title = "前方撤离口", Vector3? standingApproach = null)
        {
            Node(position, ExpeditionNodeKind.Extraction, title);
            Label("出口", position + Vector3.up * 4, .8f);
            map.walkingCheckpoints.Add(standingApproach ?? position + Vector3.back * 1.6f);
        }

        static void Bonus(Vector3 position, string title = "可选数据 / 成功带出 +40")
            => Node(position, ExpeditionNodeKind.Bonus, title);

        static void Spawn(string group, int index, Vector3 position, float yaw = 180, bool fixedPost = false)
        {
            layout.BindSpawn(group, index, position, yaw, fixedPost);
            map.enemySpawns.Add(position);
        }

        static void Region(string id, Vector3 center, Vector3 size)
            => layout.BindRegion(id, new Bounds(center, size));

        static void Door(string objective, Vector3 position, float width, string title)
        {
            var door = Solid(title, position + Vector3.up * 1.4f, new Vector3(width, 2.8f, .6f), 3);
            var sign = Label(title, position + Vector3.up * 3.2f, .4f);
            // Keep text under the unit-scale world root, not the scaled physical door.
            sign.GetComponent<ExpeditionWorldLabel>().visibleWith = door;
            layout.OpenOn(objective, door, false);
        }

        static void Bridge(string objective, float x, float z, float width, float depth, float height = 0)
        {
            Transform previous = root;
            var bridge = new GameObject("Relay bridge " + objective);
            bridge.transform.SetParent(root, false); root = bridge.transform;
            Floor(x, z, width, depth, height);
            Label("继电通路已接通", new Vector3(x, height + 1.8f, z), .35f);
            root = previous; bridge.SetActive(false);
            layout.OpenOn(objective, bridge);
            if (!map.returnBridge) map.returnBridge = bridge;
        }

        static void LaunchPad(Vector3 position)
        {
            var node = Node(position, ExpeditionNodeKind.Launch, "大跳装置 / 一次性", true);
            node.worldLabel.text = "大跳装置 / 一次性";
        }

        static void Supply(Vector3 position)
        {
            var go = new GameObject("Field supply");
            go.transform.SetParent(root, false); go.transform.position = position;
            FinalEnvironmentModels.Supply(go.transform,Vector3.zero);
            go.AddComponent<SupplyPickup>().Initialize();
            Label("整备补给", position + Vector3.up * 1.5f, .35f);
        }

        static void DamageTarget(string id, Vector3 position, string title, float health = 96)
        {
            var node = Objective(id, position, title);
            FinalEnvironmentModels.ReplaceNodeBody(node.transform,"Props_Laser");
            var target = node.gameObject.AddComponent<DamageableObjective>();
            target.Configure(id, health, _ => g.CompleteMissionObjective(id));
            node.useAction = () => { g.Toast("使用枪械、爆炸或近战破坏封锁装置", 2); return false; };
            Label("可破坏 / 近战可用", position + Vector3.up * 2.2f, .32f, true);
        }

        static void Socket(string id, Vector3 position, string title)
        {
            var node = Node(position, ExpeditionNodeKind.Objective, title, true);
            layout.BindObjective(node, id);
            var socket = node.gameObject.AddComponent<PhaseAnchorSocket>();
            socket.Configure(id, .9f, () => g.CompleteMissionObjective(id));
            node.useAction = () => { g.Toast("站在接入座上，与另一处锚点交换，将锚点送入接入座", 3); return false; };
        }

        static void Maintenance(Vector3 position)
        {
            var node = Node(position, ExpeditionNodeKind.Jammer, "维护复位 / 保留接入进度");
            node.repeatable = true;
            var reset = node.gameObject.AddComponent<AnchorMaintenanceReset>();
            var origins = new Vector3[map.anchors.Count];
            for (int i = 0; i < origins.Length; i++) origins[i] = map.anchors[i].transform.position;
            reset.Configure(map.anchors.ToArray(), origins);
            node.useAction = () =>
            {
                bool success = reset.TryReset();
                g.Toast(success ? "任务锚点已复位，接入进度保留" : "复位点有角色或障碍，请让开后再试", 3);
                return success;
            };
        }

        static void Jammer(string id, Vector3 center, float radius, Vector3 control, Vector3 emitter)
        {
            var go = new GameObject("Local interference " + id);
            go.transform.SetParent(root, false); go.transform.position = center;
            var field = go.AddComponent<PhaseInterferenceField>();
            field.Configure(id, radius);
            var node = Node(control, ExpeditionNodeKind.Jammer, "关闭局部干扰 / " + (id == "field-left" ? "左侧" : "右侧"));
            node.useAction = () =>
            {
                field.SetActive(false); node.Activate(); g.CompleteMissionObjective(id);
                g.Toast("局部干扰已关闭，其余区域规则不变", 3); return true;
            };
            var receiver = Node(emitter, ExpeditionNodeKind.Jammer, "可破坏的干扰发射器");
            FinalEnvironmentModels.ReplaceNodeBody(receiver.transform,"Props_Laser");
            var target = receiver.gameObject.AddComponent<DamageableObjective>();
            target.Configure(id + "-emitter", 96, _ => { field.SetActive(false); node.Activate(); receiver.Activate(); g.CompleteMissionObjective(id); });
            receiver.useAction = () => { g.Toast("可直接攻击发射器，也可使用独立控制器关闭", 2); return false; };
        }

        static void Courtyard(float width, float length)
        {
            Floor(0, length / 2 - 1, width, length);
            Solid("Rear perimeter", new Vector3(0, 1.5f, -1.5f), new Vector3(width + 1, 3, .5f), 1);
            Solid("Far perimeter", new Vector3(0, 1.5f, length - .5f), new Vector3(width + 1, 3, .5f), 1);
            for (int side = -1; side <= 1; side += 2)
                Solid("Side perimeter", new Vector3(side * (width / 2 + .25f), 1.5f, length / 2 - 1),
                    new Vector3(.5f, 3, length), 1);
        }

        static void Backdrop()
        {
            var sun = new GameObject("Chapter sun").AddComponent<Light>();
            sun.transform.SetParent(root, false); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(38, -38, 0);
            sun.color = new Color(.91f, .95f, 1); sun.intensity = 1.4f; sun.shadows = LightShadows.Soft;
            for (int i = 0; i < 8; i++)
            {
                float side = i % 2 == 0 ? -1 : 1, height = 14 + i % 3 * 7;
                FinalEnvironmentModels.Create("Props_Vessel_Tall",root,new Vector3(side*(32+i%3*9),height/2-7,8+i*9),new Vector3(7,height,9));
            }
        }

        public static ExpeditionMap Build(DemoGame game, int level)
        {
            if (level < 0 || level >= 12) throw new ArgumentOutOfRangeException(nameof(level));
            g = game; map = new ExpeditionMap(); visualLevel=level;
            root = new GameObject("相位行动 " + (level + 1).ToString("00") + " / " + ExpeditionCatalog.Levels[level].name).transform;
            map.root = root; layout = root.gameObject.AddComponent<MissionLayout>();
            try
            {
                Backdrop();
                switch (level)
                {
                    case 0: Calibration(); break;
                    case 1: ShieldLine(); break;
                    case 2: VerticalRoute(); break;
                    case 3: FirstExtraction(); break;
                    case 4: Crossfire(); break;
                    case 5: TransferDanger(); break;
                    case 6: RelayIslands(); break;
                    case 7: WarehouseChoices(); break;
                    case 8: TowerDescent(); break;
                    case 9: CutReinforcements(); break;
                    case 10: LocalInterference(); break;
                    case 11: CoreBreakthrough(); break;
                }
                Label((level + 1).ToString("00") + " / " + ExpeditionCatalog.Levels[level].name,
                    map.spawn + new Vector3(0, 4, 4), .75f);
                var surface = root.gameObject.AddComponent<NavMeshSurface>();
                map.navigation = surface; surface.collectObjects = CollectObjects.Children;
                surface.layerMask = Layers.WorldMask;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.overrideVoxelSize = true; surface.voxelSize = .08f;
                Physics.SyncTransforms(); RuntimeNavigationOwner.BuildFresh(surface); Physics.SyncTransforms();
                return map;
            }
            catch
            {
                for (int i = g.anchors.Count - 1; i >= 0; i--)
                    if (map.anchors.Contains(g.anchors[i])) { g.anchors.RemoveAt(i); g.anchorOrigins.RemoveAt(i); }
                if (root) { root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
                throw;
            }
        }
    }
}



