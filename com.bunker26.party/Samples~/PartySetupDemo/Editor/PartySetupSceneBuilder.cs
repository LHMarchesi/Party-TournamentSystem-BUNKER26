#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Bunker26.Party;
using Bunker26.Party.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Bunker26.Party.Samples.Editor
{
    /// <summary>
    /// Generates a serialised example scene and prefabs in the Editor. The resulting runtime UI is
    /// Inspector-driven; no Canvas or panels are constructed while the game is running.
    /// </summary>
    internal static class PartySetupSceneBuilder
    {
        private const string OutputRoot = "Assets/PartySetupDemoGenerated";
        private const string PrefabRoot = OutputRoot + "/Prefabs";
        private const string SettingsRoot = OutputRoot + "/Settings";
        private const string SceneRoot = OutputRoot + "/Scenes";
        private const string ScenePath = SceneRoot + "/PartySetupDemo.unity";
        private const string CatalogPath = SettingsRoot + "/PartyIconCatalog.asset";
        private const string PlayerCardPath = PrefabRoot + "/PartyPlayerCard.prefab";
        private const string ProfileCardPath = PrefabRoot + "/PartyProfileCard.prefab";
        private const string InputBindingRowPath = PrefabRoot + "/PartyInputBindingRow.prefab";
        private const string DemoInputActionsPath = SettingsRoot + "/PartyDemoInputActions.inputactions";
        private const string BindingSettingsPath = SettingsRoot + "/PartyInputBindingSettings.asset";
        private const string GameInputDefinitionPath = SettingsRoot + "/PartyDemoGameInputDefinition.asset";

        private static readonly Color BackgroundColor = new Color32(14, 18, 28, 255);
        private static readonly Color PanelColor = new Color32(26, 32, 46, 255);
        private static readonly Color CardColor = new Color32(35, 43, 60, 255);
        private static readonly Color FieldColor = new Color32(46, 55, 74, 255);
        private static readonly Color AccentColor = new Color32(66, 126, 227, 255);
        private static readonly Color GreenColor = new Color32(43, 147, 104, 255);
        private static readonly Color RedColor = new Color32(174, 66, 76, 255);
        private static readonly Color TextColor = new Color32(239, 243, 250, 255);
        private static readonly Color MutedColor = new Color32(165, 176, 194, 255);
        private static readonly Color DarkTextColor = new Color32(31, 40, 57, 255);

        [MenuItem("Bunker 26/Party/Generate Inspector-Driven Party Demo")]
        private static void GenerateDemo()
        {
            if (AssetDatabase.IsValidFolder(OutputRoot) &&
                !EditorUtility.DisplayDialog("Regenerate Party Demo?",
                    "This recreates the generated demo scene, prefabs and icon catalog under Assets/PartySetupDemoGenerated. Your custom edits to those generated assets may be overwritten.",
                    "Regenerate", "Cancel")) return;

            EnsureFolder("Assets", "PartySetupDemoGenerated");
            EnsureFolder(OutputRoot, "Prefabs");
            EnsureFolder(OutputRoot, "Settings");
            EnsureFolder(OutputRoot, "Scenes");

            PartyIconCatalog catalog = CreateOrUpdateCatalog();
            if (catalog == null) return;
            GameInputDefinition inputDefinition = CreateOrUpdateInputDefinition();
            if (inputDefinition == null) return;
            PartyPlayerCardView playerCardPrefab = CreatePlayerCardPrefab(catalog);
            PartyProfileCardView profileCardPrefab = CreateProfileCardPrefab(catalog);
            PartyInputBindingRowView bindingRowPrefab = CreateInputBindingRowPrefab(catalog);
            CreateScene(catalog, inputDefinition, playerCardPrefab, profileCardPrefab, bindingRowPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Party Demo created", "The scene and prefabs are saved under Assets/PartySetupDemoGenerated. Open Scenes/PartySetupDemo.unity and press Play.", "OK");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static PartyIconCatalog CreateOrUpdateCatalog()
        {
            PartyIconCatalog catalog = AssetDatabase.LoadAssetAtPath<PartyIconCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PartyIconCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.unknownProfile = LoadSprite("avatar_unknown");
            catalog.keyboard = LoadSprite("icon_keyboard");
            catalog.gamepad = LoadSprite("icon_gamepad");
            catalog.leader = LoadSprite("icon_leader");
            catalog.connected = LoadSprite("icon_connected");
            catalog.disconnected = LoadSprite("icon_disconnected");
            catalog.add = LoadSprite("icon_plus");
            catalog.edit = LoadSprite("icon_edit");
            catalog.remove = LoadSprite("icon_remove");
            catalog.close = LoadSprite("icon_close");
            catalog.profiles = LoadSprite("icon_profiles");
            catalog.statistics = LoadSprite("icon_stats");
            catalog.arrowLeft = LoadSprite("icon_arrow_left");
            catalog.arrowRight = LoadSprite("icon_arrow_right");
            catalog.fallbackPanel = LoadSprite("ui_pixel");
            catalog.profileIcons = new List<PartyProfileIconEntry>
            {
                new PartyProfileIconEntry { id = "default", sprite = LoadSprite("avatar_default") },
                new PartyProfileIconEntry { id = "star", sprite = LoadSprite("avatar_star") },
                new PartyProfileIconEntry { id = "robot", sprite = LoadSprite("avatar_robot") },
                new PartyProfileIconEntry { id = "bolt", sprite = LoadSprite("avatar_bolt") },
                new PartyProfileIconEntry { id = "crown", sprite = LoadSprite("avatar_crown") },
                new PartyProfileIconEntry { id = "fox", sprite = LoadSprite("avatar_fox") },
                new PartyProfileIconEntry { id = "skull", sprite = LoadSprite("avatar_skull") }
            };

            if (catalog.unknownProfile == null || catalog.keyboard == null || catalog.gamepad == null || catalog.fallbackPanel == null)
            {
                EditorUtility.DisplayDialog("Party art missing",
                    "Could not locate the sample icon PNGs. Import the Party Setup Demo sample from Package Manager, then run this menu item again.", "OK");
                return null;
            }
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static Sprite LoadSprite(string fileName)
        {
            string[] matches = AssetDatabase.FindAssets(fileName + " t:Texture2D");
            for (int i = 0; i < matches.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(matches[i]);
                if (Path.GetFileNameWithoutExtension(path) != fileName) continue;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.alphaIsTransparency = true;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.SaveAndReimport();
                }
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return null;
        }

        private static GameInputDefinition CreateOrUpdateInputDefinition()
        {
            InputActionAsset actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DemoInputActionsPath);
            if (actionsAsset == null)
            {
                string[] candidates = AssetDatabase.FindAssets("PartyDemoInputActions");
                string sourcePath = null;
                for (int i = 0; i < candidates.Length; i++)
                {
                    string candidate = AssetDatabase.GUIDToAssetPath(candidates[i]);
                    if (candidate != DemoInputActionsPath && candidate.Contains("/Samples/") && candidate.EndsWith("PartyDemoInputActions.inputactions"))
                    { sourcePath = candidate; break; }
                }
                if (string.IsNullOrEmpty(sourcePath) || !AssetDatabase.CopyAsset(sourcePath, DemoInputActionsPath))
                {
                    EditorUtility.DisplayDialog("Input Actions missing",
                        "Could not find PartyDemoInputActions.inputactions. Reimport the Party Setup Demo sample and try again.", "OK");
                    return null;
                }
                AssetDatabase.ImportAsset(DemoInputActionsPath, ImportAssetOptions.ForceSynchronousImport);
                actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(DemoInputActionsPath);
            }

            if (actionsAsset == null)
            {
                EditorUtility.DisplayDialog("Input Actions invalid", "The demo InputActionAsset could not be loaded. Check the Input System import errors.", "OK");
                return null;
            }

            PartyInputBindingSettings settings = AssetDatabase.LoadAssetAtPath<PartyInputBindingSettings>(BindingSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PartyInputBindingSettings>();
                AssetDatabase.CreateAsset(settings, BindingSettingsPath);
            }
            settings.gamepadControlScheme = "Gamepad";
            settings.keyboardSlot1ControlScheme = "KeyboardLeft";
            settings.keyboardSlot2ControlScheme = "KeyboardRight";
            EditorUtility.SetDirty(settings);

            GameInputDefinition definition = AssetDatabase.LoadAssetAtPath<GameInputDefinition>(GameInputDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<GameInputDefinition>();
                AssetDatabase.CreateAsset(definition, GameInputDefinitionPath);
            }
            definition.gameId = "bunker26.party-demo";
            definition.displayName = "Party Demo";
            definition.actionsAsset = actionsAsset;
            definition.bindingSettings = settings;
            definition.allowDuplicateBindings = false;
            definition.RefreshEditableBindingsFromAsset();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static PartyInputBindingRowView CreateInputBindingRowPrefab(PartyIconCatalog icons)
        {
            GameObject root = new GameObject("PartyInputBindingRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            RectTransform rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(660, 58);
            Image background = root.GetComponent<Image>();
            background.sprite = icons.fallbackPanel;
            background.color = CardColor;
            background.raycastTarget = false;
            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredHeight = 58;
            layout.minHeight = 54;
            layout.preferredWidth = 660;

            PartyInputBindingRowView row = root.AddComponent<PartyInputBindingRowView>();
            row.actionNameText = CreateText("Action Name", root.transform, "Acción", 14, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0, 0), new Vector2(0.43f, 1), new Vector2(18, 0), new Vector2(-12, -8));
            row.currentBindingText = CreateText("Current Binding", root.transform, "W", 14, MutedColor, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Vector2(0.43f, 0), new Vector2(0.72f, 1), new Vector2(0, 0), new Vector2(-8, -8));
            row.rebindButton = CreateButton("Rebind Button", root.transform, "Cambiar", icons.edit, AccentColor,
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-76, 0), new Vector2(128, 38), TextColor, 11);
            row.rebindButtonText = row.rebindButton.GetComponentInChildren<Text>(true);

            PrefabUtility.SaveAsPrefabAsset(root, InputBindingRowPath);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<PartyInputBindingRowView>(InputBindingRowPath);
        }

        private static PartyPlayerCardView CreatePlayerCardPrefab(PartyIconCatalog icons)
        {
            GameObject root = new GameObject("PartyPlayerIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Button));
            RectTransform rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(48, 48);
            Image background = root.GetComponent<Image>();
            background.sprite = icons.fallbackPanel;
            background.color = new Color32(29, 35, 49, 230);
            background.raycastTarget = true;

            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 48;
            layout.minWidth = 48;
            layout.preferredHeight = 48;
            layout.minHeight = 48;

            Button button = root.GetComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock buttonColors = button.colors;
            buttonColors.normalColor = Color.white;
            buttonColors.highlightedColor = new Color32(179, 207, 255, 255);
            buttonColors.pressedColor = new Color32(137, 174, 242, 255);
            buttonColors.selectedColor = new Color32(214, 228, 255, 255);
            button.colors = buttonColors;

            PartyPlayerCardView view = root.AddComponent<PartyPlayerCardView>();
            view.selectButton = button;
            view.hoverScale = 1.13f;
            view.selectedScale = 1.07f;
            view.scaleSmoothing = 18f;

            view.profileImage = CreateImage("Profile Icon", root.transform, icons.unknownProfile, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38));
            view.profileImage.preserveAspect = true;
            view.profileImage.raycastTarget = false;

            view.leaderBadgeImage = CreateImage("Leader Badge", root.transform, icons.leader, Color.white,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(3, -3), new Vector2(15, 15));
            view.leaderBadgeImage.preserveAspect = true;
            view.leaderBadgeImage.raycastTarget = false;

            view.inputImage = CreateImage("Input Icon", root.transform, icons.disconnected, Color.white,
                new Vector2(1, 0), new Vector2(1, 0), new Vector2(-1, 1), new Vector2(17, 17));
            view.inputImage.preserveAspect = true;
            view.inputImage.raycastTarget = false;

            view.connectionStatusImage = CreateImage("Connection Status Dot", root.transform, icons.connected, Color.white,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-3, -3), new Vector2(8, 8));
            view.connectionStatusImage.preserveAspect = true;
            view.connectionStatusImage.raycastTarget = false;
            view.connectionStatusImage.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerCardPath);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<PartyPlayerCardView>(PlayerCardPath);
        }

        private static PartyProfileCardView CreateProfileCardPrefab(PartyIconCatalog icons)
        {
            GameObject root = new GameObject("PartyProfileCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            RectTransform rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(680, 120);
            root.GetComponent<Image>().sprite = icons.fallbackPanel;
            root.GetComponent<Image>().color = CardColor;
            root.GetComponent<LayoutElement>().preferredHeight = 120;
            root.GetComponent<LayoutElement>().minHeight = 112;

            PartyProfileCardView view = root.AddComponent<PartyProfileCardView>();
            view.profileImage = CreateImage("Profile Icon", root.transform, icons.unknownProfile, Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(74, 74));
            view.profileImage.preserveAspect = true;
            view.profileNameLabel = CreateText("Profile Name", root.transform, "Profile Name", 19, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, -15), new Vector2(365, 26));
            view.statisticsLabel = CreateText("Statistics", root.transform, "0 torneos · 0 victorias", 12, MutedColor, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, -44), new Vector2(420, 22));
            view.assignmentStateLabel = CreateText("Assignment State", root.transform, "DISPONIBLE", 10, new Color32(113, 207, 164, 255), FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, -78), new Vector2(180, 18));
            view.assignButton = CreateButton("Assign Profile", root.transform, "Usar", icons.add, GreenColor, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-242, 0), new Vector2(82, 38), TextColor, 11);
            view.detailsButton = CreateButton("Profile Details", root.transform, "Datos", icons.statistics, new Color32(62, 75, 98, 255), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-154, 0), new Vector2(82, 38), TextColor, 11);
            view.editButton = CreateButton("Edit Profile", root.transform, string.Empty, icons.edit, AccentColor, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-58, 0), new Vector2(42, 38), TextColor, 11);

            PrefabUtility.SaveAsPrefabAsset(root, ProfileCardPath);
            UnityEngine.Object.DestroyImmediate(root);
            return AssetDatabase.LoadAssetAtPath<PartyProfileCardView>(ProfileCardPath);
        }

        private static void CreateScene(PartyIconCatalog catalog, GameInputDefinition inputDefinition,
            PartyPlayerCardView playerPrefab, PartyProfileCardView profilePrefab, PartyInputBindingRowView bindingRowPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            EventSystem eventSystem = eventSystemObject.GetComponent<EventSystem>();
            eventSystem.sendNavigationEvents = true;

            GameObject controllerObject = new GameObject("Party Setup Controller", typeof(PartySetupController), typeof(InputSystemDeviceMonitor));
            PartySetupController controller = controllerObject.GetComponent<PartySetupController>();
            controller.iconCatalog = catalog;
            controller.gameInputDefinition = inputDefinition;
            controller.inputDeviceMonitor = controllerObject.GetComponent<InputSystemDeviceMonitor>();
            controller.maxLocalPlayers = 8;
            controller.createLocalPlayerOneOnAwake = true;
            controller.autoJoinUnassignedGamepads = true;

            GameObject canvasObject = new GameObject("Party Setup Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PartySetupScreenView));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<GraphicRaycaster>().ignoreReversedGraphics = true;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;
            Image background = canvasObject.AddComponent<Image>();
            background.sprite = catalog.fallbackPanel;
            background.color = new Color(BackgroundColor.r, BackgroundColor.g, BackgroundColor.b, 0f);
            background.raycastTarget = false;

            PartySetupScreenView view = canvasObject.GetComponent<PartySetupScreenView>();
            BuildMainScreen(canvasObject.transform, view, catalog, playerPrefab, profilePrefab);
            BuildPlayerContextPanel(canvasObject.transform, view, catalog);
            BuildProfileLibrary(canvasObject.transform, view, catalog, profilePrefab);
            BuildProfileEditor(canvasObject.transform, view, catalog);
            BuildProfileDetails(canvasObject.transform, view, catalog);
            BuildInputSelector(canvasObject.transform, view, catalog);
            BuildInputBindingsEditor(canvasObject.transform, view, catalog, bindingRowPrefab);
            controller.screenView = view;
            // Keep the EventSystem unselected on startup. Otherwise the first button press on an
            // unassigned gamepad can both submit the selected UI button and join the Party.
            eventSystem.firstSelectedGameObject = null;

            Selection.activeGameObject = controllerObject;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        private static void BuildMainScreen(Transform canvas, PartySetupScreenView view, PartyIconCatalog icons, PartyPlayerCardView playerPrefab, PartyProfileCardView profilePrefab)
        {
            // Minimal HUD: only active player icons and a plus button occupy the top-right corner.
            RectTransform playerContent = Rect("Party Player Icons", canvas,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-76, -16), new Vector2(470, 58));
            HorizontalLayoutGroup layout = playerContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            view.playerCardsRoot = playerContent;
            view.playerCardPrefab = playerPrefab;

            view.addLocalPlayerButton = CreateButton("Add Local Player", canvas, string.Empty, icons.add,
                new Color32(45, 55, 74, 235), new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-25, -40), new Vector2(40, 40), TextColor, 12);
            view.addLocalPlayerButton.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);

            // Global profile library is reached contextually from a player icon, keeping the default HUD uncluttered.
            view.openProfileLibraryButton = null;
            view.profileLibraryHeaderIcon = null;
            view.partySummaryText = null;

            view.connectionHintText = CreateText("Join Hint", canvas,
                "Conectá un mando y presioná cualquier botón para unirte",
                11, new Color32(188, 198, 215, 230), FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -78), new Vector2(470, 22));
            view.statusText = CreateText("Status Toast Text", canvas, string.Empty, 11,
                new Color32(211, 223, 243, 235), FontStyle.Normal, TextAnchor.MiddleRight,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -101), new Vector2(470, 24));

            // The view uses a short join hint on the main HUD; full device details remain in the input panel.
        }

        private static void BuildPlayerContextPanel(Transform canvas, PartySetupScreenView view, PartyIconCatalog icons)
        {
            RectTransform panel = CreateOverlayPanel("Player Context Panel", canvas, icons, new Vector2(270, 350));
            view.playerContextPanel = panel.gameObject;
            view.playerContextPanelRect = panel;

            CreateText("Context Heading", panel, "JUGADOR", 10, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -14), new Vector2(-60, 18));
            Button close = CreateButton("Close Player Context", panel, string.Empty, icons.close, RedColor,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -12), new Vector2(25, 25), TextColor, 10);
            close.GetComponent<RectTransform>().pivot = new Vector2(1, 1);
            view.closePlayerContextButton = close;

            view.contextProfileIcon = CreateImage("Context Profile Icon", panel, icons.unknownProfile, Color.white,
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(39, -57), new Vector2(48, 48));
            view.contextProfileIcon.preserveAspect = true;
            view.contextPlayerNameText = CreateText("Context Player Name", panel, "Unknown", 16, TextColor, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(78, -43), new Vector2(-104, 24));
            view.contextPlayerInputText = CreateText("Context Player Input", panel, "Sin controles asignados", 10, MutedColor, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(78, -68), new Vector2(-104, 20));

            view.contextEditProfileButton = CreateButton("Context Edit Profile", panel, "Editar perfil", icons.edit, AccentColor,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -110), new Vector2(224, 36), TextColor, 11);
            view.contextChangeProfileButton = CreateButton("Context Change Profile", panel, "Cambiar perfil", icons.profiles, new Color32(59, 71, 94, 255),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -153), new Vector2(224, 36), TextColor, 11);
            view.contextInputButton = CreateButton("Context Input", panel, "Asignar controles", icons.gamepad, new Color32(59, 71, 94, 255),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(224, 36), TextColor, 11);
            view.contextStatisticsButton = CreateButton("Context Statistics", panel, "Estadísticas", icons.statistics, new Color32(59, 71, 94, 255),
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -239), new Vector2(224, 36), TextColor, 11);
            view.contextMakeLeaderButton = CreateButton("Context Make Leader", panel, "Hacer líder", icons.leader, GreenColor,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -282), new Vector2(224, 36), TextColor, 11);
        }

        private static void BuildProfileLibrary(Transform canvas, PartySetupScreenView view, PartyIconCatalog icons, PartyProfileCardView profilePrefab)
        {
            RectTransform panel = CreateOverlayPanel("Profile Library Panel", canvas, icons, new Vector2(850, 700));
            view.profileLibraryPanel = panel.gameObject;
            CreateText("Profile Library Heading", panel, "PERFILES GUARDADOS", 23, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -24), new Vector2(-92, 38));
            Button close = CreateButton("Close Profile Library", panel, string.Empty, icons.close, RedColor, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -28), new Vector2(40, 40), TextColor, 12);
            view.closeProfileLibraryButton = close;
            view.profileLibrarySummaryText = CreateText("Profile Library Summary", panel, "Perfiles disponibles", 12, MutedColor, FontStyle.Normal, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -68), new Vector2(-52, 22));
            ScrollRect scroll = CreateScrollRect("Saved Profiles Scroll", panel, new Vector2(0, 0.12f), new Vector2(1, 0.83f), icons, out RectTransform content);
            VerticalLayoutGroup vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8; vlg.padding = new RectOffset(0, 8, 4, 8); vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            view.profileCardsRoot = content;
            view.profileCardPrefab = profilePrefab;
            view.createProfileButton = CreateButton("Create Profile", panel, "Crear perfil", icons.add, GreenColor, new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 22), new Vector2(210, 48), TextColor, 13);
        }

        private static void BuildProfileEditor(Transform canvas, PartySetupScreenView view, PartyIconCatalog icons)
        {
            RectTransform panel = CreateOverlayPanel("Profile Editor Panel", canvas, icons, new Vector2(530, 470));
            view.profileEditorPanel = panel.gameObject;
            view.profileEditorTitleText = CreateText("Profile Editor Title", panel, "Crear perfil", 23, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -25), new Vector2(-52, 36));
            view.profileIconPreview = CreateImage("Profile Icon Preview", panel, icons.unknownProfile, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(104, 104));
            view.profileIconPreview.preserveAspect = true;
            view.previousProfileIconButton = CreateButton("Previous Profile Icon", panel, string.Empty, icons.arrowLeft, new Color32(61, 74, 98, 255), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-83, -132), new Vector2(42, 38), TextColor, 12);
            view.nextProfileIconButton = CreateButton("Next Profile Icon", panel, string.Empty, icons.arrowRight, new Color32(61, 74, 98, 255), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(83, -132), new Vector2(42, 38), TextColor, 12);
            CreateText("Profile Name Label", panel, "Nombre del perfil", 12, MutedColor, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -196), new Vector2(-52, 24));
            view.profileNameInput = CreateInputField("Profile Name Input", panel, icons, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -230), new Vector2(-52, 48));
            view.saveProfileButton = CreateButton("Save Profile", panel, "Guardar", icons.add, GreenColor, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-136, 24), new Vector2(110, 45), TextColor, 13);
            view.cancelProfileButton = CreateButton("Cancel Profile Edit", panel, "Cancelar", icons.close, new Color32(61, 74, 98, 255), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-258, 24), new Vector2(110, 45), TextColor, 12);
        }

        private static void BuildProfileDetails(Transform canvas, PartySetupScreenView view, PartyIconCatalog icons)
        {
            RectTransform panel = CreateOverlayPanel("Profile Details Panel", canvas, icons, new Vector2(520, 560));
            view.profileDetailsPanel = panel.gameObject;
            CreateText("Profile Details Heading", panel, "PERFIL", 12, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(25, -20), new Vector2(-50, 20));
            view.detailsProfileIcon = CreateImage("Details Profile Icon", panel, icons.unknownProfile, Color.white, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -65), new Vector2(96, 96));
            view.detailsProfileIcon.preserveAspect = true;
            view.detailsProfileNameText = CreateText("Details Profile Name", panel, "Profile Name", 24, TextColor, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -166), new Vector2(-50, 35));
            view.detailsProfileStatsText = CreateText("Details Profile Statistics", panel, "Torneos jugados: 0", 14, MutedColor, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(42, -218), new Vector2(-84, 185));
            view.detailsAssignButton = CreateButton("Assign Profile From Details", panel, "Asignar", icons.add, GreenColor, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-136, 23), new Vector2(110, 45), TextColor, 12);
            view.detailsEditButton = CreateButton("Edit Profile From Details", panel, "Editar", icons.edit, AccentColor, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-258, 23), new Vector2(110, 45), TextColor, 12);
            view.closeProfileDetailsButton = CreateButton("Close Profile Details", panel, "Volver", icons.close, new Color32(61, 74, 98, 255), new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 23), new Vector2(100, 45), TextColor, 12);
        }

        private static void BuildInputSelector(Transform canvas, PartySetupScreenView view, PartyIconCatalog icons)
        {
            RectTransform panel = CreateOverlayPanel("Input Selector Panel", canvas, icons, new Vector2(590, 570));
            view.inputSelectorPanel = panel.gameObject;
            CreateText("Input Selector Heading", panel, "ASIGNAR CONTROLES", 22, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -25), new Vector2(-52, 36));
            view.inputTargetText = CreateText("Input Target", panel, "Elegí un slot o presioná un botón del mando", 13, MutedColor, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -72), new Vector2(-52, 58));
            CreateText("Keyboard Slots Label", panel, "TECLADO (DOS SLOTS LÓGICOS)", 11, AccentColor, FontStyle.Bold, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -142), new Vector2(-52, 22));
            view.keyboardSlot1Button = CreateButton("Assign Keyboard Slot 1", panel, "Teclado 1", icons.keyboard, AccentColor, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -178), new Vector2(-52, 54), TextColor, 13);
            view.keyboardSlot1Button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -178);
            view.keyboardSlot2Button = CreateButton("Assign Keyboard Slot 2", panel, "Teclado 2", icons.keyboard, AccentColor, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -244), new Vector2(-52, 54), TextColor, 13);
            view.keyboardSlot2Button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -244);
            Image gamepad = CreateImage("Input Gamepad Prompt Icon", panel, icons.gamepad, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -326), new Vector2(55, 55));
            gamepad.preserveAspect = true;
            view.inputDeviceSummaryText = CreateText("Input Device Summary", panel, "Conectá un mando y presioná un botón para asignarlo.", 12, MutedColor, FontStyle.Normal, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(96, -326), new Vector2(-126, 76));
            view.customizeInputBindingsButton = CreateButton("Customize Input Bindings", panel, "Personalizar controles", icons.edit, new Color32(59, 71, 94, 255), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -414), new Vector2(-52, 44), TextColor, 12);
            view.releaseInputButton = CreateButton("Release Input", panel, "Liberar", icons.disconnected, RedColor, new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 24), new Vector2(125, 44), TextColor, 12);
            view.closeInputSelectorButton = CreateButton("Close Input Selector", panel, "Volver", icons.close, new Color32(61, 74, 98, 255), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-26, 24), new Vector2(125, 44), TextColor, 12);
        }

        private static void BuildInputBindingsEditor(Transform canvas, PartySetupScreenView view, PartyIconCatalog icons, PartyInputBindingRowView rowPrefab)
        {
            RectTransform panel = CreateOverlayPanel("Input Bindings Editor Panel", canvas, icons, new Vector2(760, 720));
            view.inputBindingsPanel = panel.gameObject;
            view.inputBindingRowPrefab = rowPrefab;
            view.inputBindingsHeadingText = CreateText("Input Bindings Heading", panel, "PERSONALIZAR CONTROLES", 21, TextColor, FontStyle.Bold,
                TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -23), new Vector2(-92, 35));
            Button close = CreateButton("Close Input Bindings", panel, string.Empty, icons.close, RedColor,
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -24), new Vector2(38, 38), TextColor, 11);
            view.closeInputBindingsButton = close;
            CreateText("Input Bindings Description", panel,
                "Seleccioná Cambiar y presioná la tecla o botón deseado. Escape cancela.", 12, MutedColor, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(26, -63), new Vector2(-52, 28));
            ScrollRect scroll = CreateScrollRect("Input Bindings Scroll", panel, new Vector2(0, 0.13f), new Vector2(1, 0.84f), icons, out RectTransform content);
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 7;
            layout.padding = new RectOffset(7, 7, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            view.inputBindingRowsRoot = content;
            view.inputBindingsStatusText = CreateText("Input Bindings Status", panel, string.Empty, 11, MutedColor, FontStyle.Normal,
                TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 0), new Vector2(26, 67), new Vector2(-52, 22));
            view.resetInputBindingsButton = CreateButton("Reset Input Bindings", panel, "Restaurar predeterminados", icons.arrowLeft,
                new Color32(59, 71, 94, 255), new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 22), new Vector2(235, 40), TextColor, 11);
            view.closeInputBindingsButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-26, 22);
            view.closeInputBindingsButton.GetComponent<RectTransform>().sizeDelta = new Vector2(125, 40);
            view.closeInputBindingsButton.GetComponentInChildren<Text>(true).text = "Volver";
        }

        private static RectTransform CreateOverlayPanel(string name, Transform parent, PartyIconCatalog icons, Vector2 size)
        {
            RectTransform panel = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            Image image = panel.gameObject.AddComponent<Image>(); image.sprite = icons.fallbackPanel; image.color = PanelColor;
            panel.gameObject.AddComponent<CanvasGroup>();
            return panel;
        }

        private static ScrollRect CreateScrollRect(string name, Transform parent, Vector2 min, Vector2 max, PartyIconCatalog icons, out RectTransform content)
        {
            RectTransform root = Rect(name, parent, min, max, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image rootImage = root.gameObject.AddComponent<Image>(); rootImage.sprite = icons.fallbackPanel; rootImage.color = new Color32(19, 25, 37, 180);
            ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            RectTransform viewport = Rect("Viewport", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image viewportImage = viewport.gameObject.AddComponent<Image>(); viewportImage.sprite = icons.fallbackPanel; viewportImage.color = new Color(1, 1, 1, 0.01f);
            Mask mask = viewport.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            content = Rect("Content", viewport, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            scroll.viewport = viewport; scroll.content = content;
            return scroll;
        }

        private static InputField CreateInputField(string name, Transform parent, PartyIconCatalog icons, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, min, max, new Vector2(0.5f, 1), position, size);
            Image bg = rect.gameObject.AddComponent<Image>(); bg.sprite = icons.fallbackPanel; bg.color = FieldColor;
            InputField input = rect.gameObject.AddComponent<InputField>(); input.targetGraphic = bg; input.lineType = InputField.LineType.SingleLine; input.characterLimit = 24;
            Text text = CreateText("Text", rect, string.Empty, 17, TextColor, FontStyle.Normal, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-28, 0));
            Text placeholder = CreateText("Placeholder", rect, "Nombre del perfil...", 16, new Color32(139, 150, 170, 255), FontStyle.Italic, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-28, 0));
            input.textComponent = text; input.placeholder = placeholder;
            return input;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), position, size);
            Image image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.color = color; image.preserveAspect = true;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int fontSize, Color color, FontStyle style,
            TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = fontSize; text.color = color; text.fontStyle = style; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Sprite icon, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color textColor, int fontSize)
        {
            RectTransform rect = Rect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f), position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<PartyIconCatalog>(CatalogPath) != null
                ? AssetDatabase.LoadAssetAtPath<PartyIconCatalog>(CatalogPath).fallbackPanel : null;
            image.color = color;
            Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors; colors.highlightedColor = new Color(1, 1, 1, 0.15f); colors.pressedColor = new Color(0.8f, 0.85f, 1, 0.35f); button.colors = colors;

            if (icon != null)
            {
                Image iconImage = CreateImage("Icon", rect, icon, Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(23, 23));
                iconImage.raycastTarget = false;
            }
            float left = icon != null ? 40f : 8f;
            Text text = CreateText("Label", rect, label, fontSize, textColor, FontStyle.Bold, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(left / 2f, 0), new Vector2(-left - 8, 0));
            text.raycastTarget = false;
            return button;
        }
    }
}
#endif
