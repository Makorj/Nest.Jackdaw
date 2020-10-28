using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Jackdaw.Unity
{

    #region GlobalSettings
    // Create a new type of Settings Asset.
    class JackdawGlobalSettings : ScriptableObject
    {
        public static readonly string JackdawGlobalSettingsDirectoryPath = "Assets/Editor/Jackdaw";
        public static readonly string JackdawGlobalSettingsAssetName = "globalSettings.asset";

        [SerializeField]
        public string m_vcs_address;

        [SerializeField]
        public string m_hawk_server_address;

        [SerializeField]
        public int m_hawk_server_port;


        internal static JackdawGlobalSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<JackdawGlobalSettings>(Path.Combine(JackdawGlobalSettingsDirectoryPath, JackdawGlobalSettingsAssetName));
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<JackdawGlobalSettings>();
                settings.m_vcs_address = "127.0.0.1";
                settings.m_hawk_server_port = 42;
                settings.m_hawk_server_address = "192.168.0.21";

                if (!Directory.Exists(JackdawGlobalSettingsDirectoryPath))
                {
                    Directory.CreateDirectory(JackdawGlobalSettingsDirectoryPath);
                }

                AssetDatabase.CreateAsset(settings, Path.Combine(JackdawGlobalSettingsDirectoryPath, JackdawGlobalSettingsAssetName));
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }

    // Register a SettingsProvider using IMGUI for the drawing framework:
    static class JackdawGlobalSettingsIMGUIRegister
    {
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Project/Jackdaw/Global", SettingsScope.Project)
            {
                // By default the last token of the path is used as display name if no label is provided.
                label = "Jackdaw - Global",
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    var settings = JackdawGlobalSettings.GetSerializedSettings();
                    EditorGUILayout.LabelField("Version control sources configuration", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(settings.FindProperty("m_vcs_address"), new GUIContent("VCS source adress"));
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Hawk server congfiguration", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(settings.FindProperty("m_hawk_server_address"), new GUIContent("Server IP address"));
                    EditorGUILayout.PropertyField(settings.FindProperty("m_hawk_server_port"), new GUIContent("Server port"));

                    settings.ApplyModifiedProperties();
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] { "Number", "Some String" })
            };

            return provider;
        }
    }
    #endregion

    #region UserSettings
    class JackdawUserSettings : ScriptableObject
    {
        public static readonly string JackdawUserSettingsDirectoryPath = "UsersSettings/Jackdaw";
        public static readonly string JackdawUserSettingsAssetName = "userSettings.asset";


        internal static JackdawUserSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<JackdawUserSettings>(Path.Combine(JackdawUserSettingsDirectoryPath, JackdawUserSettingsAssetName));
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<JackdawUserSettings>();

                if (!Directory.Exists(JackdawUserSettingsDirectoryPath))
                {
                    Directory.CreateDirectory(JackdawUserSettingsDirectoryPath);
                }

                AssetDatabase.CreateAsset(settings, Path.Combine(JackdawUserSettingsDirectoryPath, JackdawUserSettingsAssetName));
                AssetDatabase.SaveAssets();
            }
            return settings;
        }

        internal static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
    }


    static class JackdawUserSettingsIMGUIRegister
    {
        [SettingsProvider]
        public static SettingsProvider CreateMyCustomSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            var provider = new SettingsProvider("Project/Jackdaw/User settings", SettingsScope.Project)
            {
                // By default the last token of the path is used as display name if no label is provided.
                label = "Jackdaw - User settings",
                // Create the SettingsProvider and initialize its drawing (IMGUI) function in place:
                guiHandler = (searchContext) =>
                {
                    //var settings = JackdawUserSettings.GetSerializedSettings();
                    EditorGUILayout.LabelField("Authentification configuration", EditorStyles.boldLabel);

                    var oldValue = EditorPrefs.GetBool("jackdaw_user_rememberme", false);
                    bool newvalue;
                    newvalue = EditorGUILayout.Toggle("Remember credentials", oldValue);

                    if(newvalue != oldValue)
                    {
                        if(newvalue)
                        {
                            EditorPrefs.SetBool("jackdaw_user_rememberme", newvalue);
                        }
                        else
                        {
                            EditorPrefs.DeleteKey("jackdaw_user_rememberme");
                        }
                    }

                    if(newvalue)
                    {
                        oldValue = EditorPrefs.GetBool("jackdaw_auto_connect", false);
                        newvalue = EditorGUILayout.Toggle("Auto connect to server on startup", oldValue);

                        if(newvalue != oldValue)
                        {
                            if(newvalue)
                            {
                                EditorPrefs.SetBool("jackdaw_auto_connect", newvalue);
                            }
                            else
                            {
                                EditorPrefs.DeleteKey("jackdaw_auto_connect");
                            }
                        }
                    }
                    else
                    {
                        if(EditorPrefs.GetBool("jackdaw_auto_connect", false))
                            EditorPrefs.DeleteKey("jackdaw_auto_connect");
                    }

                    //settings.ApplyModifiedProperties();
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = new HashSet<string>(new[] { "Number", "Some String" })
            };

            return provider;
        }
    }

    #endregion
}
