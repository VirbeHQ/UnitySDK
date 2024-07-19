using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Net;
using Newtonsoft.Json;
using System.IO;

namespace Virbe.Core
{
    public class VirbeConfigWindow : EditorWindow
    {
        private const string VirbeDirectoryPath = "Assets/Virbe";
        private TextField _configLink;
        private Label _textLog;

        [MenuItem("Tools/Virbe/Configuration")]
        public static void ShowIntegrations()
        {
            var wnd = GetWindow<VirbeConfigWindow>();
            wnd.titleContent = new GUIContent("Virbe Being Config");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            Label label = new Label("Place here link from virbe hub:");
            label.style.paddingTop = 16;
            label.style.paddingBottom = 16;
            root.Add(label);

            _configLink = new TextField();
            _configLink.style.paddingTop = 16;
            _configLink.style.paddingBottom = 16;
            _configLink.style.width = 250;
            root.Add(_configLink);

            _textLog = new Label();
            _textLog.name = "Log Info";
            _textLog.text = string.Empty;
            _textLog.style.paddingBottom = 16;
            _textLog.style.fontSize = 12;
            _textLog.style.color = Color.red;
            root.Add(_textLog);

            Button button = new Button();
            button.name = "Set config";
            button.text = "Set config on scene";
            button.style.maxWidth = 128;
            button.style.height = 32;

            root.Add(button);
            button.clicked += Confirmed;
        }

        private void Confirmed()
        {
            var url = _configLink.text;
            _textLog.text = string.Empty;

            var result = Uri.TryCreate(url, UriKind.Absolute, out  var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            if (result)
            {
                var jsonContent = new WebClient().DownloadString(uriResult);
                try
                {
                    var beingConfig = JsonConvert.DeserializeObject<ApiBeingConfig>(jsonContent);
                    if (!Directory.Exists(Path.GetFullPath(VirbeDirectoryPath)))
                    {
                        Directory.CreateDirectory(Path.GetFullPath(VirbeDirectoryPath));
                    }
                    if (beingConfig != null)
                    {
                        var configAsset = new TextAsset(jsonContent);
                        var path = Path.Combine(VirbeDirectoryPath, $"{url.Substring(url.Length - 8)}.txt");
                        File.WriteAllText(path, jsonContent);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                        var beings = FindObjectsOfType<VirbeBeing>();
                        foreach(var being in beings)
                        {
                            being.InitializeFromTextAsset(asset);
                        }
                    }
                }
                catch(Exception e)
                {
                    _textLog.text = "Problem when parsing config from json, see console";
                    Debug.LogError($"[VIRBE] Being config creation error : {jsonContent}, error: {e.Message}");
                }
            }
            else
            {
                _textLog.text = "Link is invalid";
            }
        }
    }
}