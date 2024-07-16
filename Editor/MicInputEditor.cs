using UnityEditor;
using UnityEngine;
using Virbe.Core.VAD;

namespace MediaPipe.FaceMesh
{
    [CustomEditor(typeof(Mic))]
    sealed class MicInputEditor : Editor
    {
        static readonly GUIContent SelectLabel = new GUIContent("Select");

        SerializedProperty _deviceName;

        void OnEnable()
        {
            _deviceName = serializedObject.FindProperty("PreferredDeviceName");
        }

        void ShowDeviceSelector(Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Default"), false,
                () =>
                {
                    serializedObject.Update();
                    _deviceName.stringValue = null;
                    serializedObject.ApplyModifiedProperties();
                });

            foreach (var device in Microphone.devices)
            {
                menu.AddItem(new GUIContent(device), false,
                    () =>
                    {
                        serializedObject.Update();
                        _deviceName.stringValue = device;
                        serializedObject.ApplyModifiedProperties();
                    });
            }

            menu.DropDown(rect);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(_deviceName);

            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(60));
            if (EditorGUI.DropdownButton(rect, SelectLabel, FocusType.Keyboard))
                ShowDeviceSelector(rect);

            EditorGUILayout.EndHorizontal();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
} // namespace MediaPipe.FaceMesh