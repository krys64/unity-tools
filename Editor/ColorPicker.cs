using UnityEngine;
using UnityEditor;

public class ColorPicker : EditorWindow
{
    [SerializeField]
    Color color;
    [SerializeField]
    string hexColor;
    string myString = "Hello World";
    bool groupEnabled;
    bool myBool = true;
    float myFloat = 1.23f;

    [MenuItem("Window/Color Picker")]
    static void Init()
    {
        ColorPicker window = (ColorPicker)EditorWindow.GetWindow(typeof(ColorPicker));
        window.Show();
    }

    void OnGUI()
    {
        color = EditorGUILayout.ColorField("color", color);
        hexColor = EditorGUILayout.TextField("hexColor","#"+ ColorUtility.ToHtmlStringRGBA(color));

        
    }
}