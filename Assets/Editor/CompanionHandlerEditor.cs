#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CompanionHandler))]
public class CompanionHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        CompanionHandler handler = (CompanionHandler)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Follow"))
        {
            handler.SetFollowState();
        }
        if (GUILayout.Button("Protect"))
        {
            handler.SetProtectState();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Heal"))
        {
            handler.SetHealState();
        }
        if (GUILayout.Button("Scout"))
        {
            handler.SetRunAroundState();
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("Submit Manual Input", GUILayout.Height(30)))
        {
            handler.OnSubmitButtonClicked();
        }
    }
}
#endif