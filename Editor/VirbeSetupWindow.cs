using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.SharpZipLib.Zip;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using System.Collections;
using System;
using System.Net;

namespace Virbe.Core
{
    public class VirbeConfigWindowL : EditorWindow
    {
        private TextField _configLink;
        private Label _textLog;

        [MenuItem("Tools/Virbe/Configuration")]
        public static void ShowIntegrations()
        {
            var wnd = GetWindow<VirbeSetupWindow>();
            wnd.titleContent = new GUIContent("Virbe Being Config");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            Label label = new Label("Place here link from virbe hub:");
            label.style.paddingTop = 16;
            label.style.paddingBottom = 32;
            root.Add(label);

            _configLink = new TextField();
            _configLink.style.paddingTop = 16;
            _configLink.style.paddingBottom = 16;
            _configLink.style.width = 250;
            root.Add(_configLink);

            _textLog = new Label();
            _textLog.name = "Log Info";
            _textLog.text = string.Empty;
            _textLog.style.paddingTop = 8;
            _textLog.style.fontSize = 12;
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
            var result = Uri.TryCreate(url, UriKind.Absolute, out  var uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            if (result)
            {
                var jsonContent = new WebClient().DownloadString(uriResult);
                var beingConfig = Jsoncon
            }
            else
            {
                _textLog.text = "Link is invalid";
            }
        }
    }

    public class VirbeSetupWindow : EditorWindow
    {
        public class ConfigRunner
        {
            private IEnumerator _currentRunner;

            public void RunConfig(IEnumerator configCoroutine)
            {
                _currentRunner = configCoroutine;
                EditorApplication.update += UpdateFunc;
            }

            private void UpdateFunc()
            {
                if (_currentRunner != null)
                {
                    _currentRunner.MoveNext();
                }
                else
                {
                    EditorApplication.update -= UpdateFunc;
                }
            }
        }
        private const string RpmUrl = "https://github.com/readyplayerme/rpm-unity-sdk-core.git";
        private const string PackageName = "ai.virbe.plugin.unity";
        private const string _IntegrationsPath = "Runtime/Integrations";

        private Toggle cc3Toggle;
        private Toggle RPMToggle;
        private Toggle FaceItToggle;
        private Toggle Daz3DToggle;
        private Label _logText;
        private Toggle _rpmDownloadToggle;
        private VisualElement _rpmButtonsContainer;
        private ConfigRunner _configRunner;

        private bool _rpmInstalled;

        private static AddRequest _addRequest;
        private static ListRequest _listRequest;

        private string _cc3ZipPath = "Runtime/Integrations/CC3.zip";
        private string _RPMZipPath = "Runtime/Integrations/RPM.zip";
        private string _Daz3DZipPath = "Runtime/Integrations/Daz3D.zip";
        private string _FaceItZipPath = "Runtime/Integrations/FaceIt.zip";

        private static string _packagePath => Path.Combine("Packages", PackageName);

        private static string _cc3FullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "CC3");
        private static string _RPMFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "RPM");
        private static string _Daz3DFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "Daz3D");
        private static string _FaceItFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "FaceIt");


        [MenuItem("Tools/Virbe/Integration")]
        public static void ShowIntegrations()
        {
            var wnd = GetWindow<VirbeSetupWindow>();
            wnd.titleContent = new GUIContent("Virbe Integration");
        }

        public void CreateGUI()
        {
            _rpmInstalled = CheckIfRPMExists();

            var root = rootVisualElement;
            Label label = new Label("Select integration to use with Virbe SDK");
            label.style.paddingTop = 16;
            label.style.paddingBottom = 32;
            root.Add(label);

            RPMToggle = new Toggle();
            RPMToggle.value = Directory.Exists(_RPMFullPath) && Directory.GetFiles(_RPMFullPath).Length > 0;
            RPMToggle.RegisterValueChangedCallback(RPMToggleValueChanged);
            RPMToggle.name = "RPM";
            RPMToggle.label = "RPM";
            root.Add(RPMToggle);

            _rpmDownloadToggle = new Toggle();
            _rpmDownloadToggle.value = false;
            _rpmDownloadToggle.name = "Download RPM";
            _rpmDownloadToggle.label = "Download RPM";
            _rpmDownloadToggle.visible = false;
            root.Add(_rpmDownloadToggle);

            cc3Toggle = new Toggle();
            cc3Toggle.value = Directory.Exists(_cc3FullPath) && Directory.GetFiles(_cc3FullPath).Length > 0;
            cc3Toggle.name = "CC3";
            cc3Toggle.label = "CC3";
            root.Add(cc3Toggle);

            FaceItToggle = new Toggle();
            FaceItToggle.value = Directory.Exists(_FaceItFullPath) && Directory.GetFiles(_FaceItFullPath).Length > 0;
            FaceItToggle.name = "Face It";
            FaceItToggle.label = "Face It";
            root.Add(FaceItToggle);

            Daz3DToggle = new Toggle();
            Daz3DToggle.value = Directory.Exists(_Daz3DFullPath) && Directory.GetFiles(_Daz3DFullPath).Length > 0;
            Daz3DToggle.name = "Daz3D";
            Daz3DToggle.label = "Daz3D";
            Daz3DToggle.style.paddingBottom = 32;
            root.Add(Daz3DToggle);

            Button button = new Button();
            button.name = "Confirm";
            button.text = "Confirm";
            button.style.maxWidth = 128;
            button.style.height = 32;

            root.Add(button);
            button.clicked += Confirmed;

            _logText = new Label();
            _logText.name = "Log Info";
            _logText.text = string.Empty;
            _logText.style.paddingTop = 32;
            _logText.style.fontSize = 18;
            root.Add(_logText);
        }

        private void RPMToggleValueChanged(ChangeEvent<bool> evt)
        {
            _rpmDownloadToggle.visible = evt.newValue && !CheckIfRPMExists();
            _rpmDownloadToggle.value = false;
        }

        private IEnumerator AddPackage(string url)
        {
            LogToWindow($"Downloading package : {url}");
            _addRequest = Client.Add(url);
            while (!_addRequest.IsCompleted)
            {
                yield return new WaitForSeconds(0.2f);
            }
            if (_addRequest.IsCompleted)
            {
                if (_addRequest.Status == StatusCode.Success)
                {
                    LogToWindow("Downloaded");
                }
                else if (_addRequest.Status >= StatusCode.Failure)
                {
                    LogToWindow("Download ERROR");
                    Debug.Log(_addRequest.Error.message);
                }
                _addRequest = null;
            }
        }

        private void LogToWindow(string log)
        {
            if (_logText != null)
            {
                _logText.text = log;
            }
            Debug.Log(log);
        }

        private void Confirmed()
        {
            _configRunner = new ConfigRunner();
            _configRunner.RunConfig(ConfigCoroutine(_rpmDownloadToggle.value, cc3Toggle, RPMToggle, FaceItToggle, Daz3DToggle));
        }

        private IEnumerator ConfigCoroutine(bool downloadRPM, Toggle cc3Toggle, Toggle RPMToggle, Toggle FaceItToggle, Toggle Daz3DToggle)
        {
            LogToWindow("Setup in progress");

            if (downloadRPM)
            {
                LogToWindow($"Downloading package : {RpmUrl}");
                _addRequest = Client.Add(RpmUrl);
                while (!_addRequest.IsCompleted)
                {
                    yield return new WaitForSeconds(0.2f);
                }
                if (_addRequest.IsCompleted)
                {
                    if (_addRequest.Status == StatusCode.Success)
                    {
                        LogToWindow("Downloaded");
                    }
                    else if (_addRequest.Status >= StatusCode.Failure)
                    {
                        LogToWindow("Download ERROR");
                        Debug.Log(_addRequest.Error.message);
                    }
                    _addRequest = null;
                }
            }
            LogToWindow("Prepare integration");

            if (cc3Toggle != null)
            {
                CheckIntegration(cc3Toggle, _cc3FullPath, _cc3ZipPath);
            }
            if (RPMToggle != null)
            {
                CheckIntegration(RPMToggle, _RPMFullPath, _RPMZipPath);
            }
            if (FaceItToggle != null)
            {
                CheckIntegration(FaceItToggle, _FaceItFullPath, _FaceItZipPath);
            }
            if (Daz3DToggle != null)
            {
                CheckIntegration(Daz3DToggle, _Daz3DFullPath, _Daz3DFullPath);
            }
            AssetDatabase.Refresh();
            LogToWindow("Setup completed");
        }

        private static void CheckIntegration(Toggle toggle, string fullPath, string zipPath)
        {
            Directory.CreateDirectory(fullPath);

            var files = Directory.GetFiles(fullPath);
            if (toggle.value && files.Length == 0)
            {
                var zipFileName = Path.Combine(Path.GetFullPath(_packagePath), zipPath);
                FastZip fastZip = new FastZip();
                fastZip.ExtractZip(zipFileName, Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath), null);
            }
            else if (!toggle.value && files.Length > 0)
            {
                Directory.Delete(fullPath, true);
                Directory.CreateDirectory(fullPath);
            }
        }

        private static bool CheckIfRPMExists()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return defines.Contains("READY_PLAYER_ME");
        }
    }
}