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
        private string _cc3ZipPath = "Runtime/Integrations/CC3.zip";
        private string _cc3FolderPath = "Runtime/Integrations/CC3";
        private string _packagePath = "Packages/ai.virbe.plugin.unity";

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

            cc3Toggle = new Toggle();
            cc3Toggle.name = "CC3";
            cc3Toggle.label = "CC3";
            root.Add(cc3Toggle);

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
                var folderFullPath = Path.Combine(Path.GetFullPath(_packagePath), _cc3FolderPath);
                if (cc3Toggle.value && !Directory.Exists(folderFullPath))
                {
                    var zipFileName = Path.Combine(Path.GetFullPath(_packagePath), _cc3ZipPath);
                    FastZip fastZip = new FastZip();
                    fastZip.ExtractZip(zipFileName, folderFullPath, null);
                }
                else if (!cc3Toggle.value && Directory.Exists(folderFullPath))
                {
                    File.Delete(folderFullPath);
                }
            }
        }
    }
}