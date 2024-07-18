using UnityEditor;
using UnityEngine;

namespace Virbe.Core
{
    [CustomEditor(typeof(VirbeBeing))]
    public class VirbeBeingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            VirbeBeing being = (VirbeBeing)target;

            ApiBeingConfig apiBeingConfig = being.ReadCurrentConfig();
            
            if (apiBeingConfig != null && apiBeingConfig.HasValidHostDomain())
            {
                EditorGUILayout.HelpBox(
                    "Make sure you have properly set all the parameters.",
                    MessageType.Info);
                if (GUILayout.Button("Check your being configuration"))
                {
                    Application.OpenURL($"{apiBeingConfig.HostDomain}/dashboard/deploy/unity");
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "If you don't have above parameters, you need to first create being in Virbe Hub web panel",
                    MessageType.Info);
                if (GUILayout.Button("Register new being"))
                {
                    Application.OpenURL($"https://hub.virbe.app");
                }
            }
        }
    }
}