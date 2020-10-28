using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Unity.EditorCoroutines.Editor;
using System.Collections.Generic;
using System;
using Jackdaw;
using Jackdaw.Unity;
using System.Net;
using BlackBird;
using System.Collections;
using static BlackBird.MessageHelper;

public class JackdawMainWindow : EditorWindow
{
    [MenuItem("Jackdaw/Main Window")]
    public static void ShowExample()
    {
        JackdawMainWindow wnd = GetWindow<JackdawMainWindow>();

        wnd.hawkCallbacks = new Dictionary<string, Action>
        {
            { HAWK.CONNECT, wnd.OnHawkConnect_Callbback },
            { HAWK.ERROR.USER_UNKNOWN, wnd.OnLoginError_Callback },
            { HAWK.ERROR.NO_AVAILABLE_WORKER, () => EditorUtility.DisplayDialog("[ERROR] No remote worker available", "No remote worker is available to accomplish your task. Please try again later", "OK")},

        };

        wnd.StartAndConnectToHawkServer();

        wnd.titleContent = new GUIContent("Jackdaw - Main Window");
        wnd.Show();

    }

    public bool isClosing = false;
    private JackdawConnectWindow jackdawConnectWindow;

    private List<Jackdaw.RequestData> commandList;
    public BlackBird.Client client;

    public Dictionary<string, Action> hawkCallbacks;

    public void OnEnable()
    {
        commandList = new List<Jackdaw.RequestData>();

        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Import UXML
        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Jackdaw/Editor/JackdawMainWindow.uxml");
        VisualElement labelFromUXML = visualTree.CloneTree();
        root.Add(labelFromUXML);

        root.Q<Button>("toolbarOpenPref").clicked += () => { SettingsService.OpenProjectSettings("Project/Jackdaw"); };

        root.Q<Button>("openNewCommandWindowButton").clicked += OpenIssueCommandWindow;

        root.Q<Button>("sendCommandListButton").clicked += SendCommandList;

    }

    public void OnDisable()
    {
        isClosing = true;
        client?.SendMessage(CLIENT.DISCONNECT.ToMessage());
    }

    private void OpenIssueCommandWindow()
    {
        JackdawRequestNewCommandEditorWindow.IssueNewCommandWindow(this);
    }

    internal void AddCommand(RequestData m_Command)
    {
        commandList.Add(m_Command);
        var root = new VisualElement();
        root.style.marginTop = new StyleLength(5);
        root.style.marginLeft = new StyleLength(10);
        root.style.marginBottom = new StyleLength(5);

        Label labelTitle = new Label(m_Command.commands.ToString());
        root.Add(labelTitle);

        string details;
        switch (m_Command.commands)
        {
            case RequestType.UpdateVCS:
                details = "Update to last revision";
                break;
            case RequestType.RequestVCS:
                details = "Update to specific revision";
                break;
            case RequestType.Test:
                details = "Test";
                break;
            case RequestType.Build:
                details = "Build";
                break;
            case RequestType.Publish:
                details = "Publish";
                break;
            default:
                details = "";
                break;
        }

        Label labelDetails = new Label(details);
        root.Add(labelDetails);

        Button button = new Button();
        button.text = "Delete " + m_Command.commands.ToString();
        button.clicked += () => {
            commandList.Remove(m_Command);
            rootVisualElement.Q<VisualElement>("commandListPanel").Remove(root);
        };

        root.Add(button);

        rootVisualElement.Q<VisualElement>("commandListPanel").Add(root);
    }

    public void SendCommandList()
    {
        hawkCallbacks[HAWK.OK] = () => {
            client.SendMessage(CLIENT.SEND.COMMAND_LIST.ToMessage());

            foreach (var item in commandList)
            {
                string message = "";
                switch (item.commands)
                {
                    case RequestType.UpdateVCS:
                        message = CLIENT.SEND.VCS_UPDATE;
                        break;
                    case RequestType.RequestVCS:
                        message = "Update to specific revision";
                        break;
                    case RequestType.Test:
                        message = CLIENT.SEND.TEST;
                        break;
                    case RequestType.Build:
                        message = CLIENT.SEND.BUILD;
                        break;
                    case RequestType.Publish:
                        message = CLIENT.SEND.PUBLISH;
                        break;
                    default:
                        message = "";
                        break;
                }

                client.SendMessage(message.ToMessage());
                client.SendMessage(item.parameters.ToMessage());
            }
        };

        client.SendMessage(CLIENT.REQUEST.AVAILABLE_WORKER.ToMessage());
    }

#region HAWK

    public void StartAndConnectToHawkServer()
    {
        Retry:
        try
        {
            client = new BlackBird.Client(new BlackBird.ServerInfo{
                ipAddress = IPAddress.Parse(JackdawGlobalSettings.GetOrCreateSettings().m_hawk_server_address),
                port = JackdawGlobalSettings.GetOrCreateSettings().m_hawk_server_port
            });

            client.SendMessage(CLIENT.BASE.ToMessage());
            EditorCoroutineUtility.StartCoroutine(ReceiveMessageCoroutine(), this);
        }
        catch (Exception)
        {
            if(EditorUtility.DisplayDialog("[ERROR] Impossible to reach the server", "Could not connect to Hawk server. \n Do you want to try again ?", "Try again", "Cancel"))
            {
                goto Retry;
            }
            else
            {
                this.Close();
            }
        }
    }

#region HAWK CALLBACKS

    public void OnLoginError_Callback()
    {
        EditorUtility.DisplayDialog("Invalid credentials", "The login and/or password given did not allow us to identify you. Please try again or contact your administrator", "Ok");
        JackdawConnectWindow.ShowConnectDialog(this);
    }

    public void OnHawkConnect_Callbback()
    {
        if(EditorPrefs.GetBool("jackdaw_auto_connect", false))
        {
            client?.SendMessage(CLIENT._USER(EditorPrefs.GetString("jackdaw_user_login"), EditorPrefs.GetString("jackdaw_user_passhash")).ToMessage());
        }
        else
        {
            JackdawConnectWindow.ShowConnectDialog(this);
        }
    }

#endregion

    public IEnumerator ReceiveMessageCoroutine()
    {
        while(!isClosing)
        {
            yield return new WaitUntil(() =>{ return client.IsMessageAvailable() || isClosing; });

            if(!isClosing)
            {
                Message received = client.ReceiveMessage();
                string message = received.ToString();

                if(hawkCallbacks.TryGetValue(message, out Action act))
                {
                    act.Invoke();
                }
                else
                {
                    Debug.Log($"Message received from Hawk : \n {message}");
                }
            }
        }
    }

#endregion


}