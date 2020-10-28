using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.IO;
using System.Collections.Generic;

public class JackdawRequestNewCommandEditorWindow : EditorWindow
{
    [MenuItem("Window/UIElements/JackdawRequestNewCommandEditorWindow")]
    public static void IssueNewCommandWindow(JackdawMainWindow parentWindow)
    {
        JackdawRequestNewCommandEditorWindow wnd = GetWindow<JackdawRequestNewCommandEditorWindow>();
        wnd.titleContent = new GUIContent("JackdawRequestNewCommandEditorWindow");
        wnd.jackdawMainWindow = parentWindow;
    }

    private static string[] panelParametersList = new string[]
    {
        "testParametersPanel",
        "buildParametersPanel",
        "publishParametersPanel",
        "vcsParametersPanel",
    };

    private JackdawMainWindow jackdawMainWindow;
    private Jackdaw.RequestCommand m_Command;

    private Button addToQueueButton;

    public void OnEnable()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        m_Command = new Jackdaw.RequestCommand(){
            type = Jackdaw.RequestType.Build,
            platforms = Jackdaw.RequestPlatform.x64 | Jackdaw.RequestPlatform.Windows,
            testType = Jackdaw.TestType.Standalone | Jackdaw.TestType.PlayTest
        };

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Jackdaw/Editor/JackdawRequestNewCommandEditorWindow.uxml");
        VisualElement labelFromUXML = visualTree.CloneTree();
        root.Add(labelFromUXML);

        InitBuildParameters();
        InitPublishParameters();

        HideAllParametersPanel();
        rootVisualElement.Q<VisualElement>("buildParametersPanel").style.display = DisplayStyle.Flex;

        root.Q<EnumField>("RequestTypeSelector").Init(m_Command.type);
        root.Q<EnumField>("RequestTypeSelector").RegisterValueChangedCallback(OnRequestTypeChanged);

        addToQueueButton = root.Q<Button>("addToQueueButton");
        addToQueueButton.clicked += OnAddToQueue;
    }

    private void OnRequestTypeChanged(ChangeEvent<Enum> evt)
    {
        m_Command.type = (Jackdaw.RequestType) rootVisualElement.Q<EnumField>("RequestTypeSelector").value;

        HideAllParametersPanel();
        addToQueueButton.style.display = DisplayStyle.Flex;

        switch (m_Command.type)
        {
            case Jackdaw.RequestType.UpdateVCS:
            case Jackdaw.RequestType.RequestVCS:
                rootVisualElement.Q<VisualElement>("vcsParametersPanel").style.display = DisplayStyle.Flex;
                break;
            case Jackdaw.RequestType.Test:
                rootVisualElement.Q<VisualElement>("testParametersPanel").style.display = DisplayStyle.Flex;
                break;
            case Jackdaw.RequestType.Build:
                rootVisualElement.Q<VisualElement>("buildParametersPanel").style.display = DisplayStyle.Flex;
                addToQueueButton.style.display = DisplayStyle.None;
                break;
            case Jackdaw.RequestType.Publish:
                rootVisualElement.Q<VisualElement>("publishParametersPanel").style.display = DisplayStyle.Flex;
                break;
        }
    }

    private void InitBuildParameters()
    {
        settingsList = new Dictionary<string, BatchBuilderSettings>();
        filename = new List<string>();

        string[] guids = AssetDatabase.FindAssets("t:BatchBuilderSettings");
        if (guids.Length > 0)
        {

            foreach (var item in guids)
            {
                string textsGUID = item;
                string textsPath = AssetDatabase.GUIDToAssetPath(textsGUID);

                var settings = AssetDatabase.LoadAssetAtPath<BatchBuilderSettings>(textsPath);
                settingsList.Add(Path.GetFileNameWithoutExtension(textsPath), settings);
                filename.Add(Path.GetFileNameWithoutExtension(textsPath));
                selectedSettings = null;
            }
        }
        else
        {
            selectedSettings = CreateInstance<BatchBuilderSettings>();
            AssetDatabase.CreateAsset(selectedSettings, "Assets/Build.asset");
        }

        (buildIMGUIcontainer = rootVisualElement.Q<IMGUIContainer>("buildIMGUI")).onGUIHandler = () => OnBuildGUI();
    }

    private void InitPublishParameters()
    {
        Array values = typeof(Jackdaw.PublishPatform).GetEnumValues();

        foreach (var item in values)
        {
            Toggle toggle = new Toggle(item.ToString())
            {
                value = (m_Command.publishPatform & (Jackdaw.PublishPatform)item) == (Jackdaw.PublishPatform)item,
                name = item.ToString() + "PublishToggle"
            };
            toggle.RegisterValueChangedCallback(OnPublishParametersChanged);
            rootVisualElement.Q<VisualElement>("publishParametersPanel").Add(toggle);
        }
    }

    private void OnPublishParametersChanged(ChangeEvent<bool> evt)
    {
        Array values = typeof(Jackdaw.PublishPatform).GetEnumValues();

        foreach (var item in values)
        {
            string name = item.ToString() + "PublishToggle";
            if(rootVisualElement.Q<Toggle>(name).value)
            {
                m_Command.publishPatform |= (Jackdaw.PublishPatform)item;
            }
            else
            {
                m_Command.publishPatform &= ~(Jackdaw.PublishPatform)item;
            }
        }

        Debug.Log(m_Command.publishPatform);
    }

    private void HideAllParametersPanel()
    {
        foreach (var item in panelParametersList)
        {
            rootVisualElement.Q<VisualElement>(item).style.display = DisplayStyle.None;
        }
    }

    private void OnAddToQueue()
    {
        //jackdawMainWindow.AddCommand(m_Command);
        Close();
    }

#region BUILD IMGUI

    IMGUIContainer buildIMGUIcontainer;

    Vector2 scrollTargets;
    Vector2 scrollSymbols;

    enum Step
    {
        SetSymbols,
        Platform,
        Compilation,
        DataCopy,
        AssetBundles,
        Build,
        Zip
    }

    BatchBuilderSettings selectedSettings;
    Dictionary<string, BatchBuilderSettings> settingsList;
    List<string> filename;

    bool PlatformHasFilename(BuildTarget target)
    {
        return target != BuildTarget.XboxOne
            && target != BuildTarget.PS4;
    }

    static void Swap<T>(ref T lhs, ref T rhs)
    {
        T tmp = lhs;
        lhs = rhs;
        rhs = tmp;
    }

    Editor editor;
    int _choiceIndex = -1;
    public void OnBuildGUI()
    {
        if (settingsList.Count > 0)
        {
            EditorGUILayout.BeginHorizontal();
            {
                var newchoiceIndex = EditorGUILayout.Popup("Presets",_choiceIndex, filename.ToArray());
                {
                    newchoiceIndex = _choiceIndex == -1 ? 0 : newchoiceIndex;

                    if(newchoiceIndex != _choiceIndex)
                    {
                        if(selectedSettings != null)
                        {
                            foreach (BatchBuilderSettings.Settings s in selectedSettings.Builds)
                                s.Editing.valueChanged.RemoveListener(Repaint);
                        }

                        _choiceIndex = newchoiceIndex;
                        selectedSettings = settingsList[filename[_choiceIndex]];

                        foreach (BatchBuilderSettings.Settings s in selectedSettings.Builds)
                            s.Editing.valueChanged.AddListener(Repaint);
                    }
                }

                if(GUILayout.Button("Create new"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Save Your BuildSettings", "buildSettings.asset", "asset", "Please select file name to save setting asset to:");
                    if (!string.IsNullOrEmpty(path)) {
                        var newSettings = CreateInstance<BatchBuilderSettings>();
                        AssetDatabase.CreateAsset(newSettings, path);
                        settingsList.Add(Path.GetFileNameWithoutExtension(path), newSettings);
                        filename.Add(Path.GetFileNameWithoutExtension(path));

                        _choiceIndex = filename.Count-1;

                        selectedSettings = newSettings;
                    }
                }

            }
            EditorGUILayout.EndHorizontal();
        }

        if (selectedSettings != null)
        {
            Editor.DrawFoldoutInspector(selectedSettings, ref editor);

            GUI.enabled = true;

#if HAS_PS4
        GUI.enabled &= settings.Platform != null;
#endif

            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Label("Targets", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Add"))
                {
                    ArrayUtility.Insert(ref selectedSettings.Builds, selectedSettings.Builds.Length,
                        new BatchBuilderSettings.Settings()
                        {
                            Name = "New Build",
                            Path = "Builds/New",
                            Filename = PlayerSettings.productName + ".exe",
                            Target = BuildTarget.StandaloneWindows64,
                            Group = BuildTargetGroup.Standalone,
                            symbols = new BatchBuilderSettings.Symbol[0],
                            Active = false,

                            Editing = new UnityEditor.AnimatedValues.AnimBool(true),
                        });
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            {
                Undo.RecordObject(selectedSettings, "Edit Batch Build Settings");
                ref BatchBuilderSettings.Settings[] builds = ref selectedSettings.Builds;
                EditArray arrayEdit = EditArray.NoChange;
                int arrayEditIndex = -1;
                scrollTargets = EditorGUILayout.BeginScrollView(scrollTargets);
                {
                    for (int i = 0; i < builds.Length; i++)
                    {
                        EditArray r = BuildTitle(builds, i);
                        if (r != EditArray.NoChange)
                        {
                            arrayEdit = r;
                            arrayEditIndex = i;
                        }
                        if (builds[i].Editing.target)
                            i = BuildSettings(builds, i);
                    }
                }
                EditorGUILayout.EndScrollView();

                switch (arrayEdit)
                {
                    case EditArray.Delete:
                        {
                            ArrayUtility.RemoveAt(ref builds, arrayEditIndex);
                            GUI.FocusControl(string.Empty);
                            break;
                        }

                    case EditArray.Up:
                        {
                            Swap(ref builds[arrayEditIndex], ref builds[arrayEditIndex - 1]);
                            GUI.FocusControl(string.Empty);
                            break;
                        }

                    case EditArray.Down:
                        {
                            Swap(ref builds[arrayEditIndex], ref builds[arrayEditIndex + 1]);
                            GUI.FocusControl(string.Empty);
                            break;
                        }
                }

                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.Label("General Symbols", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Add"))
                        ArrayUtility.Insert(ref selectedSettings.GeneralSymbols, selectedSettings.GeneralSymbols.Length, new BatchBuilderSettings.Symbol());
                }
                EditorGUILayout.EndHorizontal();

                scrollSymbols = EditorGUILayout.BeginScrollView(scrollSymbols);
                {
                    for (int i = 0; i < selectedSettings.GeneralSymbols.Length; i++)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            selectedSettings.GeneralSymbols[i].Active = EditorGUILayout.Toggle(selectedSettings.GeneralSymbols[i].Active, GUILayout.Width(15));
                            selectedSettings.GeneralSymbols[i].Name = GUILayout.TextField(selectedSettings.GeneralSymbols[i].Name);
                            if (GUILayout.Button("Delete", GUILayout.Width(60)))
                            {
                                ArrayUtility.RemoveAt(ref selectedSettings.GeneralSymbols, i);
                                i--;
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndScrollView();


                EditorGUILayout.Space();
            }
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(selectedSettings);

            if (GUILayout.Button("Add build to command"))
            {
                string jsonString = JsonUtility.ToJson(selectedSettings);

                jackdawMainWindow.AddCommand(new Jackdaw.RequestData(){
                    commands = Jackdaw.RequestType.Build,
                    parameters = jsonString
                });
                //File.WriteAllText("Temp/selectedSettings.json", jsonString);
                Close();
            }

        }

    }

    enum EditArray { NoChange, Delete, Up, Down };
    private EditArray BuildTitle(BatchBuilderSettings.Settings[] builds, int i)
    {
        EditArray result = EditArray.NoChange;
        GUILayout.BeginHorizontal();
        {
            builds[i].Active = GUILayout.Toggle(builds[i].Active, GUIContent.none, EditorStyles.toggle);
            builds[i].Editing.target = GUILayout.Toggle(builds[i].Editing.target, GUIContent.none, EditorStyles.foldout);
            if (builds[i].Editing.target)
                builds[i].Name = EditorGUILayout.TextField(builds[i].Name);
            else
                GUILayout.Label(builds[i].Name);

            bool wasEditing = builds[i].Editing.target;
            if (wasEditing && !builds[i].Editing.target)
                GUI.FocusControl(string.Empty);

            GUILayout.FlexibleSpace();

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && i > 0;
            if (GUILayout.Button("▲"))
                result = EditArray.Up;
            GUI.enabled = enabled && i < builds.Length - 1;
            if (GUILayout.Button("▼"))
                result = EditArray.Down;
            GUI.enabled = enabled;
            if (GUILayout.Button("✖"))
                result = EditArray.Delete;
        }
        GUILayout.EndHorizontal();
        return result;
    }

    private int BuildSettings(BatchBuilderSettings.Settings[] builds, int i)
    {
        EditorGUILayout.BeginFadeGroup(builds[i].Editing.faded);
        {
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Space(25);
                EditorGUILayout.BeginVertical();
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        builds[i].Path = EditorGUILayout.TextField("Output Directory", builds[i].Path);
                        if (GUILayout.Button("Browse"))
                        {
                            string newPath = EditorUtility.OpenFolderPanel($"Build destination folder for {builds[i].Name}", builds[i].Path, "");
                            builds[i].Path = string.IsNullOrEmpty(newPath) ? builds[i].Path : newPath;
                        }
                    }
                    EditorGUILayout.EndHorizontal();

                    if (PlatformHasFilename(builds[i].Target))
                        builds[i].Filename = EditorGUILayout.TextField("Filename", builds[i].Filename);
                    EditorGUILayout.BeginHorizontal();
                    {
                        builds[i].Group = (BuildTargetGroup)EditorGUILayout.EnumPopup("Target", builds[i].Group);
                        builds[i].Target = (BuildTarget)EditorGUILayout.EnumPopup(builds[i].Target);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.Label("Symbols", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Add"))
                            ArrayUtility.Insert(ref builds[i].symbols, builds[i].symbols.Length, new BatchBuilderSettings.Symbol());
                    }
                    EditorGUILayout.EndHorizontal();

                    for (int j = 0; j < builds[i].symbols.Length; j++)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            builds[i].symbols[j].Active = EditorGUILayout.Toggle(builds[i].symbols[j].Active, GUILayout.Width(15));
                            builds[i].symbols[j].Name = GUILayout.TextField(builds[i].symbols[j].Name);
                            if (GUILayout.Button("Delete", GUILayout.Width(60)))
                            {
                                ArrayUtility.RemoveAt(ref builds[i].symbols, i);
                                i--;
                            }
                        }
                        GUILayout.EndHorizontal();
                    }

                    switch (builds[i].Target)
                    {
                        case BuildTarget.XboxOne:
                            {
                                builds[i].Version = EditorGUILayout.TextField("Version", builds[i].Version);
                                break;
                            }

                        case BuildTarget.Switch:
                            {
                                builds[i].AppID = EditorGUILayout.TextField("ApplicationID", builds[i].AppID);
                                builds[i].ReleaseNumber = EditorGUILayout.IntField("Release", builds[i].ReleaseNumber);
                                break;
                            }
#if HAS_PS4
                        case BuildTarget.PS4:
                        {
                            PS4Settings(ref builds[i]);
                            break;
                        }
#endif
                    }
                    EditorGUILayout.BeginHorizontal();
                    {
                        builds[i].Zip = GUILayout.Toggle(builds[i].Zip, "Create ZIP Archive");
                        builds[i].UnityLogo = GUILayout.Toggle(builds[i].UnityLogo, "Unity Logo");
                        if (builds[i].Target == BuildTarget.StandaloneOSX)
                            builds[i].Sign = GUILayout.Toggle(builds[i].Sign, "Codesign");
                        GUILayout.FlexibleSpace();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.Space();
        EditorGUILayout.EndFadeGroup();
        return i;
    }

#if HAS_PS4
    private void PS4Settings(ref Settings build)
    {
        build.AppID = EditorGUILayout.TextField("ContentID", build.AppID);
        build.Version = EditorGUILayout.TextField("Master Version", build.Version);

        EditorGUILayout.BeginHorizontal();
        {
            build.NPTitleDatPath = EditorGUILayout.TextField("npTitle.dat", build.NPTitleDatPath);
            if (GUILayout.Button("Select", GUILayout.Width(150)))
            {
                string newPath = EditorUtility.OpenFilePanel("npTitle.dat", Path.GetDirectoryName(build.NPTitleDatPath), "dat");
                if (!string.IsNullOrEmpty(newPath))
                {
                    string basePath = Application.dataPath;
                    basePath = basePath.Remove(basePath.Length - "Assets".Length, "Assets".Length);
                    newPath = newPath.Substring(basePath.Length, newPath.Length - basePath.Length);
                    build.NPTitleDatPath = newPath;
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        build.ps4PackageType = (PS4BuildSubtarget)EditorGUILayout.EnumPopup("Build", build.ps4PackageType);

        build.parentalLevel = EditorGUILayout.IntField("Parental Level", build.parentalLevel);

        GUILayout.Label("NP Content Restrictions", EditorStyles.boldLabel);
        build.DefaultAgeRestriction = EditorGUILayout.IntField("Default Age", build.DefaultAgeRestriction);
        if (build.AgeRestrictions != null)
        {
            for (int age = 0; age < build.AgeRestrictions.Length; age++)
            {
                GUILayout.BeginHorizontal();
                {
                    build.AgeRestrictions[age].LanguageCode = EditorGUILayout.TextField("Language Code", build.AgeRestrictions[age].LanguageCode);
                    build.AgeRestrictions[age].Age = EditorGUILayout.IntField("Age", build.AgeRestrictions[age].Age);
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                        ArrayUtility.RemoveAt(ref build.AgeRestrictions, age--);
                }
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.BeginHorizontal();
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(20)))
                ArrayUtility.Add(ref build.AgeRestrictions, new Platform.PS4Data.AgeRestriction());
        }
        GUILayout.EndHorizontal();
    }
#endif
#endregion
}