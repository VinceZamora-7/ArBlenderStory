#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using ARLearning.AR;
using ARLearning.Input;
using ARLearning.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ARLearning.Editor
{
    /// <summary>Creates the deliberately small Phase 1 AR placement setup.</summary>
    public static class ARPhase1Setup
    {
        const string ScenePath = "Assets/AR/ARTest.unity";
        const string ReticlePrefabPath = "Assets/Prefabs/AR/PlacementReticle.prefab";
        const string CubePrefabPath = "Assets/Prefabs/Learning/LearningCube.prefab";
        const string CubeMaterialPath = "Assets/Materials/LearningCubeMaterial.mat";
        const string AppShellPath = "Assets/UI/AppShell.uxml";
        const string AppThemePath = "Assets/UI/AppTheme.uss";
        const string PanelSettingsPath = "Assets/UI/AppPanelSettings.asset";

        [MenuItem("AR Learning/Setup Phase 1")]
        public static void SetupPhase1()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/AR");
            EnsureFolder("Assets/Prefabs/Learning");
            EnsureFolder("Assets/UI");

            var reticle = CreateReticlePrefab();
            var cube = CreateLearningCubePrefab();
            var importedObjects = CreateImportedLearningPrefabs();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ConfigureScene(reticle, cube, importedObjects);
            AddSceneToBuildSettings();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("AR Learning Phase 1 setup complete.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets", Path.GetFileName(path));
        }

        static GameObject CreateReticlePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReticlePrefabPath);
            if (prefab != null) return prefab;

            var root = new GameObject("Placement Reticle", typeof(MeshFilter), typeof(MeshRenderer), typeof(PlacementReticleView));
            prefab = PrefabUtility.SaveAsPrefabAsset(root, ReticlePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject CreateLearningCubePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CubePrefabPath);
            if (prefab != null) return prefab;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Learning Cube";
            cube.transform.localScale = Vector3.one * 0.12f;

            var material = AssetDatabase.LoadAssetAtPath<Material>(CubeMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Learning Cube Material" };
                material.SetColor("_BaseColor", new Color(0.15f, 0.65f, 0.95f, 1f));
                AssetDatabase.CreateAsset(material, CubeMaterialPath);
            }
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            prefab = PrefabUtility.SaveAsPrefabAsset(cube, CubePrefabPath);
            Object.DestroyImmediate(cube);
            return prefab;
        }

        static LearningObjectCatalog.Entry[] CreateImportedLearningPrefabs()
        {
            var definitions = new[]
            {
                ("Bevel", "BevelTool"), ("Extrude", "ExtrudeTool"), ("Inset", "InsetTool"),
                ("Knife", "KnifeTool"), ("Loop Cut", "LoopCutTool"), ("Move", "MoveTool"),
                ("Rotate", "RotateTool"), ("Scale", "ScaleTool"), ("Spin", "SpinTool")
            };
            var entries = new List<LearningObjectCatalog.Entry>();
            foreach (var (name, file) in definitions)
            {
                var prefabPath = $"Assets/Prefabs/Learning/{file}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/LearningObject/{file}.glb");
                    if (source == null) continue;
                    var root = PrefabUtility.InstantiatePrefab(source) as GameObject;
                    root.name = name + " Learning Object";
                    root.transform.localScale = Vector3.one * 0.06f;
                    foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                        if (filter.GetComponent<Collider>() == null && filter.sharedMesh != null)
                            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    Object.DestroyImmediate(root);
                }
                entries.Add(new LearningObjectCatalog.Entry { Name = name, Prefab = prefab });
            }
            return entries.ToArray();
        }

        static void ConfigureScene(GameObject reticle, GameObject cube, LearningObjectCatalog.Entry[] importedObjects)
        {
            var session = FindOrCreate<ARSession>("AR Session");
            // AR is optional: unsupported devices must not be prompted to install
            // Google Play Services for AR during ARSession startup.
            session.attemptUpdate = false;
            var availability = GetOrAdd<ARAvailabilityHandler>(session.gameObject);
            var origin = FindOrCreateXROrigin();

            // These four components deliberately share an object: ARPlacementManager
            // obtains its dependencies through GetComponent in Awake.
            var state = GetOrAdd<ARLearningStateController>(origin);
            GetOrAdd<TouchInputRouter>(origin);
            var planes = GetOrAdd<ARPlaneManager>(origin);
            GetOrAdd<ARRaycastManager>(origin);
            var placement = GetOrAdd<ARPlacementManager>(origin);
            var transforms = GetOrAdd<TransformToolController>(origin);
            var meshTools = GetOrAdd<CubeMeshToolController>(origin);
            var preview = GetOrAdd<PreviewModeController>(origin);
            var catalog = GetOrAdd<LearningObjectCatalog>(origin);
            catalog.Configure(importedObjects);
            EditorUtility.SetDirty(catalog);
            var cameraPreview = GetOrAdd<NonARCameraPreview>(origin);
            cameraPreview.enabled = false;
            GetOrAdd<WorkspaceGridView>(origin);
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;

            var camera = FindOrCreateCamera(origin.transform);
            ConfigureTrackedPoseDriver(camera.gameObject);
            GetOrAdd<ARCameraManager>(camera.gameObject);
            GetOrAdd<ARCameraBackground>(camera.gameObject);

            var uiRoot = GameObject.Find("AR Learning UI Controller") ?? new GameObject("AR Learning UI Controller");
            var ui = GetOrAdd<ARLearningUI>(uiRoot);
            var oldNavigation = GetOrAdd<AppNavigationUI>(uiRoot);
            oldNavigation.enabled = false;
            var document = GetOrAdd<UIDocument>(uiRoot);
            var navigation = GetOrAdd<AppToolkitUI>(uiRoot);
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.name = "App Panel Settings";
                AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
            }
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1080, 1920);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = .5f;
            EditorUtility.SetDirty(panelSettings);
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AppShellPath);
            document.sortingOrder = 100;
            EditorUtility.SetDirty(document);
            SetPrivateField(placement, "m_ReticlePrefab", reticle);
            SetPrivateField(placement, "m_LearningCubePrefab", cube);
            SetPrivateField(placement, "m_ObjectCatalog", catalog);
            SetPrivateField(ui, "m_State", state);
            SetPrivateField(ui, "m_Placement", placement);
            SetPrivateField(ui, "m_Availability", availability);
            SetPrivateField(ui, "m_TransformTools", transforms);
            SetPrivateField(ui, "m_MeshTools", meshTools);
            SetPrivateField(ui, "m_ObjectCatalog", catalog);
            SetPrivateField(navigation, "m_Workspace", ui);
            SetPrivateField(navigation, "m_StyleSheet", AssetDatabase.LoadAssetAtPath<StyleSheet>(AppThemePath));
            SetPrivateField(transforms, "m_Placement", placement);
            SetPrivateField(meshTools, "m_Placement", placement);
            SetPrivateField(meshTools, "m_TransformTools", transforms);
            SetPrivateField(preview, "m_Placement", placement);

            // Start in device-independent preview mode. This prevents ARCore from
            // launching its install/update activity on unsupported phones.
            session.gameObject.SetActive(false);
        }

        static T FindOrCreate<T>(string objectName) where T : Component
        {
            var component = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            return component != null ? component : new GameObject(objectName).AddComponent<T>();
        }

        static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            return gameObject.TryGetComponent<T>(out var component) ? component : gameObject.AddComponent<T>();
        }

        static GameObject FindOrCreateXROrigin()
        {
            var type = System.Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
            if (type == null) throw new System.InvalidOperationException("XROrigin is unavailable.");
            foreach (var component in Object.FindObjectsByType<Component>(FindObjectsInactive.Exclude))
                if (type.IsInstanceOfType(component)) return component.gameObject;
            var origin = new GameObject("XR Origin");
            origin.AddComponent(type);
            return origin;
        }

        static Camera FindOrCreateCamera(Transform origin)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("Main Camera", typeof(Camera));
                cameraObject.tag = "MainCamera";
                camera = cameraObject.GetComponent<Camera>();
            }
            camera.transform.SetParent(origin, false);
            camera.transform.localPosition = Vector3.zero;
            camera.transform.localRotation = Quaternion.identity;
            return camera;
        }

        static void ConfigureTrackedPoseDriver(GameObject cameraObject)
        {
            var driver = GetOrAdd<TrackedPoseDriver>(cameraObject);
            driver.positionInput = new InputActionProperty(new InputAction("Center Eye Position", InputActionType.Value, "<XRHMD>/centerEyePosition"));
            driver.rotationInput = new InputActionProperty(new InputAction("Center Eye Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation"));
            EditorUtility.SetDirty(driver);
        }

        static void SetPrivateField(Object target, string fieldName, Object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null) throw new System.InvalidOperationException($"Missing {fieldName} on {target.GetType().Name}.");
            field.SetValue(target, value);
            EditorUtility.SetDirty(target);
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(scene => scene.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

#endif
