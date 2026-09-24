using System.IO;
using ArenaSurvivor.Core.Endless;
using ArenaSurvivor.Unity;
using ArenaSurvivor.Unity.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArenaSurvivor.Editor
{
    /// <summary>
    /// One-click setup of everything the endless mode needs in the project, so the scene does not have to be edited
    /// by hand: the endless settings asset, the pickup materials and prefabs, the new UI (XP bar, level up cards,
    /// endless button, record texts) and all references on the bootstrap and the screens.
    ///
    /// New UI is built by duplicating existing, already styled objects (health bar, texts, buttons), so it keeps
    /// the fonts, sprites and colours of the rest of the UI. Safe to run again: existing objects are found by name
    /// and only their references are refreshed.
    /// </summary>
    public static class EndlessModeSetup
    {
        private const string ScenePath = "Assets/Scenes/Arena.unity";
        private const string SettingsPath = "Assets/Data/Endless/Endless_Default.asset";
        private const string MaterialFolder = "Assets/Materials";
        private const string PrefabFolder = "Assets/Prefabs";

        private static readonly Color ExperienceColor = new Color(0.3f, 0.85f, 1f);
        private static readonly Color HealthColor = new Color(0.9f, 0.15f, 0.15f);
        private static readonly Color CardColor = new Color(0.14f, 0.18f, 0.28f, 0.97f);
        private static readonly Color EndlessButtonColor = new Color(0.72f, 0.33f, 0.1f, 0.95f);
        private static readonly Color CardLevelColor = new Color(1f, 0.82f, 0.25f);

        [MenuItem("Tools/Arena Survivor/Setup Endless Mode")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                scene = EditorSceneManager.OpenScene(ScenePath);
            }

            EndlessSettings settings = CreateSettings();
            Transform experiencePrefab = CreateExperiencePrefab();
            Transform healthPrefab = CreateHealthPrefab();

            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            var hud = Object.FindFirstObjectByType<HudScreen>(FindObjectsInactive.Include);
            var menu = Object.FindFirstObjectByType<MenuScreen>(FindObjectsInactive.Include);
            var result = Object.FindFirstObjectByType<ResultScreen>(FindObjectsInactive.Include);
            if (bootstrap == null || hud == null || menu == null || result == null)
            {
                Debug.LogError("Endless setup: GameBootstrap, HudScreen, MenuScreen or ResultScreen not found in the scene.");
                return;
            }

            SetupHud(hud);
            LevelUpScreen levelUp = SetupLevelUpScreen(hud.transform.parent, result);
            SetupMenu(menu);
            SetupResult(result);

            var so = new SerializedObject(bootstrap);
            Set(so, "endless", settings);
            Set(so, "experiencePickupPrefab", experiencePrefab);
            Set(so, "healthPickupPrefab", healthPrefab);
            Set(so, "levelUpScreen", levelUp);
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Endless setup: done. Settings, pickup prefabs, XP bar, level up screen, endless button and " +
                      "record texts are in place and wired; the scene is saved.");
        }

        // ---------------------------------------------------------------- assets

        private static EndlessSettings CreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<EndlessSettings>(SettingsPath);
            if (settings != null)
            {
                return settings;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            settings = ScriptableObject.CreateInstance<EndlessSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            return settings;
        }

        private static Material CreateMaterial(string name, Color color, float emission)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                // Simple Lit: cheap, but still shaded, so a spinning gem reads as a 3D object.
                material = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            if (emission > 0f)
            {
                // A little glow so pickups stand out on the dark ground.
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform CreateExperiencePrefab()
        {
            Material material = CreateMaterial("M_PickupExperience", ExperienceColor, 0.8f);
            var root = new GameObject("Pickup_Experience");

            // A cube tipped onto a corner reads as a gem; the root spins it around the vertical axis.
            AddPart(root.transform, "Gem", Vector3.zero, new Vector3(45f, 0f, 45f), Vector3.one * 0.28f, material);
            return SavePrefab(root);
        }

        private static Transform CreateHealthPrefab()
        {
            Material box = CreateMaterial("M_PickupHealthBox", new Color(0.95f, 0.95f, 0.95f), 0.2f);
            Material cross = CreateMaterial("M_PickupHealthCross", HealthColor, 0.6f);
            var root = new GameObject("Pickup_Health");

            // A white box with a red cross on top: a first aid kit.
            AddPart(root.transform, "Box", Vector3.zero, Vector3.zero, new Vector3(0.45f, 0.3f, 0.45f), box);
            AddPart(root.transform, "CrossA", new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.32f, 0.04f, 0.1f), cross);
            AddPart(root.transform, "CrossB", new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.1f, 0.04f, 0.32f), cross);
            return SavePrefab(root);
        }

        private static void AddPart(Transform parent, string name, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>()); // No physics in this project.
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;

            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // Many small objects; shadows would cost more than they add.
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Transform SavePrefab(GameObject root)
        {
            string path = $"{PrefabFolder}/{root.name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.transform;
        }

        // ---------------------------------------------------------------- UI

        private static void SetupHud(HudScreen hud)
        {
            Transform healthBar = hud.transform.Find("HealthBar");
            Transform bar = hud.transform.Find("ExperienceBar");
            if (bar == null)
            {
                // Same look as the health bar: dark background, fill stretched over it.
                bar = Duplicate(healthBar, hud.transform, "ExperienceBar");
                Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -135f), new Vector2(800f, 30f));
                bar.Find("Fill").GetComponent<Image>().color = ExperienceColor;

                TMP_Text level = Duplicate(hud.transform.Find("Kills"), bar, "Level").GetComponent<TMP_Text>();
                Stretch(level.rectTransform);
                level.alignment = TextAlignmentOptions.Center;
                level.fontSize = 26f;
                level.text = "LV 1";
                level.raycastTarget = false;
            }

            var so = new SerializedObject(hud);
            Set(so, "experienceBar", bar.gameObject);
            Set(so, "experienceFill", bar.Find("Fill").GetComponent<Image>());
            Set(so, "levelText", bar.Find("Level").GetComponent<TMP_Text>());
            so.ApplyModifiedProperties();
        }

        private static LevelUpScreen SetupLevelUpScreen(Transform canvas, ResultScreen result)
        {
            Transform existing = canvas.Find("LevelUpScreen");
            if (existing != null)
            {
                return existing.GetComponent<LevelUpScreen>();
            }

            var screen = new GameObject("LevelUpScreen", typeof(RectTransform), typeof(Image), typeof(LevelUpScreen));
            screen.transform.SetParent(canvas, false);
            Stretch((RectTransform)screen.transform);
            // Draw order: above the HUD and the damage flash, below the result, benchmark and menu screens.
            screen.transform.SetSiblingIndex(result.transform.GetSiblingIndex());

            // Dims the frozen arena and blocks touches to the joystick behind it.
            var dim = screen.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);
            dim.raycastTarget = true;

            TMP_Text title = Duplicate(result.transform.Find("Title"), screen.transform, "Title").GetComponent<TMP_Text>();
            title.rectTransform.anchoredPosition = new Vector2(0f, 360f);
            title.text = "LEVEL UP!";
            title.color = CardLevelColor;

            TMP_Text hint = Duplicate(result.transform.Find("Survived"), screen.transform, "Hint").GetComponent<TMP_Text>();
            hint.rectTransform.anchoredPosition = new Vector2(0f, 250f);
            hint.text = "Choose one upgrade";
            hint.fontStyle = FontStyles.Normal;

            Transform template = result.transform.Find("ReplayButton");
            var cards = new UpgradeCard[3];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = CreateCard(template, screen.transform, i);
            }

            var so = new SerializedObject(screen.GetComponent<LevelUpScreen>());
            Set(so, "titleText", title);
            SerializedProperty cardArray = so.FindProperty("cards");
            cardArray.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                cardArray.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            }

            so.ApplyModifiedProperties();
            screen.SetActive(false);
            return screen.GetComponent<LevelUpScreen>();
        }

        private static UpgradeCard CreateCard(Transform buttonTemplate, Transform parent, int index)
        {
            Transform card = Duplicate(buttonTemplate, parent, $"Card{index}");
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2((index - 1) * 540f, -110f), new Vector2(480f, 540f));
            card.GetComponent<Image>().color = CardColor;

            var button = card.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); // Drop anything copied from the template.

            TMP_Text title = card.Find("Label").GetComponent<TMP_Text>();
            title.name = "Title";
            Place(title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(-40f, 110f));
            title.fontSize = 48f;
            title.fontStyle = FontStyles.Bold;
            title.textWrappingMode = TextWrappingModes.Normal;
            title.raycastTarget = false;

            TMP_Text description = Duplicate(title.transform, card, "Description").GetComponent<TMP_Text>();
            Place(description.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(-60f, 220f));
            description.fontSize = 38f;
            description.fontStyle = FontStyles.Normal;

            TMP_Text level = Duplicate(title.transform, card, "Level").GetComponent<TMP_Text>();
            Place(level.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(-40f, 70f));
            level.fontSize = 34f;
            level.color = CardLevelColor;

            UpgradeCard component = card.gameObject.AddComponent<UpgradeCard>();
            var so = new SerializedObject(component);
            Set(so, "button", button);
            Set(so, "titleText", title);
            Set(so, "descriptionText", description);
            Set(so, "levelText", level);
            so.ApplyModifiedProperties();
            return component;
        }

        private static void SetupMenu(MenuScreen menu)
        {
            Transform endless = menu.transform.Find("EndlessButton");
            if (endless == null)
            {
                // Difficulty buttons move to the left column; the endless button fills the right column.
                foreach (string name in new[] { "EasyButton", "NormalButton", "HardButton" })
                {
                    var rect = (RectTransform)menu.transform.Find(name);
                    rect.anchoredPosition = new Vector2(-300f, rect.anchoredPosition.y);
                }

                endless = Duplicate(menu.transform.Find("HardButton"), menu.transform, "EndlessButton");
                Place(endless, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(300f, -60f), new Vector2(480f, 430f));
                endless.GetComponent<Image>().color = EndlessButtonColor;
                endless.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();

                TMP_Text label = endless.Find("Label").GetComponent<TMP_Text>();
                Place(label.transform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(0f, 120f));
                label.text = "ENDLESS";
                label.fontSize = 72f;
                label.fontStyle = FontStyles.Bold;

                TMP_Text record = Duplicate(menu.transform.Find("TotalKills"), endless, "EndlessRecord").GetComponent<TMP_Text>();
                Place(record.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(-50f, 130f));
                record.fontSize = 32f;
                record.textWrappingMode = TextWrappingModes.Normal;
                record.text = "No timer. Level up and pick upgrades.";
                record.raycastTarget = false;

                TMP_Text subtitle = menu.transform.Find("Subtitle").GetComponent<TMP_Text>();
                subtitle.text = "Survive for 3 minutes, or as long as you can";
            }

            var so = new SerializedObject(menu);
            Set(so, "endlessButton", endless.GetComponent<Button>());
            Set(so, "endlessRecordText", endless.Find("EndlessRecord").GetComponent<TMP_Text>());
            so.ApplyModifiedProperties();
        }

        private static void SetupResult(ResultScreen result)
        {
            Transform record = result.transform.Find("Record");
            if (record == null)
            {
                record = Duplicate(result.transform.Find("TotalKills"), result.transform, "Record");
                ((RectTransform)record).anchoredPosition = new Vector2(0f, -65f);
                var text = record.GetComponent<TMP_Text>();
                text.fontSize = 42f;
                text.text = "NEW RECORD!";
                record.gameObject.SetActive(false);
            }

            var so = new SerializedObject(result);
            Set(so, "recordText", record.GetComponent<TMP_Text>());
            so.ApplyModifiedProperties();
        }

        // ---------------------------------------------------------------- helpers

        private static Transform Duplicate(Transform source, Transform parent, string name)
        {
            GameObject copy = Object.Instantiate(source.gameObject, parent, false);
            copy.name = name;
            copy.SetActive(true);
            Undo.RegisterCreatedObjectUndo(copy, "Endless setup");
            return copy.transform;
        }

        private static void Place(Transform transform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void Set(SerializedObject so, string property, Object value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"Endless setup: property '{property}' not found on {so.targetObject.GetType().Name}.");
                return;
            }

            p.objectReferenceValue = value;
        }
    }
}
