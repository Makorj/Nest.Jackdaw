using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using BlackBird;
using static BlackBird.MessageHelper;
using Security = BlackBird.Security;
using System;

public class JackdawConnectWindow : EditorWindow
{
    public static JackdawConnectWindow ShowConnectDialog(JackdawMainWindow jackdawMainWindow)
    {
        JackdawConnectWindow wnd = GetWindow<JackdawConnectWindow>();
        wnd.titleContent = new GUIContent("Jackdaw - Connect");
        wnd.maxSize = wnd.minSize = new Vector2(340,270);
        wnd.mainWindow = jackdawMainWindow;
        wnd.ShowModal();

        return wnd;
    }

    public JackdawMainWindow mainWindow;

    public TextField loginField;
    public TextField passwordField;
    private bool closedViaConnectButton;

    public void OnDisable()
    {
        if(!closedViaConnectButton)
        {
            mainWindow.Close();
        }
    }

    public void OnEnable()
    {
        closedViaConnectButton = false;
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Jackdaw/Editor/JackdawConnectWindow.uss.uxml");
        VisualElement labelFromUXML = visualTree.CloneTree();
        root.Add(labelFromUXML);

        loginField = root.Q<TextField>("loginField");
        passwordField = root.Q<TextField>("passwordField");

        root.Q<Toggle>("rememberMeToggle").SetValueWithoutNotify(EditorPrefs.GetBool("jackdaw_user_rememberme", false));
        root.Q<Toggle>("rememberMeToggle").RegisterValueChangedCallback(RememberMeChangedCallback);


        root.Q<Toggle>("autoLoginToggle").SetValueWithoutNotify(EditorPrefs.GetBool("jackdaw_auto_connect", false));
        root.Q<Toggle>("autoLoginToggle").RegisterValueChangedCallback(AutoLoginChangedCallback);

        loginField.value = EditorPrefs.GetString("jackdaw_user_login", "");
        if(EditorPrefs.HasKey("jackdaw_user_passhash"))
        {
            passwordField.SetValueWithoutNotify("nimportequoi");
        }
        else
        {
            passwordField.SetValueWithoutNotify("");
        }

        loginField.RegisterValueChangedCallback(FieldChangedCallback);
        passwordField.RegisterValueChangedCallback(FieldChangedCallback);

        root.Q<Button>("connectButton").clicked += () => {
            if(EditorPrefs.GetBool("jackdaw_user_rememberme"))
            {
                mainWindow.client?.SendMessage(CLIENT._USER(EditorPrefs.GetString("jackdaw_user_login"), EditorPrefs.GetString("jackdaw_user_passhash")).ToMessage());
            }
            else
            {
                mainWindow.client?.SendMessage(CLIENT._USER(loginField.value, Security.HashPassword(passwordField.value)).ToMessage());
            }

            closedViaConnectButton = true;
            this.Close();
        };
    }

    private void AutoLoginChangedCallback(ChangeEvent<bool> evt)
    {
        if(rootVisualElement.Q<Toggle>("autoLoginToggle").value)
        {
            EditorPrefs.SetBool("jackdaw_auto_connect", true);
            EditorPrefs.SetBool("jackdaw_user_rememberme", true);
            EditorPrefs.SetString("jackdaw_user_login", loginField.value);
            EditorPrefs.SetString("jackdaw_user_passhash", Security.HashPassword(passwordField.value));
        }
        else
        {
            EditorPrefs.DeleteKey("jackdaw_auto_connect");
        }
    }

    private void FieldChangedCallback(ChangeEvent<string> evt)
    {
         if(rootVisualElement.Q<Toggle>("rememberMeToggle").value)
        {
            EditorPrefs.SetString("jackdaw_user_login", loginField.value);
            EditorPrefs.SetString("jackdaw_user_passhash", Security.HashPassword(passwordField.value));
        }
    }

    private void RememberMeChangedCallback(ChangeEvent<bool> evt)
    {
        if(rootVisualElement.Q<Toggle>("rememberMeToggle").value)
        {
            EditorPrefs.SetBool("jackdaw_user_rememberme", true);
            EditorPrefs.SetString("jackdaw_user_login", loginField.value);
            EditorPrefs.SetString("jackdaw_user_passhash", Security.HashPassword(passwordField.value));
        }
        else
        {
            EditorPrefs.DeleteKey("jackdaw_user_rememberme");
            EditorPrefs.DeleteKey("jackdaw_user_login");
            EditorPrefs.DeleteKey("jackdaw_user_passhash");
        }
    }
}