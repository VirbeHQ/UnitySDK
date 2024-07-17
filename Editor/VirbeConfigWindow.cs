using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.SharpZipLib.Zip;
using System;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using System.Linq;
using UnityEditor.PackageManager.UI;

namespace Virbe.Core
{
    public class VirbeConfigWindow : EditorWindow
    {
        private const string RpmUrl = "https://github.com/readyplayerme/rpm-unity-sdk-core.git";
        private const string RpmSampleName = "SampleRPM";
        private const string PackageName = "ai.virbe.plugin.unity";

        private Toggle cc3Toggle;
        private Toggle RPMToggle;
        private Toggle FaceItToggle;
        private Toggle Daz3DToggle;
        private Label _logText;
        private Button _rpmButton;
        private Button _rpmSampleButton;
        private VisualElement _rpmButtonsContainer;

        private bool _rpmInstalled;

        private static AddRequest _addRequest;
        private static ListRequest _listRequest;

        private string _cc3ZipPath = "Runtime/Integrations/CC3.zip";
        private string _RPMZipPath = "Runtime/Integrations/RPM.zip";
        private string _Daz3DZipPath = "Runtime/Integrations/Daz3D.zip";
        private string _FaceItZipPath = "Runtime/Integrations/FaceIt.zip";

        private string _IntegrationsPath = "Runtime/Integrations";
        private string _packagePath => Path.Combine("Packages", PackageName);
        private string _cc3FullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "CC3");
        private string _RPMFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "RPM");
        private string _Daz3DFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "Daz3D");
        private string _FaceItFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "FaceIt");


        [MenuItem("Tools/Virbe/Configuration")]
        public static void ShowIntegrations()
        {
            var wnd = GetWindow<VirbeConfigWindow>();
            wnd.titleContent = new GUIContent("Virbe Configuration");
        }

        private void OnGUI()
        {
            var showRPMDownload = _rpmInstalled && RPMToggle.value;
            _rpmButtonsContainer.style.height = RPMToggle.value ? 24 :0 ;
            _rpmButtonsContainer.visible = RPMToggle.value;
            if (showRPMDownload)
            {
                _rpmButton.text = "RPM Installed";
                _rpmButton.clicked -= DownloadRPMPackage;
                _rpmButton.style.color = Color.gray;
            }
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
            RPMToggle.name = "RPM";
            RPMToggle.label = "RPM";
            root.Add(RPMToggle);

            _rpmButton = new Button();
            _rpmButton.name = "Download RPM";
            _rpmButton.text = "Download RPM";
            _rpmButton.style.width = 96;
            _rpmButton.style.height = 24;
            _rpmButton.clicked += DownloadRPMPackage;

            _rpmSampleButton = new Button();
            _rpmSampleButton.name = "RPM Sample";
            _rpmSampleButton.text = "RPM Sample";
            _rpmSampleButton.style.width = 96;
            _rpmSampleButton.style.height = 24;
            _rpmSampleButton.clicked += () => DownloadSample(PackageName, RpmSampleName);

            _rpmButtonsContainer = new VisualElement();
            _rpmButtonsContainer.style.flexDirection = FlexDirection.Row;
            _rpmButtonsContainer.style.height = 24;
            _rpmButtonsContainer.Add(_rpmButton);
            _rpmButtonsContainer.Add(_rpmSampleButton);
            root.Add(_rpmButtonsContainer);

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
            _logText.style.fontSize = 24;
            root.Add(_logText);
        }


        private void AddPackage(string url)
        {
            if(_addRequest != null)
            {
                return;
            }
            _addRequest = Client.Add(url);
            EditorApplication.update += PackageDownloadProgress;
            _logText.text = "Downloading package...";
        }

        private void DownloadSample(string packageName, string sampleName)
        {
            if (_listRequest != null)
            {
                return;
            }
            _listRequest = Client.List(true, false);
            EditorApplication.update += DownloadSample;
        }

        private void DownloadSample()
        {
            if (!_listRequest.IsCompleted)
            {
                return;
            }
            if (_listRequest.Status == StatusCode.Success)
            {
                var package = _listRequest.Result.FirstOrDefault(p => p.name == PackageName);
                if (package != null)
                {
                    var samples = Sample.FindByPackage(PackageName, package.version);
                    if (samples != null)
                    {
                        var sample = samples.FirstOrDefault(s => s.displayName == RpmSampleName);
                        sample.Import(Sample.ImportOptions.OverridePreviousImports);
                        Debug.Log($"Sample '{RpmSampleName}' imported successfully.");
                    }
                    else
                    {
                        Debug.LogError($"No samples found for package '{PackageName}'.");
                    }
                }
                else
                {
                    Debug.LogError($"Package '{PackageName}' not found.");
                }
            }
            else
            {
                Debug.LogError($"Failed to list packages: {_listRequest.Error.message}");
            }
            _listRequest = null;
            EditorApplication.update -= DownloadSample;
        }

        private void DownloadRPMPackage() => AddPackage(RpmUrl);

        private void PackageDownloadProgress()
        {
            if (_addRequest.IsCompleted)
            {
                if (_addRequest.Status == StatusCode.Success)
                {
                    _logText.text = "Downloaded";
                }
                else if (_addRequest.Status >= StatusCode.Failure)
                {
                    _logText.text = "Download ERROR";
                    Debug.Log(_addRequest.Error.message);
                }

                _addRequest = null;
                EditorApplication.update -= PackageDownloadProgress;
            }
        }

        private void Confirmed()
        {
            _logText.text = "Setup in progress";

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
            _logText.text = "Setup completed";
        }

        private void CheckIntegration(Toggle toggle, string fullPath, string zipPath)
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

        private bool CheckIfRPMExists()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return defines.Contains("READY_PLAYER_ME");
        }
    }
}