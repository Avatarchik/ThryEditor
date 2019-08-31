﻿using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Thry
{

    public class ThryEditorGuiHelper
    {

        public static void drawConfigTextureProperty(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor, bool scaleOffset)
        {
            if (Config.Get().useBigTextures) drawBigTextureProperty(position, prop, label, editor, scaleOffset);
            else drawSmallTextureProperty(position, prop, label, editor, scaleOffset);
        }

        public static void drawSmallTextureProperty(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor, bool scaleOffset)
        {
            Rect thumbnailPos = position;
            thumbnailPos.x += scaleOffset ? 20 : 0;
            editor.TexturePropertyMiniThumbnail(thumbnailPos, prop,label.text, (scaleOffset? "Click here for scale / offset":"") + (label.tooltip != "" ? " | " : "") + label.tooltip);
            if (scaleOffset && DrawingData.currentTexProperty != null)
            {
                //draw dropdown triangle
                thumbnailPos.x += ThryEditor.currentlyDrawing.currentProperty.xOffset * 30;
                if (Event.current.type == EventType.Repaint)
                    EditorStyles.foldout.Draw(thumbnailPos, false, false, DrawingData.currentTexProperty.showScaleOffset, false);
                //test click and draw scale/offset
                if (DrawingData.currentTexProperty.showScaleOffset) ThryEditor.currentlyDrawing.editor.TextureScaleOffsetProperty(prop);
                if (ThryEditor.MouseClick && position.Contains(Event.current.mousePosition))
                {
                    DrawingData.currentTexProperty.showScaleOffset = !DrawingData.currentTexProperty.showScaleOffset;
                    editor.Repaint();
                }
            }

            DrawingData.lastGuiObjectRect = position;
        }

        public static void drawBigTextureProperty(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor, bool scaleOffset)
        {
            GUILayoutUtility.GetRect(label, Styles.Get().bigTextureStyle);
            editor.TextureProperty(position, prop, label.text, label.tooltip, scaleOffset);
            DrawingData.lastGuiObjectRect = position;
        }

        //draw the render queue selector
        public static int drawRenderQueueSelector(Shader defaultShader, int customQueueFieldInput)
        {
            EditorGUILayout.BeginHorizontal();
            if (customQueueFieldInput == -1) customQueueFieldInput = ThryEditor.currentlyDrawing.materials[0].renderQueue;
            int[] queueOptionsQueues = new int[] { defaultShader.renderQueue, 2000, 2450, 3000, customQueueFieldInput };
            string[] queueOptions = new string[] { "From Shader", "Geometry", "Alpha Test", "Transparency" };
            int queueSelection = 4;
            if (defaultShader.renderQueue == customQueueFieldInput) queueSelection = 0;
            else
            {
                string customOption = null;
                int q = customQueueFieldInput;
                if (q < 2000) customOption = queueOptions[1] + "-" + (2000 - q);
                else if (q < 2450) { if (q > 2000) customOption = queueOptions[1] + "+" + (q - 2000); else queueSelection = 1; }
                else if (q < 3000) { if (q > 2450) customOption = queueOptions[2] + "+" + (q - 2450); else queueSelection = 2; }
                else if (q < 5001) { if (q > 3000) customOption = queueOptions[3] + "+" + (q - 3000); else queueSelection = 3; }
                if (customOption != null) queueOptions = new string[] { "From Shader", "Geometry", "Alpha Test", "Transparency", customOption };
            }
            EditorGUILayout.LabelField("Render Queue", GUILayout.ExpandWidth(true));
            int newQueueSelection = EditorGUILayout.Popup(queueSelection, queueOptions, GUILayout.MaxWidth(100));
            int newQueue = queueOptionsQueues[newQueueSelection];
            if (queueSelection != newQueueSelection) customQueueFieldInput = newQueue;
            int newCustomQueueFieldInput = EditorGUILayout.IntField(customQueueFieldInput, GUILayout.MaxWidth(65));
            bool isInput = customQueueFieldInput != newCustomQueueFieldInput || queueSelection != newQueueSelection;
            customQueueFieldInput = newCustomQueueFieldInput;
            foreach (Material m in ThryEditor.currentlyDrawing.materials)
                if (customQueueFieldInput != m.renderQueue && isInput) m.renderQueue = customQueueFieldInput;
            if (customQueueFieldInput != ThryEditor.currentlyDrawing.materials[0].renderQueue && !isInput) customQueueFieldInput = ThryEditor.currentlyDrawing.materials[0].renderQueue;
            EditorGUILayout.EndHorizontal();
            return customQueueFieldInput;
        }

        //draw all collected footers
        public static void drawFooters(List<string> footers)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Space(2);
            foreach (string footNote in footers)
            {
                drawFooter(footNote);
                GUILayout.Space(2);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        //draw single footer
        private static void drawFooter(string data)
        {
            string[] splitNote = data.TrimEnd(')').Split("(".ToCharArray(), 2);
            string value = splitNote[1];
            string type = splitNote[0];
            if (type == "linkButton")
            {
                string[] values = value.Split(",".ToCharArray());
                drawLinkButton(70, 20, values[0], values[1]);
            }
        }

        //draw a button with a link
        private static void drawLinkButton(int Width, int Height, string title, string link)
        {
            if (GUILayout.Button(title, GUILayout.Width(Width), GUILayout.Height(Height)))
            {
                Application.OpenURL(link);
            }
        }

        public static void DrawHeader(ref bool enabled, ref bool options, GUIContent name)
        {
            var r = EditorGUILayout.BeginHorizontal("box");
            enabled = EditorGUILayout.Toggle(enabled, EditorStyles.radioButton, GUILayout.MaxWidth(15.0f));
            options = GUI.Toggle(r, options, GUIContent.none, new GUIStyle());
            EditorGUILayout.LabelField(name, Styles.Get().dropDownHeaderLabel);
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawMasterLabel(string shaderName)
        {
            

            EditorGUILayout.LabelField("<size=16>" + shaderName + "</size>", Styles.Get().masterLabel, GUILayout.MinHeight(18));
        }
    }

    //-----------------------------------------------------------------

    public class ThryEditorHeader
    {
        private List<MaterialProperty> propertyes;
        private bool currentState;

        public ThryEditorHeader(MaterialEditor materialEditor, string propertyName)
        {
            this.propertyes = new List<MaterialProperty>();
            foreach (Material materialEditorTarget in materialEditor.targets)
            {
                Object[] asArray = new Object[] { materialEditorTarget };
                propertyes.Add(MaterialEditor.GetMaterialProperty(asArray, propertyName));
            }

            this.currentState = fetchState();
        }

        public bool fetchState()
        {
            foreach (MaterialProperty materialProperty in propertyes)
            {
                if (materialProperty.floatValue == 1)
                    return true;
            }



            return false;
        }

        public bool getState()
        {
            return this.currentState;
        }

        public void Toggle()
        {

            if (getState())
            {
                foreach (MaterialProperty materialProperty in propertyes)
                {
                    materialProperty.floatValue = 0;
                }
            }
            else
            {
                foreach (MaterialProperty materialProperty in propertyes)
                {
                    materialProperty.floatValue = 1;
                }
            }

            this.currentState = !this.currentState;
        }

        public void Foldout(int xOffset, GUIContent content, ThryEditor gui)
        {
            var style = new GUIStyle(Styles.Get().dropDownHeader);
            style.margin.left = 30 * xOffset;

            var rect = GUILayoutUtility.GetRect(16f + 20f, 22f, style);
            DrawingData.lastGuiObjectRect = rect;
            GUI.Box(rect, content, style);

            var e = Event.current;

            var toggleRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
            if (e.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(toggleRect, false, false, getState(), false);
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition)&&!e.alt)
            {
                this.Toggle();
                e.Use();
            }
        }
    }
}
