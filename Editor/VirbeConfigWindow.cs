using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.SharpZipLib.Zip;

namespace Virbe.Core
{
    public class VirbeConfigWindow : EditorWindow
    {
        private Toggle cc3Toggle;
        private Toggle RPMToggle;
        private Toggle FaceItToggle;
        private Toggle Daz3DToggle;

        private string _cc3ZipPath = "Runtime/Integrations/CC3.zip";
        private string _RPMZipPath = "Runtime/Integrations/RPM.zip";
        private string _Daz3DZipPath = "Runtime/Integrations/Daz3D.zip";
        private string _FaceItZipPath = "Runtime/Integrations/FaceIt.zip";

        private string _IntegrationsPath = "Runtime/Integrations";
        private string _packagePath = "Packages/ai.virbe.plugin.unity";
        private string _cc3FullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "CC3");
        private string _RPMFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "RPM");
        private string _Daz3DFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "Daz3D");
        private string _FaceItFullPath => Path.Combine(Path.GetFullPath(_packagePath), _IntegrationsPath, "FaceIt");


        [MenuItem("Virbe/Integrations")]
        public static void ShowIntegrations()
        {
            var wnd = GetWindow<VirbeConfigWindow>();
            wnd.titleContent = new GUIContent("Virbe Integrations");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            Label label = new Label("Select integration to use inside Virbe plugin");
            root.Add(label);

            RPMToggle = new Toggle();
            RPMToggle.value = Directory.Exists(_RPMFullPath) && Directory.GetFiles(_RPMFullPath).Length > 0;
            RPMToggle.name = "RPM";
            RPMToggle.label = "RPM";
            root.Add(RPMToggle);

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
            root.Add(Daz3DToggle);

            Button button = new Button();
            button.name = "Confirm";
            button.text = "Confirm";
            root.Add(button);
            button.clicked += Confirmed;
        }

        private void Confirmed()
        {
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
    }
}