using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DuvalDrive.EditorTools
{
    /// <summary>
    /// Builds the whole prototype scene from code: a compressed Riverside
    /// (Jacksonville) neighborhood, a parking lot, and the student-driver car.
    /// Runs automatically the first time the project opens, and from the menu
    /// "Duval Drive > Rebuild Prototype Scene".
    /// </summary>
    [InitializeOnLoad]
    public static class PrototypeSceneBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/Riverside_Prototype.unity";
        const string GeneratedDir = "Assets/_Project/Generated";

        // Street grid: three east-west and three north-south streets, one block = 100 m.
        static readonly float[] Grid = { -100f, 0f, 100f };
        const float RoadHalf = 5f;         // 10 m wide two-lane street
        const float SidewalkWidth = 2f;

        static readonly string[] EastWestNames = { "Post St", "Park St", "Riverside Ave" };   // z = -100, 0, 100
        static readonly string[] NorthSouthNames = { "Margaret St", "Stockton St", "King St" }; // x = -100, 0, 100

        static readonly Vector3 SpawnPosition = new Vector3(6f, 0.6f, -136f);

        static Dictionary<string, Material> materials;
        static System.Random rng;

        static PrototypeSceneBuilder()
        {
            EditorApplication.delayCall += AutoBuildOnFirstOpen;
        }

        static void AutoBuildOnFirstOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath)) return;
            if (SessionState.GetBool("DuvalDrive.AutoBuilt", false)) return;
            SessionState.SetBool("DuvalDrive.AutoBuilt", true);
            Build();
            EditorUtility.DisplayDialog("Duval Drive",
                "Your Riverside driving scene has been built!\n\n" +
                "1. Press the Play button (top center of the window).\n" +
                "2. Click inside the Game view so the mouse can look around.\n" +
                "3. Press TAB in-game to see the controls.", "Let's drive");
        }

        [MenuItem("Duval Drive/Rebuild Prototype Scene")]
        static void RebuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Duval Drive",
                    "Rebuild the prototype scene? Any manual changes to it will be replaced.", "Rebuild", "Cancel"))
                return;
            Build();
        }

        [MenuItem("Duval Drive/Open Prototype Scene")]
        static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { Build(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        // ------------------------------------------------------------------ build

        static void Build()
        {
            materials = new Dictionary<string, Material>();
            rng = new System.Random(904); // Jacksonville area code
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder(GeneratedDir);
            EnsureFolder(GeneratedDir + "/Materials");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildEnvironment();
            var world = new GameObject("World").transform;
            BuildGround(world);
            BuildStreets(world);
            BuildParkingLot(world);
            BuildScenery(world);
            var car = BuildCar();
            BuildSystems(car);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Duval Drive] Built " + ScenePath);
        }

        static void BuildEnvironment()
        {
            var sun = new GameObject("Sun (Florida afternoon)");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            RenderSettings.sun = light;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.32f, 0.25f);

            // Humid haze.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0025f;
            RenderSettings.fogColor = new Color(0.75f, 0.8f, 0.85f);
        }

        static void BuildGround(Transform world)
        {
            var ground = Box("Ground (grass)", world, new Vector3(0f, -0.5f, -40f), new Vector3(500f, 1f, 330f), Mat("Grass", new Color(0.33f, 0.47f, 0.22f)));
            ground.AddComponent<Surface>().type = SurfaceType.Grass;

            // The St. Johns River along the north edge, with downtown across the water.
            var water = Box("St. Johns River", world, new Vector3(0f, -1.2f, 300f), new Vector3(1200f, 1f, 360f), Mat("River", new Color(0.18f, 0.3f, 0.33f)));
            Object.DestroyImmediate(water.GetComponent<Collider>());
            Box("Seawall", world, new Vector3(0f, -0.4f, 125.5f), new Vector3(500f, 1f, 1f), Mat("Concrete", new Color(0.7f, 0.7f, 0.68f)));
            Box("Far bank", world, new Vector3(0f, -0.6f, 560f), new Vector3(1400f, 1f, 200f), Mat("Grass", Color.white), false);

            var skyline = new GameObject("Downtown skyline").transform;
            skyline.SetParent(world, false);
            float[,] towers =
            {
                { 260f, 150f, 30f }, { 300f, 110f, 26f }, { 340f, 175f, 34f }, { 380f, 95f, 24f },
                { 420f, 130f, 28f }, { 230f, 80f, 22f }, { 460f, 70f, 30f }, { 320f, 60f, 40f },
            };
            for (int i = 0; i < towers.GetLength(0); i++)
            {
                float x = towers[i, 0], hgt = towers[i, 1], w = towers[i, 2];
                var c = Color.Lerp(new Color(0.55f, 0.62f, 0.7f), new Color(0.75f, 0.73f, 0.68f), (float)rng.NextDouble());
                Box("Tower " + (i + 1), skyline, new Vector3(x, hgt / 2f - 0.6f, 520f + i % 3 * 25f), new Vector3(w, hgt, w), Mat("Tower" + i, c), false);
            }
        }

        // ---------------------------------------------------------------- streets

        static void BuildStreets(Transform world)
        {
            var streets = new GameObject("Streets").transform;
            streets.SetParent(world, false);
            Material asphalt = Mat("Asphalt", new Color(0.2f, 0.2f, 0.21f));

            // Road segments between intersections.
            for (int line = 0; line < Grid.Length; line++)
            {
                for (int i = 0; i < Grid.Length - 1; i++)
                {
                    float a = Grid[i] + RoadHalf, b = Grid[i + 1] - RoadHalf;
                    float mid = (a + b) / 2f, len = b - a;

                    // East-west street at z = Grid[line]
                    string ew = EastWestNames[line];
                    float ewLimit = ew == "Riverside Ave" ? 35f : 30f;
                    bool school = ew == "Park St" && i == 1;
                    Segment(streets, ew, new Vector3(mid, 0f, Grid[line]), Vector3.right, len, ewLimit, school, asphalt);

                    // North-south street at x = Grid[line]
                    Segment(streets, NorthSouthNames[line], new Vector3(Grid[line], 0f, mid), Vector3.forward, len, 30f, false, asphalt);
                }
            }

            // Intersections.
            for (int xi = 0; xi < Grid.Length; xi++)
                for (int zi = 0; zi < Grid.Length; zi++)
                    Intersection(streets, xi, zi, asphalt);
        }

        static void Segment(Transform parent, string street, Vector3 center, Vector3 axis, float length, float limit, bool school, Material asphalt)
        {
            var go = new GameObject(street + " block");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.rotation = Quaternion.LookRotation(axis);
            var t = go.transform;

            var road = Box("Road", t, new Vector3(0f, 0f, 0f), new Vector3(RoadHalf * 2f, 0.04f, length), asphalt);
            road.AddComponent<Surface>().type = SurfaceType.Road;

            // Double yellow center line and white edge lines.
            Material yellow = Mat("Paint yellow", new Color(0.95f, 0.75f, 0.1f), true);
            Material white = Mat("Paint white", new Color(0.95f, 0.95f, 0.95f), true);
            Box("Center line L", t, new Vector3(-0.12f, 0.025f, 0f), new Vector3(0.12f, 0.01f, length), yellow, false);
            Box("Center line R", t, new Vector3(0.12f, 0.025f, 0f), new Vector3(0.12f, 0.01f, length), yellow, false);
            Box("Edge line L", t, new Vector3(-RoadHalf + 0.4f, 0.025f, 0f), new Vector3(0.12f, 0.01f, length), white, false);
            Box("Edge line R", t, new Vector3(RoadHalf - 0.4f, 0.025f, 0f), new Vector3(0.12f, 0.01f, length), white, false);

            Material concrete = Mat("Sidewalk", new Color(0.72f, 0.71f, 0.68f));
            for (int side = -1; side <= 1; side += 2)
            {
                var sw = Box("Sidewalk", t, new Vector3(side * (RoadHalf + SidewalkWidth / 2f), 0.06f, 0f), new Vector3(SidewalkWidth, 0.12f, length), concrete);
                sw.AddComponent<Surface>().type = SurfaceType.Sidewalk;
            }

            // Trigger that tells the rules engine which street we're on.
            var zone = new GameObject("Zone: " + street + " (" + limit + " mph)");
            zone.layer = 2; // Ignore Raycast
            zone.transform.SetParent(t, false);
            var bc = zone.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3(0f, 1.5f, 0f);
            bc.size = new Vector3(RoadHalf * 2f, 3f, length);
            var seg = zone.AddComponent<RoadSegment>();
            seg.streetName = street;
            seg.limitMph = limit;
            seg.axis = axis;

            SpeedLimitSign(t, limit, length);

            if (school) SchoolZone(t, seg, length);
        }

        static void SpeedLimitSign(Transform segment, float limit, float length)
        {
            // One sign per direction near the start of the block, on the right-hand side.
            Material white = Mat("Sign white", Color.white, true);
            Material black = Mat("Sign black", new Color(0.08f, 0.08f, 0.08f), true);
            for (int dir = -1; dir <= 1; dir += 2)
            {
                var root = new GameObject("Speed limit " + limit).transform;
                root.SetParent(segment, false);
                root.localPosition = new Vector3(dir * (RoadHalf + 1.2f), 0f, -dir * (length / 2f - 8f));
                root.localRotation = Quaternion.LookRotation(dir > 0 ? Vector3.forward : Vector3.back);
                Pole(root, 2.6f);
                Box("Panel", root, new Vector3(0f, 2.3f, -0.05f), new Vector3(0.6f, 0.75f, 0.03f), white, false);
                // Two dark bars standing in for the "SPEED LIMIT" text; the HUD shows the number.
                Box("Text", root, new Vector3(0f, 2.5f, -0.07f), new Vector3(0.42f, 0.06f, 0.01f), black, false);
                Box("Number", root, new Vector3(0f, 2.2f, -0.07f), new Vector3(0.3f, 0.22f, 0.01f), black, false);
            }
        }

        static void SchoolZone(Transform segment, RoadSegment seg, float length)
        {
            seg.schoolZone = true;
            seg.schoolLimitMph = 20f;
            Material yellowGreen = Mat("School sign", new Color(0.75f, 1f, 0.15f), true);
            Material lampMat = Mat("Beacon", new Color(0.3f, 0.25f, 0.05f), true);
            var beacons = new List<Renderer>();
            for (int dir = -1; dir <= 1; dir += 2)
            {
                var root = new GameObject("School zone beacon").transform;
                root.SetParent(segment, false);
                root.localPosition = new Vector3(dir * (RoadHalf + 1.2f), 0f, -dir * (length / 2f - 3f));
                root.localRotation = Quaternion.LookRotation(dir > 0 ? Vector3.forward : Vector3.back);
                Pole(root, 3.4f);
                Box("Sign", root, new Vector3(0f, 2.4f, -0.05f), new Vector3(0.8f, 0.9f, 0.03f), yellowGreen, false);
                beacons.Add(Sphere("Beacon L", root, new Vector3(-0.25f, 3.2f, -0.1f), 0.22f, lampMat).GetComponent<Renderer>());
                beacons.Add(Sphere("Beacon R", root, new Vector3(0.25f, 3.2f, -0.1f), 0.22f, lampMat).GetComponent<Renderer>());
            }
            seg.beacons = beacons.ToArray();

            // Crosswalk in front of the school.
            Material white = Mat("Paint white", Color.white, true);
            for (int i = 0; i < 6; i++)
                Box("Crosswalk stripe", segment, new Vector3(-RoadHalf + 0.9f + i * 1.65f, 0.026f, 0f), new Vector3(0.6f, 0.01f, 3f), white, false);
        }

        static void Intersection(Transform parent, int xi, int zi, Material asphalt)
        {
            float x = Grid[xi], z = Grid[zi];
            string ns = NorthSouthNames[xi], ew = EastWestNames[zi];
            var root = new GameObject(ns + " & " + ew).transform;
            root.SetParent(parent, false);
            root.position = new Vector3(x, 0f, z);

            var road = Box("Road", root, Vector3.zero, new Vector3(RoadHalf * 2f, 0.04f, RoadHalf * 2f), asphalt);
            road.AddComponent<Surface>().type = SurfaceType.Road;

            var zone = new GameObject("Zone: intersection");
            zone.layer = 2;
            zone.transform.SetParent(root, false);
            var bc = zone.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3(0f, 1.5f, 0f);
            bc.size = new Vector3(RoadHalf * 2f, 3f, RoadHalf * 2f);
            zone.AddComponent<IntersectionZone>().intersectionName = ns + " & " + ew;

            bool hasN = zi < Grid.Length - 1, hasS = zi > 0, hasE = xi < Grid.Length - 1, hasW = xi > 0;
            bool driveway = xi == 1 && zi == 0; // parking lot exit joins from the south

            // Stop control: 4-way at the center; elsewhere the street that ends at a T stops;
            // and the parking-lot driveway stops before Post St.
            bool center = xi == 1 && zi == 1;
            bool nsEnds = !(hasN && hasS);
            bool ewEnds = !(hasE && hasW);
            bool corner = nsEnds && ewEnds;

            if (center)
            {
                StopApproach(root, Vector3.forward, ew);
                StopApproach(root, Vector3.back, ew);
                StopApproach(root, Vector3.right, ns);
                StopApproach(root, Vector3.left, ns);
            }
            else if (!corner)
            {
                if (nsEnds)
                {
                    if (hasS) StopApproach(root, Vector3.forward, ew); // northbound traffic arrives from the south
                    if (hasN) StopApproach(root, Vector3.back, ew);
                }
                if (ewEnds)
                {
                    if (hasW) StopApproach(root, Vector3.right, ns);
                    if (hasE) StopApproach(root, Vector3.left, ns);
                }
            }
            if (driveway) StopApproach(root, Vector3.forward, ew);
        }

        /// <summary>Stop line, stop sign and stop zone for traffic travelling in <paramref name="travel"/>.</summary>
        static void StopApproach(Transform intersection, Vector3 travel, string crossStreet)
        {
            var root = new GameObject("Stop (" + DirName(travel) + "bound)").transform;
            root.SetParent(intersection, false);
            root.localRotation = Quaternion.LookRotation(travel);
            // In this local frame the car drives toward +z and the intersection starts at z = -RoadHalf.
            root.localPosition = Vector3.zero;

            Material white = Mat("Paint white", Color.white, true);
            Box("Stop line", root, new Vector3(RoadHalf / 2f, 0.026f, -RoadHalf - 0.5f), new Vector3(RoadHalf - 0.3f, 0.01f, 0.45f), white, false);

            // Stop sign on the right-hand corner, facing the driver.
            var sign = new GameObject("Stop sign").transform;
            sign.SetParent(root, false);
            sign.localPosition = new Vector3(RoadHalf + 1.2f, 0f, -RoadHalf - 1.5f);
            Pole(sign, 2.4f);
            var face = new GameObject("Octagon");
            face.transform.SetParent(sign, false);
            face.transform.localPosition = new Vector3(0f, 2.2f, -0.06f);
            face.AddComponent<MeshFilter>().sharedMesh = OctagonMesh();
            face.AddComponent<MeshRenderer>().sharedMaterial = Mat("Stop red", new Color(0.8f, 0.05f, 0.05f), true);
            Box("Stop text bar", sign, new Vector3(0f, 2.2f, -0.075f), new Vector3(0.5f, 0.14f, 0.01f), Mat("Sign white", Color.white, true), false);

            var zone = new GameObject("Zone: stop");
            zone.layer = 2;
            zone.transform.SetParent(root, false);
            zone.transform.localPosition = new Vector3(RoadHalf / 2f, 1.5f, -RoadHalf - 4.5f);
            var bc = zone.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(RoadHalf, 3f, 8f);
            zone.AddComponent<StopZone>().crossStreet = crossStreet;
        }

        static string DirName(Vector3 d)
        {
            if (d == Vector3.forward) return "north";
            if (d == Vector3.back) return "south";
            if (d == Vector3.right) return "east";
            return "west";
        }

        // ------------------------------------------------------------ parking lot

        static void BuildParkingLot(Transform world)
        {
            var lot = new GameObject("First Coast Grocers lot").transform;
            lot.SetParent(world, false);
            Material asphalt = Mat("Lot asphalt", new Color(0.24f, 0.24f, 0.25f));
            Material white = Mat("Paint white", Color.white, true);

            var surface = Box("Lot surface", lot, new Vector3(0f, 0f, -132f), new Vector3(70f, 0.04f, 38f), asphalt);
            surface.AddComponent<Surface>().type = SurfaceType.ParkingLot;
            var drive = Box("Driveway", lot, new Vector3(0f, 0f, -109f), new Vector3(RoadHalf * 2f, 0.04f, 8.2f), asphalt);
            drive.AddComponent<Surface>().type = SurfaceType.Road;

            // Two rows of stalls facing each other across a center aisle.
            for (int i = -6; i <= 6; i++)
            {
                if (i == 0) continue; // aisle to the driveway
                float x = i * 3f;
                Box("Stall line", lot, new Vector3(x - 1.5f, 0.026f, -122f), new Vector3(0.12f, 0.01f, 5.5f), white, false);
                Box("Stall line", lot, new Vector3(x - 1.5f, 0.026f, -140f), new Vector3(0.12f, 0.01f, 5.5f), white, false);
            }

            // The store, with parody branding.
            Box("First Coast Grocers", lot, new Vector3(0f, 4f, -162f), new Vector3(60f, 8f, 20f), Mat("Store", new Color(0.88f, 0.84f, 0.74f)));
            Box("Store sign", lot, new Vector3(0f, 7f, -151.9f), new Vector3(20f, 1.6f, 0.2f), Mat("Store green", new Color(0.1f, 0.45f, 0.25f), true), false);
            Box("Awning", lot, new Vector3(0f, 3.4f, -151f), new Vector3(30f, 0.3f, 2f), Mat("Store green", Color.white, true), false);

            // A couple of parked cars to steer around.
            ParkedCar(lot, new Vector3(-9f, 0f, -140f), Mat("Car red", new Color(0.6f, 0.1f, 0.1f)));
            ParkedCar(lot, new Vector3(15f, 0f, -122f), Mat("Car blue", new Color(0.15f, 0.25f, 0.55f)));
            ParkedCar(lot, new Vector3(-12f, 0f, -122f), Mat("Car silver", new Color(0.65f, 0.66f, 0.68f)));
        }

        static void ParkedCar(Transform parent, Vector3 pos, Material paint)
        {
            var car = new GameObject("Parked car").transform;
            car.SetParent(parent, false);
            car.position = pos;
            Box("Body", car, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.8f, 4.4f), paint);
            Box("Cabin", car, new Vector3(0f, 1.4f, -0.3f), new Vector3(1.6f, 0.6f, 2.2f), Mat("Glass dark", new Color(0.15f, 0.18f, 0.2f)), false);
        }

        // ---------------------------------------------------------------- scenery

        static void BuildScenery(Transform world)
        {
            var houses = new GameObject("Houses").transform;
            houses.SetParent(world, false);
            var trees = new GameObject("Trees").transform;
            trees.SetParent(world, false);

            Color[] paint =
            {
                new Color(0.96f, 0.82f, 0.7f),  // coral-cream
                new Color(0.75f, 0.9f, 0.82f),  // mint
                new Color(0.95f, 0.92f, 0.78f), // cream
                new Color(0.72f, 0.82f, 0.93f), // pale blue
                new Color(0.97f, 0.88f, 0.55f), // soft yellow
                new Color(0.9f, 0.9f, 0.88f),   // white
            };

            for (int line = 0; line < Grid.Length; line++)
            {
                for (int i = 0; i < Grid.Length - 1; i++)
                {
                    float a = Grid[i] + RoadHalf, b = Grid[i + 1] - RoadHalf;
                    float zLine = Grid[line], xLine = Grid[line];
                    for (int side = -1; side <= 1; side += 2)
                    {
                        // Along east-west streets.
                        bool riverfront = line == 2 && side > 0;
                        bool schoolSide = line == 1 && i == 1 && side < 0;
                        bool lotSide = line == 0 && side < 0; // grocery parking lot south of Post St
                        if (schoolSide)
                        {
                            Box("Oak Hollow Elementary", houses, new Vector3((a + b) / 2f, 3.5f, zLine - 28f), new Vector3(60f, 7f, 30f), Mat("School brick", new Color(0.65f, 0.35f, 0.28f)));
                        }
                        else if (riverfront)
                        {
                            // Riverfront park: palms instead of houses.
                            for (float x = a + 6f; x < b - 4f; x += 12f) Palm(trees, new Vector3(x, 0f, zLine + 12f));
                        }
                        else
                        {
                            for (float x = a + 14f; x < b - 8f; x += 18f)
                                if (!lotSide || Mathf.Abs(x) > 42f)
                                    House(houses, new Vector3(x, 0f, zLine + side * 15f), side > 0 ? 180f : 0f, paint);
                        }

                        // Along north-south streets (start further from the corner to leave room).
                        for (float zz = a + 28f; zz < b - 22f; zz += 18f)
                            House(houses, new Vector3(xLine + side * 15f, 0f, zz), side > 0 ? -90f : 90f, paint);

                        // Live oaks with Spanish moss between houses and the sidewalk.
                        for (float t = a + 6f; t < b - 4f; t += 22f)
                        {
                            float jitter = (float)rng.NextDouble() * 6f;
                            bool inDriveway = lotSide && Mathf.Abs(t + jitter) < 9f;
                            if (!riverfront && !inDriveway) LiveOak(trees, new Vector3(t + jitter, 0f, zLine + side * 9.5f));
                            LiveOak(trees, new Vector3(xLine + side * 9.5f, 0f, t + jitter));
                        }
                    }
                }
            }
        }

        static void House(Transform parent, Vector3 pos, float yaw, Color[] paint)
        {
            var h = new GameObject("House").transform;
            h.SetParent(parent, false);
            h.position = pos;
            h.rotation = Quaternion.Euler(0f, yaw, 0f);
            int c = rng.Next(paint.Length);
            float w = 9f + (float)rng.NextDouble() * 3f;
            float hgt = 4f + (rng.Next(3) == 0 ? 3f : 0f);
            Box("Walls", h, new Vector3(0f, hgt / 2f, 0f), new Vector3(w, hgt, 9f), Mat("House " + c, paint[c]));
            Box("Roof", h, new Vector3(0f, hgt + 0.4f, 0f), new Vector3(w + 1f, 0.8f, 10f), Mat("Roof", new Color(0.35f, 0.3f, 0.28f)), false);
            Box("Porch", h, new Vector3(0f, 0.2f, 5.5f), new Vector3(w * 0.6f, 0.4f, 2f), Mat("Porch", new Color(0.6f, 0.58f, 0.55f)), false);
        }

        static void LiveOak(Transform parent, Vector3 pos)
        {
            var t = new GameObject("Live oak").transform;
            t.SetParent(parent, false);
            t.position = pos;
            var trunk = Cylinder("Trunk", t, new Vector3(0f, 2f, 0f), new Vector3(0.7f, 2f, 0.7f), Mat("Bark", new Color(0.3f, 0.24f, 0.18f)));
            trunk.GetComponent<CapsuleCollider>().radius = 0.5f;
            Material leaves = Mat("Oak leaves", new Color(0.18f, 0.3f, 0.14f));
            Material moss = Mat("Spanish moss", new Color(0.58f, 0.62f, 0.55f));
            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(ang) * 2.8f, 5f + (float)rng.NextDouble(), Mathf.Sin(ang) * 2.8f);
                Sphere("Canopy", t, p, 4.5f + (float)rng.NextDouble() * 1.5f, leaves, false);
                Box("Moss", t, p + new Vector3(0.6f, -2.2f, 0.4f), new Vector3(0.25f, 1.6f, 0.25f), moss, false);
            }
            Sphere("Canopy top", t, new Vector3(0f, 6.8f, 0f), 5.5f, leaves, false);
        }

        static void Palm(Transform parent, Vector3 pos)
        {
            var t = new GameObject("Sabal palm").transform;
            t.SetParent(parent, false);
            t.position = pos;
            Cylinder("Trunk", t, new Vector3(0f, 4f, 0f), new Vector3(0.45f, 4f, 0.45f), Mat("Palm trunk", new Color(0.5f, 0.45f, 0.36f)));
            Material frond = Mat("Palm frond", new Color(0.25f, 0.42f, 0.18f));
            for (int i = 0; i < 7; i++)
            {
                var f = Box("Frond", t, new Vector3(0f, 8f, 0f), new Vector3(0.6f, 0.1f, 3.4f), frond, false);
                f.transform.localRotation = Quaternion.Euler(25f, i * (360f / 7f), 0f);
                f.transform.localPosition = new Vector3(0f, 8f, 0f) + f.transform.localRotation * new Vector3(0f, 0f, 1.5f);
            }
        }

        // -------------------------------------------------------------------- car

        static GameObject BuildCar()
        {
            var root = new GameObject("Student Driver Car");
            root.transform.position = SpawnPosition;
            root.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var t = root.transform;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1400f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var body = root.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, 0.95f, 0f);
            body.size = new Vector3(1.8f, 0.9f, 4.4f);

            Material paint = Mat("Car white", new Color(0.93f, 0.93f, 0.92f));
            Material trim = Mat("Car trim", new Color(0.1f, 0.1f, 0.11f));
            Material interior = Mat("Interior", new Color(0.16f, 0.16f, 0.17f));
            Material seat = Mat("Seat", new Color(0.22f, 0.2f, 0.19f));

            // Exterior shell. You sit at eye height 1.32 m; the body top (1.1 m) is the window sill.
            Box("Body", t, new Vector3(0f, 0.8f, 0f), new Vector3(1.8f, 0.6f, 4.4f), paint, false);
            Box("Roof", t, new Vector3(0f, 1.76f, -0.35f), new Vector3(1.7f, 0.06f, 2.0f), paint, false);
            Box("Headliner", t, new Vector3(0f, 1.72f, -0.35f), new Vector3(1.6f, 0.02f, 1.9f), Mat("Headliner", new Color(0.6f, 0.58f, 0.55f)), false);
            for (int s = -1; s <= 1; s += 2)
            {
                var aPillar = Box("A-pillar", t, new Vector3(s * 0.8f, 1.43f, 0.8f), new Vector3(0.07f, 0.72f, 0.09f), trim, false);
                aPillar.transform.localRotation = Quaternion.Euler(-24.8f, 0f, 0f);
                Box("B-pillar", t, new Vector3(s * 0.84f, 1.43f, -0.6f), new Vector3(0.07f, 0.66f, 0.14f), paint, false);
                var cPillar = Box("C-pillar", t, new Vector3(s * 0.8f, 1.43f, -1.45f), new Vector3(0.07f, 0.72f, 0.25f), paint, false);
                cPillar.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            }

            // Roof sign.
            Box("STUDENT DRIVER sign", t, new Vector3(0f, 1.93f, -0.6f), new Vector3(1.1f, 0.28f, 0.1f), Mat("Student sign", new Color(1f, 0.85f, 0.1f), true), false);

            // Interior.
            Box("Dashboard", t, new Vector3(0f, 1.05f, 0.75f), new Vector3(1.7f, 0.22f, 0.45f), interior, false);
            Box("Instrument hood", t, new Vector3(-0.37f, 1.2f, 0.7f), new Vector3(0.5f, 0.08f, 0.15f), interior, false);
            Box("Cabin floor trim", t, new Vector3(0f, 1.115f, -0.4f), new Vector3(1.72f, 0.02f, 1.9f), interior, false);
            Box("Center console", t, new Vector3(0f, 1.17f, 0.1f), new Vector3(0.25f, 0.1f, 0.9f), interior, false);
            Box("Driver seat back", t, new Vector3(-0.37f, 1.25f, -0.42f), new Vector3(0.5f, 0.75f, 0.12f), seat, false);
            Box("Passenger seat back", t, new Vector3(0.37f, 1.25f, -0.42f), new Vector3(0.5f, 0.75f, 0.12f), seat, false);
            Box("Rear bench", t, new Vector3(0f, 1.2f, -1.25f), new Vector3(1.5f, 0.6f, 0.15f), seat, false);

            // Your instructor in the passenger seat (look right to see them).
            Capsule("Instructor", t, new Vector3(0.37f, 1.25f, -0.2f), new Vector3(0.42f, 0.38f, 0.32f), Mat("Instructor shirt", new Color(0.2f, 0.35f, 0.6f)));
            Sphere("Instructor head", t, new Vector3(0.37f, 1.6f, -0.18f), 0.22f, Mat("Skin", new Color(0.6f, 0.43f, 0.32f)), false);

            // Steering wheel: pivot tilted toward the driver, spinner rotates around its Z axis.
            var column = new GameObject("Steering column").transform;
            column.SetParent(t, false);
            column.localPosition = new Vector3(-0.37f, 1.1f, 0.42f);
            column.localRotation = Quaternion.Euler(25f, 0f, 0f);
            var spinner = new GameObject("Steering wheel").transform;
            spinner.SetParent(column, false);
            var rim = Cylinder("Rim", spinner, Vector3.zero, new Vector3(0.38f, 0.015f, 0.38f), trim);
            Object.DestroyImmediate(rim.GetComponent<Collider>());
            rim.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Box("Spoke", spinner, new Vector3(0f, -0.08f, -0.01f), new Vector3(0.05f, 0.16f, 0.02f), Mat("Spoke", new Color(0.35f, 0.35f, 0.37f)), false);
            Box("Top marker", spinner, new Vector3(0f, 0.18f, -0.012f), new Vector3(0.04f, 0.03f, 0.02f), Mat("Marker", new Color(0.8f, 0.8f, 0.8f), true), false);

            // Dash turn-signal lamps.
            Material dashLamp = Mat("Dash lamp", new Color(0.05f, 0.15f, 0.07f), true);
            var leftDash = Box("Dash lamp L", t, new Vector3(-0.5f, 1.25f, 0.63f), new Vector3(0.04f, 0.025f, 0.02f), dashLamp, false);
            var rightDash = Box("Dash lamp R", t, new Vector3(-0.24f, 1.25f, 0.63f), new Vector3(0.04f, 0.025f, 0.02f), dashLamp, false);

            // Driver's head + camera (US cars: driver on the left).
            var headPivot = new GameObject("Driver head").transform;
            headPivot.SetParent(t, false);
            headPivot.localPosition = new Vector3(-0.37f, 1.32f, -0.12f);
            var camGo = new GameObject("Driver camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(headPivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1500f;
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();

            // Mirrors: each is a camera rendering into a texture shown (flipped) on a quad.
            Vector3 eye = headPivot.position;
            var rearMirror = Mirror(t, "Rear-view mirror", new Vector3(0f, 1.58f, 0.55f), new Vector2(0.3f, 0.09f), eye,
                new Vector3(0f, 1.55f, -2.4f), Vector3.back, 28f, 512, 160);
            var leftMirror = Mirror(t, "Left mirror", new Vector3(-1.0f, 1.2f, 0.62f), new Vector2(0.2f, 0.13f), eye,
                new Vector3(-1.02f, 1.2f, 0.5f), new Vector3(-0.22f, -0.03f, -1f), 32f, 256, 168);
            var rightMirror = Mirror(t, "Right mirror", new Vector3(1.0f, 1.2f, 0.62f), new Vector2(0.2f, 0.13f), eye,
                new Vector3(1.02f, 1.2f, 0.5f), new Vector3(0.22f, -0.03f, -1f), 32f, 256, 168);

            var look = headPivot.gameObject.AddComponent<DriverHeadLook>();
            look.leftMirror = leftMirror;
            look.rearMirror = rearMirror;
            look.rightMirror = rightMirror;

            // Wheels.
            var controller = root.AddComponent<CarController>();
            controller.bodyCollider = body;
            controller.steeringWheel = spinner;
            Transform vis;
            controller.frontLeft = Wheel(t, "Wheel FL", new Vector3(-0.78f, 0.45f, 1.35f), out vis); controller.frontLeftVisual = vis;
            controller.frontRight = Wheel(t, "Wheel FR", new Vector3(0.78f, 0.45f, 1.35f), out vis); controller.frontRightVisual = vis;
            controller.rearLeft = Wheel(t, "Wheel RL", new Vector3(-0.78f, 0.45f, -1.35f), out vis); controller.rearLeftVisual = vis;
            controller.rearRight = Wheel(t, "Wheel RR", new Vector3(0.78f, 0.45f, -1.35f), out vis); controller.rearRightVisual = vis;

            // Exterior lights.
            Material amber = Mat("Signal lamp", new Color(0.25f, 0.17f, 0.05f), true);
            Material tail = Mat("Tail lamp", new Color(0.35f, 0.02f, 0.02f), true);
            Material lens = Mat("Headlight lens", new Color(0.95f, 0.95f, 0.85f), true);
            var lights = root.AddComponent<CarLights>();
            lights.leftLamps = new[]
            {
                Box("Signal FL", t, new Vector3(-0.82f, 0.85f, 2.21f), new Vector3(0.14f, 0.1f, 0.02f), amber, false).GetComponent<Renderer>(),
                Box("Signal RL", t, new Vector3(-0.82f, 0.95f, -2.21f), new Vector3(0.14f, 0.1f, 0.02f), amber, false).GetComponent<Renderer>(),
                Box("Mirror signal L", t, new Vector3(-1.05f, 1.2f, 0.66f), new Vector3(0.08f, 0.03f, 0.02f), amber, false).GetComponent<Renderer>(),
            };
            lights.rightLamps = new[]
            {
                Box("Signal FR", t, new Vector3(0.82f, 0.85f, 2.21f), new Vector3(0.14f, 0.1f, 0.02f), amber, false).GetComponent<Renderer>(),
                Box("Signal RR", t, new Vector3(0.82f, 0.95f, -2.21f), new Vector3(0.14f, 0.1f, 0.02f), amber, false).GetComponent<Renderer>(),
                Box("Mirror signal R", t, new Vector3(1.05f, 1.2f, 0.66f), new Vector3(0.08f, 0.03f, 0.02f), amber, false).GetComponent<Renderer>(),
            };
            lights.leftDashLamp = leftDash.GetComponent<Renderer>();
            lights.rightDashLamp = rightDash.GetComponent<Renderer>();
            lights.brakeLamps = new[]
            {
                Box("Tail L", t, new Vector3(-0.6f, 0.95f, -2.21f), new Vector3(0.3f, 0.12f, 0.02f), tail, false).GetComponent<Renderer>(),
                Box("Tail R", t, new Vector3(0.6f, 0.95f, -2.21f), new Vector3(0.3f, 0.12f, 0.02f), tail, false).GetComponent<Renderer>(),
            };
            var heads = new List<Light>();
            for (int s = -1; s <= 1; s += 2)
            {
                Box("Headlight", t, new Vector3(s * 0.6f, 0.85f, 2.21f), new Vector3(0.32f, 0.12f, 0.02f), lens, false);
                var hl = new GameObject("Headlight beam");
                hl.transform.SetParent(t, false);
                hl.transform.localPosition = new Vector3(s * 0.6f, 0.85f, 2.3f);
                hl.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
                var l = hl.AddComponent<Light>();
                l.type = LightType.Spot;
                l.range = 45f;
                l.spotAngle = 55f;
                l.intensity = 2.5f;
                l.color = new Color(1f, 0.96f, 0.88f);
                heads.Add(l);
            }
            lights.headlights = heads.ToArray();

            root.AddComponent<EngineAudio>();
            return root;
        }

        static Transform Mirror(Transform car, string name, Vector3 glassPos, Vector2 size, Vector3 eyeWorld,
            Vector3 camPos, Vector3 camDir, float fov, int texW, int texH)
        {
            var rt = new RenderTexture(texW, texH, 16) { name = name };
            string rtPath = GeneratedDir + "/Materials/" + name + ".renderTexture";
            AssetDatabase.CreateAsset(rt, rtPath);

            var camGo = new GameObject(name + " camera");
            camGo.transform.SetParent(car, false);
            camGo.transform.localPosition = camPos;
            camGo.transform.localRotation = Quaternion.LookRotation(camDir);
            var cam = camGo.AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 300f;

            var mat = UnlitTextureMaterial(name, rt);

            var housing = Box(name + " housing", car, glassPos, new Vector3(size.x + 0.04f, size.y + 0.04f, 0.04f), Mat("Car trim", Color.black), false);
            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glass.name = name;
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.transform.SetParent(car, false);
            glass.transform.position = car.TransformPoint(glassPos);
            // A quad's visible side faces -Z, so point +Z away from the driver's eye.
            Vector3 away = car.TransformPoint(glassPos) - eyeWorld;
            glass.transform.rotation = Quaternion.LookRotation(away);
            glass.transform.position -= away.normalized * 0.025f;
            glass.transform.localScale = new Vector3(size.x, size.y, 1f);
            glass.GetComponent<Renderer>().sharedMaterial = mat;
            housing.transform.rotation = glass.transform.rotation;
            return glass.transform;
        }

        static WheelCollider Wheel(Transform car, string name, Vector3 pos, out Transform visual)
        {
            var go = new GameObject(name);
            go.transform.SetParent(car, false);
            go.transform.localPosition = pos;
            var wc = go.AddComponent<WheelCollider>();
            wc.radius = 0.34f;
            wc.mass = 20f;
            wc.suspensionDistance = 0.22f;
            wc.forceAppPointDistance = 0.1f;
            var spring = wc.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4000f;
            spring.targetPosition = 0.5f;
            wc.suspensionSpring = spring;
            var fwd = wc.forwardFriction;
            fwd.stiffness = 1.4f;
            wc.forwardFriction = fwd;
            var side = wc.sidewaysFriction;
            side.stiffness = 1.6f;
            wc.sidewaysFriction = side;

            var holder = new GameObject(name + " visual").transform;
            holder.SetParent(car, false);
            holder.localPosition = pos;
            var tire = Cylinder("Tire", holder, Vector3.zero, new Vector3(0.68f, 0.11f, 0.68f), Mat("Tire", new Color(0.08f, 0.08f, 0.08f)));
            Object.DestroyImmediate(tire.GetComponent<Collider>());
            tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var hub = Cylinder("Hub", holder, Vector3.zero, new Vector3(0.42f, 0.115f, 0.42f), Mat("Hub", new Color(0.6f, 0.6f, 0.62f)));
            Object.DestroyImmediate(hub.GetComponent<Collider>());
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            visual = holder;
            return wc;
        }

        static void BuildSystems(GameObject car)
        {
            var systems = new GameObject("Game Systems");
            systems.AddComponent<RulesEngine>();
            systems.AddComponent<Instructor>();
            systems.AddComponent<DrivingHUD>();
        }

        // ---------------------------------------------------------------- helpers

        static Material Mat(string name, Color color, bool unlit = false)
        {
            string key = name + (unlit ? "_unlit" : "");
            Material m;
            if (materials.TryGetValue(key, out m)) return m;

            m = new Material(FindShader(unlit)) { name = name, color = color };
            AssetDatabase.CreateAsset(m, GeneratedDir + "/Materials/" + Sanitize(key) + ".mat");
            materials[key] = m;
            return m;
        }

        static Material UnlitTextureMaterial(string name, Texture tex)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            Shader s = srp ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Unlit/Texture");
            if (s == null) s = FindShader(true);
            var m = new Material(s) { name = name, mainTexture = tex };
            // Mirror image: flip horizontally.
            m.mainTextureScale = new Vector2(-1f, 1f);
            m.mainTextureOffset = new Vector2(1f, 0f);
            AssetDatabase.CreateAsset(m, GeneratedDir + "/Materials/" + Sanitize(name) + "_mirror.mat");
            return m;
        }

        static Shader FindShader(bool unlit)
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            Shader s = null;
            if (srp) s = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find(unlit ? "Unlit/Color" : "Standard");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            return Setup(go, name, parent, localPos, scale, m, collider);
        }

        static GameObject Cylinder(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            return Setup(go, name, parent, localPos, scale, m, true);
        }

        static GameObject Capsule(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            return Setup(go, name, parent, localPos, scale, m, false);
        }

        static GameObject Sphere(string name, Transform parent, Vector3 localPos, float diameter, Material m, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            return Setup(go, name, parent, localPos, Vector3.one * diameter, m, collider);
        }

        static GameObject Setup(GameObject go, string name, Transform parent, Vector3 localPos, Vector3 scale, Material m, bool collider)
        {
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void Pole(Transform parent, float height)
        {
            Cylinder("Pole", parent, new Vector3(0f, height / 2f, 0f), new Vector3(0.08f, height / 2f, 0.08f), Mat("Metal", new Color(0.55f, 0.56f, 0.58f)));
        }

        static Mesh octagon;

        static Mesh OctagonMesh()
        {
            if (octagon != null) return octagon;
            const float r = 0.38f;
            var verts = new Vector3[9];
            verts[0] = Vector3.zero;
            for (int i = 0; i < 8; i++)
            {
                float a = (22.5f + i * 45f) * Mathf.Deg2Rad;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            var tris = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                int a = i + 1, b = (i + 1) % 8 + 1;
                tris.AddRange(new[] { 0, b, a }); // front (facing -Z, toward the driver)
                tris.AddRange(new[] { 0, a, b }); // back
            }
            octagon = new Mesh { name = "Octagon", vertices = verts, triangles = tris.ToArray() };
            octagon.RecalculateNormals();
            octagon.RecalculateBounds();
            string path = GeneratedDir + "/Octagon.asset";
            AssetDatabase.CreateAsset(octagon, path);
            return octagon;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes) if (s.path == path) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
