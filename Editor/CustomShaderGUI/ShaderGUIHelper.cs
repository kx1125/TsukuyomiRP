using UnityEngine;
using UnityEditor;

public static class ShaderGUIHelper
{
    static private GUIStyle _BigHeaderLabel;
    static private GUIStyle _SubHeaderLabel;
    static private GUIStyle _Text;
    static private GUIStyle _SubToggle;
    static private GUIStyle _Gradient;

    static public GUIStyle BigHeaderFolder
    {
        get
        {
            _BigHeaderLabel = new GUIStyle("ShurikenModuleTitle");
            _BigHeaderLabel.fontStyle = FontStyle.Bold;
            _BigHeaderLabel.fontSize = 14;
            _BigHeaderLabel.alignment = TextAnchor.MiddleLeft;
            _BigHeaderLabel.fixedHeight = 25;
            _BigHeaderLabel.contentOffset = new Vector2(20f, -2f);
            _BigHeaderLabel.normal.textColor = new Color(1.0f, 0.5f, 0f, 1.0f);

            return _BigHeaderLabel;
        }
    }
    
    static public GUIStyle SubHeaderLabel
    {
        get
        {
            _SubHeaderLabel = new GUIStyle(EditorStyles.label);
            _SubHeaderLabel.fontStyle = FontStyle.Bold;
            _SubHeaderLabel.fontSize = 12;
            _SubHeaderLabel.alignment = TextAnchor.MiddleLeft;
            _SubHeaderLabel.contentOffset = new Vector2(0f, -2f);
            _SubHeaderLabel.normal.textColor = new Color(1.0f, 0.7f, 0f, 1.0f);

            return _SubHeaderLabel;
        }
    }
    
    static public GUIStyle SubHeaderFolder
    {
        get
        {
            _SubHeaderLabel = new GUIStyle("ShurikenModuleTitle");
            _SubHeaderLabel.fontStyle = FontStyle.Bold;
            _SubHeaderLabel.fontSize = 12;
            _SubHeaderLabel.alignment = TextAnchor.MiddleLeft;
            _SubHeaderLabel.contentOffset = new Vector2(20f, -2f);
            _SubHeaderLabel.normal.textColor = new Color(1.0f, 0.5f, 0f, 1.0f);

            return _SubHeaderLabel;
        }
    }

    static public GUIStyle Text
    {
        get
        {
            _Text = new GUIStyle(EditorStyles.label);
            _Text.fontStyle = FontStyle.Italic;
            _Text.fontSize = 10;
            //_Text.normal.textColor = Color.gray;
            return _Text;
        }
    }
    static public GUIStyle SubToggle
    {
        get
        {
            _SubToggle = new GUIStyle(EditorStyles.toggle);
            _SubToggle.fontStyle = FontStyle.Normal;
            _SubToggle.normal.textColor = new Color(1.0f, 0.5f, 0f, 1.0f);
            _SubToggle.onNormal.textColor = new Color(1.0f, 0.3f, 0f, 1.0f);
            _SubToggle.hover.textColor = new Color(1.0f, 0.5f, 0f, 1.0f);
            _SubToggle.onHover.textColor = new Color(1.0f, 0.3f, 0f, 1.0f);
            return _SubToggle;
        }
    }

    static public GUIStyle GradientEditor
    {
        get
        {
            _Gradient = new GUIStyle();
            return _Gradient;
        }
    }

    static public bool GetFoldOut(bool display, string title, string tooltip = null)
    {
        GUIContent content = new GUIContent(title, tooltip);
        Rect rect = GUILayoutUtility.GetRect(content, BigHeaderFolder);
        GUI.Box(rect, content, BigHeaderFolder);
        
        var toggleRect = new Rect(rect.x + 2f, rect.y + 4f, 13f, 13f);
        var e = Event.current;
        if (e.type == EventType.Repaint)
        {
            EditorStyles.foldout.Draw(toggleRect, false, false, display, false);
        }

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            display = !display;
            e.Use();
        }

        return display;
    }

    static public bool GetSubFoldOut(bool display, string title, string tooltip = null)
    {
        GUIContent content = new GUIContent(title, tooltip);
        Rect rect = GUILayoutUtility.GetRect(content, SubHeaderFolder);
        GUI.Box(rect, content, SubHeaderFolder);
        
        var toggleRect = new Rect(rect.x + 2f, rect.y + 2f, 10f, 10f);
        var e = Event.current;
        if (e.type == EventType.Repaint)
        {
            EditorStyles.foldout.Draw(toggleRect, false, false, display, false);
        }

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            display = !display;
            e.Use();
        }

        return display;
    }
    
    static public void GetSubHeader(string title, string tooltip = null)
    {
        GUIContent content = new GUIContent(title, tooltip);
        Rect rect = GUILayoutUtility.GetRect(content, SubHeaderLabel);
        EditorGUI.LabelField(rect, content, SubHeaderLabel);
    }

    static public bool GetSubToggle(bool toggle, string title, string tooltip = null)
    {
        GUIContent content = new GUIContent(title, tooltip);
        Rect rect = GUILayoutUtility.GetRect(content, SubToggle);
        rect.x += 40;
        //rect.width *= 0.4f;
        bool subToggle = GUI.Toggle(rect, toggle, content, SubToggle);
        return subToggle;
    }

    static public Gradient GetGradient(Gradient gradient)
    {
        Rect rect = GUILayoutUtility.GetRect(new GUIContent(), GradientEditor);
        Gradient _gradient = EditorGUILayout.GradientField(gradient, GUILayout.MaxHeight(15f));
        return _gradient;
    }
}
