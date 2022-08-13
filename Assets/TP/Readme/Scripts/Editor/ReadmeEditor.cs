#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.WebPages;
using UnityEngine;
using UnityEditor;
using Application = UnityEngine.Application;
using Object = UnityEngine.Object;
using TheArtOfDev.HtmlRenderer.PdfSharp;
using PdfSharp;
using PdfSharp.Pdf;
using HtmlAgilityPack;

namespace TP 
{
    [CustomEditor(typeof(Readme)), ExecuteInEditMode]
    public class ReadmeEditor : Editor
    {
        public static ReadmeEditor ActiveReadmeEditor;

        #region private vars
        private Readme readme;

        private bool verbose = false;
        private bool liteEditor;

        // State
        private bool editing = false;
        private bool boldOn = false;
        private bool italicOn = false;
        private bool sourceOn = false;

        //OnInspectorGUI State
        private static bool showDebugButtons = false;
        private static bool showAdvancedOptions = false;
        private static bool showCursorPosition = false;
        private static bool showObjectIdPairs = false;
        private static bool showDebugInfo = false;

        //Text Editor
        private TextEditor textEditor;
        private bool selectIndexChanged = false;
        private bool cursorIndexChanged = false;
        private bool editorSelectIndexChanged = false;
        private bool editorCursorIndexChanged = false;
        private int previousCursorIndex = -1;
        private int previousSelectIndex = -1;
        private int currentCursorIndex = -1;
        private int currentSelectIndex = -1;
        private Rect textAreaRect;
        private Rect scrollAreaRect;
        private bool richTextChanged = false;
        private bool mouseCaptured = false;
        private Stack tempCursorIndex = new Stack();
        private Stack tempSelectIndex = new Stack();

        // Editor Control/Styles
        private string activeTextAreaName = "";
        private string textAreaEmptyName;
        private string textAreaReadonlyName;
        private string textAreaSourceName;
        private string textAreaStyleName;
        private GUIStyle activeTextAreaStyle;
        private GUIStyle emptyRichText;
        private GUIStyle selectableRichText;
        private GUIStyle editableRichText;
        private GUIStyle editableText;
        private GUISkin skinDefault;
        private GUISkin skinStyle;
        private GUISkin skinSource;
        private int textPadding = 5;
        private float availableWidth;
        private Dictionary<int, string> controlIdToName = new Dictionary<int, string>();
        private Dictionary<string, int> controlNameToId = new Dictionary<string, int>();
        private int activeTextAreaControlId;

        //Scrolling
        private bool scrollEnabled = true;
        private int scrollMaxHeight = 400;
        private Vector2 scroll;
        private int scrollAreaPad = 6;
        private int scrollBarSize = 15;

        // Text area focus
        private string previousFocusedWindow = "";
        private bool windowFocusModified = false;
        private bool textAreaRefreshPending = false;
        private int focusDelay = 0;
        private int focusCursorIndex = -1;
        private int focusSelectIndex = -1;
        private bool autoFocus = true;
        private bool autoSetEditorRect = false;
        private Rect doneEditButtonRect;

        // Drag and drop object fields
        private Object[] objectsToDrop;
        private Vector2 objectDropPosition;
        private string objectIdPairListString;

        // Cursor Fix 
        private bool fixCursorBug = true;

        //Copy buffer fix
        private string previousCopyBuffer;

        private Event currentEvent;

        private int frame = 0;
        #endregion 

        public override void OnInspectorGUI()
        {
            Debug.Log("OnInspectorGUI");
            frame++;
            
            currentEvent = new Event(Event.current);

            Readme readmeTarget = (Readme)target;
            if (readmeTarget != null)
            {
                readme = readmeTarget;
                ActiveReadmeEditor = this;
            }

            readme.Initialize();
            readme.ConnectManager();
            readme.UpdateSettings(ReadmeSettings.GetPath(this));

            liteEditor = readme.ActiveSettings.lite;
            UpdateGuiStyles(readme);
            UpdateTackIcon(readme);

            #region Editor GUI

            UpdateAvailableWidth();
            StopInvalidTextAreaEvents();
            EditorGuiTextAreaObjectFields();
            
            if (!editing)
            {
                //Text Area
                if (readme.Text.IsEmpty())
                {
                    if (readme.readonlyMode && readme.ActiveSettings.redistributable)
                    {
                        //TODO this doesn't make sense. People should be able to see the content here right???
                        string message = "You are using the readonly version of Readme. If you'd like to create and " +
                                         "edit readme files you can purchase a copy of Readme from the Unity Asset" +
                                         "Store.";
                        string website = "https://assetstore.unity.com/packages/slug/152336";
                        EditorGUILayout.HelpBox(message, MessageType.Warning);
                        EditorGUILayout.SelectableLabel(website, GUILayout.Height(GetTextAreaSize(website).y));
                    }
                    else
                    {
                        string content = "Click \"Edit\" to add your readme!";
                        EditorGuiTextArea(editing, content, textAreaEmptyName, emptyRichText, false);
                    }
                }
                else
                {
                    string content = !TagsError(RichText) ? RichText : readme.Text;
                    EditorGuiTextArea(editing, content, textAreaReadonlyName, selectableRichText);
                }

                //Controls
                if (!readme.readonlyMode || Readme.disableAllReadonlyMode)
                {
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Edit"))
                    {
                        editing = true;
                        NewRepaint(focusText:true);
                    }

                    if (Event.current.type == EventType.Repaint)
                    {
                        doneEditButtonRect = GUILayoutUtility.GetLastRect();
                    }

                    if (IsPrefab(readme.gameObject))
                    {
                        if (GUILayout.Button("Export To PDF"))
                        {
                            ExportToPdf();
                        }
                    }
                }
            }
            else
            {
                //Text Area
                if (sourceOn)
                {
                    EditorGuiTextArea(editing, RichText, textAreaSourceName, editableText);
                }
                else
                {
                    EditorGuiTextArea(editing, RichText, textAreaStyleName, editableRichText);
                }

                if (TagsError(RichText))
                {
                    EditorGUILayout.HelpBox("Rich text error detected. Check for mismatched tags.",
                        MessageType.Warning);
                }

                if (selectIndexChanged || cursorIndexChanged)
                {
                    UpdateStyleState();
                }

                //Controls
                EditorGUILayout.Space();
                if (GUILayout.Button("Done"))
                {
                    editing = false;
                    NewRepaint(focusText:true);
                }
                
                if (Event.current.type == EventType.Repaint)
                {
                    doneEditButtonRect = GUILayoutUtility.GetLastRect();
                }

                EditorGuiToolbar();
            }

            EditorGuiAdvancedDropdown();

            #endregion Editor GUI

            //Post gui draw update
            UpdateTextEditor();
            EditorGuiTextAreaObjectFields();
            DragAndDropObjectField();
            CheckKeyboardShortCuts();
            FixCursorBug();
            ScrollToCursor();

            NewFocus();
            NewSetCursor();
            
            if (frame < focusFrame || frame < setCursorFrame)
            {
                TriggerOnInspectorGUI();
            }

            if (!firstFocus && !ActiveText.IsEmpty())
            {
                if (textEditor == null)
                {
                    if (savedWindowFocus == "")
                    {
                        savedWindowFocus = EditorWindow.focusedWindow.titleContent.text;
                    }

                    NewFocus(true);
                }
                else
                {
                    firstFocus = true;
                    NewRepaint(0, 0, focusText: true);
                    if (savedWindowFocus != "")
                    {
                        ReadmeUtil.FocusEditorWindow(savedWindowFocus);
                        savedWindowFocus = "";
                    }
                }
            }
        }

        private int focusFrame = -1;
        private int setCursorFrame = -1;
        private (int, int) savedCursor;
        private bool firstFocus = false;
        private string savedWindowFocus = "";

        private void NewRepaint(int newCursorIndex = -1, int newSelectIndex = -1, bool focusText = false)
        {
            TriggerOnInspectorGUI();
            SetText(ActiveText);
            SetCursors((newCursorIndex, newSelectIndex));
            
            bool textAlreadyFocused = textEditor != null && GetControlId(ExpectedTextAreaName()) == textEditor.controlID;
            if (focusText && !textAlreadyFocused)
            {
                focusFrame = frame + 2;
                setCursorFrame = frame + 4;
                savedCursor = GetCursors();
            }
        }

        private void NewFocus(bool force = false)
        {
            if (frame == focusFrame || force)
            {
                ReadmeUtil.FocusEditorWindow("Inspector");
                EditorGUI.FocusTextInControl(activeTextAreaName);
                GUI.FocusControl(activeTextAreaName);
            }
        }

        private void NewSetCursor()
        {
            if (frame == setCursorFrame)
            {
                if (textEditor != null)
                {
                    SetCursors(savedCursor);
                 
                    savedCursor = (-1, -1);
                }
            }
        }

        private void SetText(string text)
        {
            if (textEditor != null)
            {
                textEditor.text = text;
            }
        }

        private void SetCursors((int, int) cursors)
        {
            if (textEditor != null)
            {
                (int cursorIndex, int selectIndex) = cursors;
                if (cursorIndex != -1)
                {
                    textEditor.cursorIndex = cursorIndex;
                }

                if (selectIndex != -1)
                {
                    textEditor.selectIndex = selectIndex;
                }
            }
        }

        private (int, int)  GetCursors()
        {
            return textEditor == null ? (-1, -1) : (textEditor.cursorIndex, textEditor.selectIndex);
        }

        private void TriggerOnInspectorGUI()
        {
            EditorUtility.SetDirty(readme); //Trigger OnInspectorGUI() call
        }

        private string ExpectedTextAreaName()
        {
            string expectedTextAreaName = "";
            if (sourceOn) { expectedTextAreaName = textAreaSourceName; }
            else if (!editing) { expectedTextAreaName = textAreaReadonlyName; }
            else if (!RichText.IsEmpty()) { expectedTextAreaName = textAreaStyleName; }
            if (sourceOn) { expectedTextAreaName = textAreaEmptyName; }

            return expectedTextAreaName;
        }
        
        private void EditorGuiTextArea(bool canEdit, string content, string controlName, GUIStyle style, bool selectable=true)
        {
            activeTextAreaName = controlName;
            activeTextAreaStyle = style;

            Vector2 size = GetTextAreaSize(content);
            GUILayoutOption[] options = new[] { GUILayout.Width(size.x), GUILayout.Height(size.y) };
            bool scrollShowing = scrollEnabled && size.y + scrollAreaPad > scrollMaxHeight;
            Vector2 scrollAreaSize = new Vector2(size.x + scrollAreaPad, size.y + scrollAreaPad);
            if (scrollShowing)
            {
                scrollAreaSize.x += scrollBarSize;
                scrollAreaSize.y = scrollMaxHeight;
            }

            GUILayoutOption[] scrollAreaOptions = new[]
                { GUILayout.Width(scrollAreaSize.x), GUILayout.Height(scrollAreaSize.y) };

            scroll = EditorGUILayout.BeginScrollView(scroll, scrollAreaOptions);
            AddControl("scroll", GetLastControlId);
            GUI.SetNextControlName(controlName);
            if (canEdit)
            {
                PrepareForTextAreaChange(RichText);
                string oldRichText = RichText;
                RichText = EditorGUILayout.TextArea(content, style, options);
                
                if (oldRichText != RichText)
                {
                    SetTargetDirty();
                    int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 0;
                    CursorIndex = GetNearestPoorTextIndex(CursorIndex, -direction);
                    SelectIndex = GetNearestPoorTextIndex(SelectIndex, -direction);
                }
            }
            else
            {
                if (selectable)
                {
                    EditorGUILayout.SelectableLabel(content, style, options);
                }
                else
                {
                    EditorGUILayout.LabelField(content, style, options);
                }
            }

            AddControl(controlName, GetLastControlId);
            activeTextAreaControlId = GetLastControlId;
            textAreaRect = GetLastRect(textAreaRect);
            EditorGUILayout.EndScrollView();
            scrollAreaRect = GetLastRect(scrollAreaRect);
        }
        
        private void EditorGuiTextAreaObjectFields()
        {
            if (RichText != null)
            {
                UpdateTextAreaObjectFieldArray();
                DrawTextAreaObjectFields();
                UpdateTextAreaObjectFieldIds();
            }

            void UpdateTextAreaObjectFieldArray()
            {
                if (textEditor != null)
                {
                    string objectTagPattern = "<o=\"[-,a-zA-Z0-9]*\"></o>";
                    MatchCollection matches = Regex.Matches(RichText, objectTagPattern, RegexOptions.None);
                    TextAreaObjectField[] newTextAreaObjectFields = new TextAreaObjectField[matches.Count];
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        Match match = matches[i];

                        if (match.Success)
                        {
                            string idValue = match.Value.Replace("<o=\"", "").Replace("\"></o>", "");
                            int objectId = 0;
                            bool parseSuccess = int.TryParse(idValue, out objectId);

                            if (!parseSuccess && verbose)
                            {
                                Debug.Log("Unable to parse id: " + idValue);
                            }

                            int startIndex = match.Index;
                            int endIndex = match.Index + match.Value.Length;

                            if (endIndex == RichText.Length || RichText[endIndex] != ' ')
                            {
                                RichText = RichText.Insert(endIndex, " ");
                            }

                            if (startIndex == 0 || RichText[startIndex - 1] != ' ')
                            {
                                RichText = RichText.Insert(startIndex, " ");
                            }

                            Rect rect = GetRect(startIndex - 1, endIndex + 1);
                            rect.position += textAreaRect.position;

                            Rect rectWithCorrectHeight = GetRect(startIndex - 1, endIndex); // Have to do this for when a space is moved to the next line.
                            rect.height = rectWithCorrectHeight.height;
                            rect.width = rectWithCorrectHeight.width;

                            if (rect.x > 0 && rect.y > 0 && rect.width > 0 && rect.height > 0)
                            {
                                TextAreaObjectField matchedField =
                                    TextAreaObjectFields.FirstOrDefault(item => item.ObjectId == objectId);
                                if (matchedField != null && !matchedField.IdInSync)
                                {
                                    matchedField.UpdateId();
                                    objectId = matchedField.ObjectId;

                                    int idStartIndex = match.Index + 4;
                                    RichText = RichText
                                        .Remove(idStartIndex, idValue.Length)
                                        .Insert(idStartIndex, ReadmeUtil.GetFixedLengthId(objectId.ToString()));
                                }

                                if (matchedField != null && !matchedField.IdInSync)
                                {
                                    ReadmeManager.AddObjectIdPair(matchedField.ObjectRef, objectId);
                                }

                                TextAreaObjectField newField = new TextAreaObjectField(rect, objectId, startIndex,
                                    endIndex - startIndex);
                                newTextAreaObjectFields[i] = newField;
                                newTextAreaObjectFields[i].OnChangeHandler += SetTargetDirty;
                            }
                            else
                            {
                                return; //Abort everything. Position is incorrect! Probably no textEditor found.
                            }
                        }
                    }

                    if (!TextAreaObjectFields.SequenceEqual(newTextAreaObjectFields))
                    {
                        TextAreaObjectFields = newTextAreaObjectFields;
                    }
                }
            }

            void DrawTextAreaObjectFields()
            {
                if (!sourceOn || !editing)
                {
                    Vector2 offset = -scroll - textAreaRect.position;
                    EditorGUI.BeginDisabledGroup(!editing);
                    foreach (TextAreaObjectField textAreaObjectField in TextAreaObjectFields)
                    {
                        textAreaObjectField.Draw(textEditor, offset, scrollAreaRect);
                    }

                    EditorGUI.EndDisabledGroup();
                }
            }

            void UpdateTextAreaObjectFieldIds()
            {
                StringBuilder newRichText = new StringBuilder(RichText);
                string objectTagPattern = "<o=\"[-,a-zA-Z0-9]*\"></o>";
                int startTagLength = "<o=\"".Length;
                int endTagLength = "\"></o>".Length;
                int expectedFieldCount = Regex.Matches(RichText, "<o=\"[-,a-zA-Z0-9]*\"></o>", RegexOptions.None).Count;

                if (expectedFieldCount != TextAreaObjectFields.Length)
                {
                    return;
                }

                for (int i = TextAreaObjectFields.Length - 1; i >= 0; i--)
                {
                    TextAreaObjectField textAreaObjectField = TextAreaObjectFields[i];

                    if (RichText.Length > textAreaObjectField.Index)
                    {
                        Match match =
                            Regex.Match(RichText.Substring(Mathf.Max(0, textAreaObjectField.Index - 1)),
                                objectTagPattern, RegexOptions.None);

                        if (match.Success)
                        {
                            string textAreaId =
                                ReadmeUtil.GetFixedLengthId(match.Value.Replace("<o=\"", "").Replace("\"></o>", ""));
                            string objectFieldId = ReadmeUtil.GetFixedLengthId(textAreaObjectField.ObjectId.ToString());

                            if (textAreaId != objectFieldId)
                            {
                                int idStartIndex = textAreaObjectField.Index + match.Index + startTagLength;
                                newRichText.Remove(idStartIndex - 1, textAreaId.Length);
                                newRichText.Insert(idStartIndex - 1, objectFieldId);
                            }
                        }
                    }
                }

                RichText = newRichText.ToString();
            }
        }
        
        private void EditorGuiToolbar()
        {
            float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;
            GUILayout.BeginHorizontal();
            SetFontColor(EditorGUILayout.ColorField(readme.fontColor, GUILayout.Width(smallButtonWidth)));
            SetFontStyle(EditorGUILayout.ObjectField(readme.font, typeof(Font), true) as Font);

            string[] options = new string[]
            {
                "8", "9", "10", "11", "12", "14", "16", "18", "20", "22", "24", "26", "28", "36", "48", "72"
            };
            int selected = options.ToList().IndexOf(readme.fontSize.ToString());
            if (selected == -1)
            {
                selected = 4;
            }

            selected = EditorGUILayout.Popup(selected, options, GUILayout.Width(smallButtonWidth));
            int fontSize = int.Parse(options[selected]);
            SetFontSize(fontSize);

            GUIStyle boldButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            boldButtonStyle.fontStyle = FontStyle.Normal;
            if (boldOn)
            {
                boldButtonStyle.fontStyle = FontStyle.Bold;
            }

            if (GUILayout.Button(new GUIContent("B", "Bold (alt+b)"), boldButtonStyle,
                    GUILayout.Width(smallButtonWidth)))
            {
                ToggleStyle("b");
            }

            GUIStyle italicizedButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            italicizedButtonStyle.fontStyle = FontStyle.Normal;
            if (italicOn)
            {
                italicizedButtonStyle.fontStyle = FontStyle.Italic;
            }

            if (GUILayout.Button(new GUIContent("I", "Italic (alt+i)"), italicizedButtonStyle,
                    GUILayout.Width(smallButtonWidth)))
            {
                ToggleStyle("i");
            }

            GUIStyle objButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            if (GUILayout.Button(new GUIContent("Obj", "Insert Object Field (alt+o)"), objButtonStyle,
                    GUILayout.Width(smallButtonWidth)))
            {
                AddObjectField();
            }

            GUIStyle sourceButtonStyle = new GUIStyle(EditorStyles.toolbarButton);
            sourceButtonStyle.fontStyle = FontStyle.Normal;
            GUIContent sourceButtonContent = new GUIContent("</>", "View Source");
            if (sourceOn)
            {
                sourceButtonContent = new GUIContent("Abc", "View Style");
            }

            if (GUILayout.Button(sourceButtonContent, sourceButtonStyle, GUILayout.Width(smallButtonWidth)))
            {
                sourceOn = !sourceOn;
            }

            GUILayout.EndHorizontal();

            if (sourceOn)
            {
                EditorGUILayout.HelpBox("Source mode enabled! Supported tags:\n" +
                                        " <b></b>\n" +
                                        " <i></i>\n" +
                                        " <color=\"#00ffff\"></color>\n" +
                                        " <size=\"20\"></size>\n" +
                                        " <o=\"0000001\"></o>",
                    MessageType.Info);
            }
        }

        private void EditorGuiAdvancedDropdown()
        {
            float smallButtonWidth = EditorGUIUtility.singleLineHeight * 2;
            float textAreaWidth = GetTextAreaSize().x;

            if (editing || showAdvancedOptions)
            {
                showAdvancedOptions = EditorGUILayout.Foldout(showAdvancedOptions, "Advanced");
            }

            if (showAdvancedOptions)
            {
                EditorGUI.indentLevel++;
                GUI.enabled = false;
                SerializedProperty prop = serializedObject.FindProperty("m_Script");
                EditorGUILayout.PropertyField(prop, true, new GUILayoutOption[0]);
                GUI.enabled = true;

                GUIContent fixCursorBugTooltip = new GUIContent(
                    "Cursor Correction",
                    "Override Unity text box cursor placement.");
                fixCursorBug = EditorGUILayout.Toggle(fixCursorBugTooltip, fixCursorBug);
                verbose = EditorGUILayout.Toggle("Verbose", verbose);
                readme.useTackIcon = EditorGUILayout.Toggle("Use Tack Gizmo", readme.useTackIcon);
                Readme.neverUseTackIcon = EditorGUILayout.Toggle("Never Use Tack Gizmo", Readme.neverUseTackIcon);
                readme.readonlyMode = EditorGUILayout.Toggle("Readonly Mode", readme.readonlyMode);
                GUIContent disableAllReadonlyModeTooltip = new GUIContent(
                    "Disable All Readonly Mode",
                    "Global setting to enable editing without changing each readonly readme.");
                Readme.disableAllReadonlyMode =
                    EditorGUILayout.Toggle(disableAllReadonlyModeTooltip, Readme.disableAllReadonlyMode);

                showCursorPosition = EditorGUILayout.Foldout(showCursorPosition, "Cursor Position");
                if (showCursorPosition)
                {
                    EditorGUI.indentLevel++;
                    string richTextWithCursor = RichText;
                    if (TextEditorActive && SelectIndex <= RichText.Length)
                    {
                        richTextWithCursor = richTextWithCursor.Insert(Mathf.Max(SelectIndex, CursorIndex), "|");
                        if (SelectIndex != CursorIndex)
                        {
                            richTextWithCursor = richTextWithCursor.Insert(Mathf.Min(SelectIndex, CursorIndex), "|");
                        }
                    }

                    richTextWithCursor = richTextWithCursor.Replace("\n", " \\n\n");
                    float adjustedTextAreaHeight =
                        editableText.CalcHeight(new GUIContent(richTextWithCursor), textAreaWidth - 50);
                    EditorGUILayout.SelectableLabel(richTextWithCursor, editableText,
                        GUILayout.Height(adjustedTextAreaHeight));
                    EditorGUI.indentLevel--;
                }

                showObjectIdPairs = EditorGUILayout.Foldout(showObjectIdPairs, "Master Object Field Dictionary");
                if (showObjectIdPairs)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Toggle("Manager Connected", readme.managerConnected);
                    EditorGUILayout.LabelField("Object Id Pairs");
                    float objectDictHeight =
                        editableText.CalcHeight(new GUIContent(objectIdPairListString), textAreaWidth - 50);
                    EditorGUILayout.LabelField(objectIdPairListString, editableText,
                        GUILayout.Height(objectDictHeight));
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Refresh Pairs", GUILayout.Width(smallButtonWidth * 4)) ||
                        objectIdPairListString == null)
                    {
                        objectIdPairListString = ReadmeManager.GetObjectIdPairListString();
                        NewRepaint();
                    }

                    if (GUILayout.Button("Clear Pairs", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        ReadmeManager.Clear();
                        NewRepaint();
                    }

                    GUILayout.EndHorizontal();
                    EditorGUI.indentLevel--;
                }

                showDebugInfo = EditorGUILayout.Foldout(showDebugInfo, "Debug Info");
                if (showDebugInfo)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.HelpBox(
                        "mousePosition: " + Event.current.mousePosition + "\n" +
                        "FocusedWindow: " + EditorWindow.focusedWindow + "\n" +
                        "mouseOverWindow: " + EditorWindow.mouseOverWindow + "\n" +
                        "FocusedControl: " + GUI.GetNameOfFocusedControl() + "\n" +
                        "Event Type: " + Event.current.ToString() + "\n" +
                        "textAreaRect: " + textAreaRect + "\n" +
                        "scrollAreaRect: " + scrollAreaRect + "\n" +
                        "scroll: " + scroll + "\n" +
                        "Calc Cursor Position: " + (Event.current.mousePosition - textAreaRect.position) + "\n" +
                        "Text Editor Active: " + TextEditorActive + "\n" +
                        "cursorIndex: " + (!TextEditorActive ? "" : CursorIndex.ToString()) + "\n" +
                        "selectIndex: " + (!TextEditorActive ? "" : SelectIndex.ToString()) + "\n" +
                        "cursorIndex OnTag: " + IsOnTag(CursorIndex) + "\n" +
                        "selectIndex OnTag: " + IsOnTag(SelectIndex) + "\n" +
                        "TagsError: " + TagsError(RichText) + "\n" +
                        "Style Map Info: " + "\n" +
                        "\t<b> tags:" + (readme.StyleMaps.ContainsKey("b")
                            ? readme.StyleMaps["b"].FindAll(isStyle => isStyle).Count.ToString()
                            : "0") + "\n" +
                        "\t<i> tags:" + (readme.StyleMaps.ContainsKey("i")
                            ? readme.StyleMaps["i"].FindAll(isStyle => isStyle).Count.ToString()
                            : "0") + "\n" +
                        ""
                        , MessageType.Info);

                    MessageType messageType = textEditor != null ? MessageType.Info : MessageType.Warning;

                    EditorGUILayout.HelpBox(
                        "Toggle Bold: alt+b\n" +
                        "Toggle Italic: alt+i\n" +
                        "Add Object: alt+o\n" +
                        "Show Advanced Options: alt+a\n"
                        , MessageType.Info);

                    if (textEditor != null)
                    {
                        EditorGUILayout.HelpBox(
                            "ControlIds" +
                            "\n\t" + "scrollArea: " + GetControlId("scroll") +
                            "\n\t" + "textAreaEmpty: " + GetControlId(textAreaEmptyName) +
                            "\n\t" + "textAreaReadonly: " + GetControlId(textAreaReadonlyName) +
                            "\n\t" + "textAreaSource: " + GetControlId(textAreaSourceName) +
                            "\n\t" + "textAreaStyle: " + GetControlId(textAreaStyleName)
                            , MessageType.Info);

                        EditorGUILayout.HelpBox(
                            "TEXT EDITOR VALUES" +
                            // "\n\t" + "text: " + textEditor.text +
                            // "\n\t" + "SelectedText: " + textEditor.SelectedText +
                            "\n\t" + "multiline: " + textEditor.multiline +
                            "\n\t" + "position: " + textEditor.position +
                            "\n\t" + "style: " + textEditor.style +
                            "\n\t" + "cursorIndex: " + textEditor.cursorIndex +
                            "\n\t" + "hasSelection: " + textEditor.hasSelection +
                            "\n\t" + "scrollOffset: " + textEditor.scrollOffset +
                            "\n\t" + "selectIndex: " + textEditor.selectIndex +
                            "\n\t" + "altCursorPosition: " + textEditor.altCursorPosition +
                            "\n\t" + "controlID: " + GetControlName(textEditor.controlID) +
                            "\n\t" + "controlID_Event: " + Event.current.GetTypeForControl(textEditor.controlID) +
                            "\n\t" + "doubleClickSnapping: " + textEditor.doubleClickSnapping +
                            "\n\t" + "graphicalCursorPos: " + textEditor.graphicalCursorPos +
                            "\n\t" + "isPasswordField: " + textEditor.isPasswordField +
                            "\n\t" + "isPasswordField: " + textEditor.isPasswordField +
                            "\n\t" + "keyboardOnScreen: " + textEditor.keyboardOnScreen +
                            "\n\t" + "graphicalSelectCursorPos: " + textEditor.graphicalSelectCursorPos +
                            "\n\t" + "hasHorizontalCursorPos: " + textEditor.hasHorizontalCursorPos
                            , MessageType.Info);

                        EditorGUILayout.HelpBox(
                            "GUIUtility" +
                            "\n\t" + "hotControl: " + GUIUtility.hotControl +
                            "\n\t" + "keyboardControl: " + GUIUtility.keyboardControl +
                            "\n\t" + "GetStateObject: " +
                            GUIUtility.GetStateObject(typeof(TextEditor), textEditor.controlID) +
                            "\n\t" + "QueryStateObject: " +
                            GUIUtility.QueryStateObject(typeof(TextEditor), textEditor.controlID)
                            , MessageType.Info);

                        EditorGUILayout.HelpBox(
                            "EditorGUIUtility : GUIUtility" +
                            "\n\t" + "textFieldHasSelection: " + EditorGUIUtility.textFieldHasSelection +
                            "\n\t" + "s_LastControlID: " + GetLastControlId
                            , MessageType.Info);

                        EditorGUILayout.HelpBox(
                            "EditorUtility" +
                            "\n\t" + "IsDirty: " + EditorUtility.IsDirty(readme) +
                            "\n\t" + "GetDirtyCount: " + EditorUtility.GetDirtyCount(readme)
                            , MessageType.Info);


                        EditorGUILayout.HelpBox(
                            "GUI" +
                            "\n\t" + "GetNameOfFocusedControl: " + GUI.GetNameOfFocusedControl()
                            , MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No textEditor Found", MessageType.Warning);
                    }


                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("Save to File", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        readme.Save();
                    }

                    if (GUILayout.Button("Load from File", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        readme.LoadLastSave();
                        Repaint();
                    }

                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button("New Settings File", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        ReadmeSettings newSettings = new ReadmeSettings(ReadmeSettings.GetPath(this));
                        newSettings.SaveSettings();
                        Repaint();
                    }

                    if (GUILayout.Button("Reload Settings", GUILayout.Width(smallButtonWidth * 4)))
                    {
                        readme.UpdateSettings(ReadmeSettings.GetPath(this), true, verbose);
                        Repaint();
                    }

                    Readme.overrideSettings =
                        EditorGUILayout.ObjectField(Readme.overrideSettings, typeof(Object), false);

                    GUILayout.EndHorizontal();

                    showDebugButtons = EditorGUILayout.Foldout(showDebugButtons, "Debug Buttons");
                    if (showDebugButtons)
                    {
                        float debugButtonWidth = smallButtonWidth * 6;

                        if (GUILayout.Button("TestHtmlAgilityPack", GUILayout.Width(debugButtonWidth)))
                        {
                            TestHtmlAgilityPack();
                        }

                        if (GUILayout.Button("Repaint", GUILayout.Width(debugButtonWidth)))
                        {
                            NewRepaint();
                        }

                        if (GUILayout.Button("GUI.FocusControl", GUILayout.Width(debugButtonWidth)))
                        {
                            GUI.FocusControl(activeTextAreaName); //readme_text_editor_style
                        }

                        if (GUILayout.Button("OnGui", GUILayout.Width(debugButtonWidth)))
                        {
                            EditorUtility.SetDirty(readme.gameObject);
                        }

                        if (GUILayout.Button("AssignTextEditor", GUILayout.Width(debugButtonWidth)))
                        {
                            UpdateTextEditor();
                        }

                        if (GUILayout.Button("SetDirty", GUILayout.Width(debugButtonWidth)))
                        {
                            EditorUtility.SetDirty(readme);
                        }

                        if (GUILayout.Button("Repaint", GUILayout.Width(debugButtonWidth)))
                        {
                            Repaint();
                        }

                        if (GUILayout.Button("RecordObject", GUILayout.Width(debugButtonWidth)))
                        {
                            Undo.RecordObject(readme, "Force update!");
                        }

                        if (GUILayout.Button("FocusTextInControl", GUILayout.Width(debugButtonWidth)))
                        {
                            EditorGUI.FocusTextInControl(activeTextAreaName);
                        }

                        if (GUILayout.Button("Un-FocusTextInControl", GUILayout.Width(debugButtonWidth)))
                        {
                            EditorGUI.FocusTextInControl("");
                        }
                    }

                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }
        }

        private void UpdateGuiStyles(Readme readmeTarget)
        {
            skinDefault = GUI.skin;
            skinStyle = GUI.skin;
            skinSource = GUI.skin;
            skinStyle = skinStyle != null ? skinStyle : ReadmeUtil.GetSkin(ReadmeUtil.SKIN_STYLE, this);
            skinSource = skinSource != null ? skinSource : ReadmeUtil.GetSkin( ReadmeUtil.SKIN_SOURCE, this);
            
            textAreaEmptyName = "readme_text_editor_empty_" + readme.GetInstanceID();
            textAreaReadonlyName = "readme_text_editor_readonly_" + readme.GetInstanceID();
            textAreaSourceName = "readme_text_editor_source_" + readme.GetInstanceID();
            textAreaStyleName = "readme_text_editor_style_" + readme.GetInstanceID();

            RectOffset padding = new RectOffset(textPadding, textPadding, textPadding, textPadding);
            RectOffset margin = new RectOffset(3, 3, 3, 3);
            
            emptyRichText = new GUIStyle(skinDefault.label)
            {
                richText = true,
                focused = { textColor = Color.gray },
                normal = { textColor = Color.gray },
                font = readmeTarget.font,
                fontSize = readmeTarget.fontSize,
                wordWrap = true,
                padding = padding,
                margin = margin
            };

            selectableRichText = new GUIStyle(skinDefault.label)
            {
                richText = true,
                focused = { textColor = readmeTarget.fontColor },
                normal = { textColor = readmeTarget.fontColor },
                font = readmeTarget.font,
                fontSize = readmeTarget.fontSize,
                wordWrap = true,
                padding = padding,
                margin = margin
            };

            editableRichText = new GUIStyle(skinStyle.textArea)
            {
                richText = true,
                font = readmeTarget.font,
                fontSize = readmeTarget.fontSize,
                wordWrap = true,
                padding = padding,
                margin = margin
            };

            editableText = new GUIStyle(skinSource.textArea)
            {
                richText = false,
                wordWrap = true,
                padding = padding,
                margin = margin
            };
        }

        private void UpdateTackIcon(Readme readmeTarget)
        {
            Object selectedObject = Selection.activeObject;
            if (selectedObject != null)
            {
                if (readme.useTackIcon && !Readme.neverUseTackIcon)
                {
                    Texture2D icon =
                        AssetDatabase.LoadAssetAtPath<Texture2D>(
                            "Assets/Packages/TP/Readme/Assets/Textures/readme_icon_256_256.png");
                    IconManager.SetIcon(selectedObject as GameObject, icon);
                    readme.iconBeingUsed = true;
                }
                else if (readme.iconBeingUsed)
                {
                    IconManager.RemoveIcon(selectedObject as GameObject);
                    readme.iconBeingUsed = false;
                }
            }
        }

        private void UpdateAvailableWidth()
        {
            EditorGUILayout.Space();
            float defaultWidth = availableWidth != 0 ? availableWidth : EditorGUIUtility.currentViewWidth - 20;
            availableWidth = GetLastRect(new Rect(0, 0, defaultWidth, 0)).width;
        }

        private void StopInvalidTextAreaEvents()
        {
            //Stop button clicks from being used by text
            if (currentEvent.type == EventType.MouseDown
                && textAreaRect.Contains(currentEvent.mousePosition)
                && !scrollAreaRect.Contains(currentEvent.mousePosition))
            {
                currentEvent.Use();
            }
        }

        private Vector2 GetTextAreaSize(string text = "")
        {
            int xPadding = -10;
            Vector2 size = CalcSize(text, xPadding, 0);
            bool scrollShowing = scrollEnabled && size.y + scrollAreaPad > scrollMaxHeight;
            if (scrollShowing)
            {
                size = CalcSize(text, xPadding - scrollBarSize);
            }

            return size;

            Vector2 CalcSize(string text, float xPadding = 0, float yPadding = 0)
            {
                Vector2 size = new Vector2();
                size.x = availableWidth + xPadding;
                size.y = editableRichText.CalcHeight(new GUIContent(text), size.x) + yPadding;
                return size;
            }
        }

        private TextEditor GetPrivateTextEditor =>
            typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as TextEditor;

        private TextEditor GetTextEditor()
        {
            TextEditor privateTextEditor = GetPrivateTextEditor;

            if (privateTextEditor == null)
            {
                // EditorGUI.FocusTextInControl(activeTextAreaName);
                privateTextEditor = GetPrivateTextEditor;
            }

            return privateTextEditor;
        }

        private void UpdateTextEditor()
        {
            if (readme.Text.Length > 0 || editing)
            {
                TextEditor newTextEditor = GetTextEditor();

                if (newTextEditor != null && textEditor != newTextEditor)
                {
                    textEditor = newTextEditor;

                    if (verbose)
                    {
                        Debug.Log("README: Text Editor assigned!");
                    }
                }
            }
        }

        private void SetTargetDirty()
        {
            if (!Application.isPlaying)
            {
                Undo.RegisterCompleteObjectUndo(readme, "Readme edited");

                if (IsPrefab(readme.gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(readme.gameObject);
                }
            }
        }

        private void ShowLiteVersionDialog(string feature = "This")
        {
            string title = "Paid Feature Only";
            string message = feature +
                             " is a paid feature. To use this feature please purchase a copy of Readme from the Unity Asset Store.";
            string ok = "Go to Asset Store";
            string cancel = "Nevermind";
            bool result = EditorUtility.DisplayDialog(title, message, ok, cancel);

            if (result)
            {
                Application.OpenURL("https://assetstore.unity.com/packages/slug/152336");
            }
        }

        private void DragAndDropObjectField()
        {
            if (editing)
            {
                switch (currentEvent.type)
                {
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        if (!scrollAreaRect.Contains(currentEvent.mousePosition))
                        {
                            return; // Ignore drag and drop outside of textArea
                        }

                        foreach (TextAreaObjectField textAreaObjectField in TextAreaObjectFields)
                        {
                            if (textAreaObjectField.FieldRect.Contains(currentEvent.mousePosition))
                            {
                                return; // Ignore drag and drop over current Object Fields
                            }
                        }

                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                        if (currentEvent.type == EventType.DragPerform && objectsToDrop == null)
                        {
                            DragAndDrop.AcceptDrag();

                            objectsToDrop = DragAndDrop.objectReferences;
                            objectDropPosition = currentEvent.mousePosition;
                        }

                        break;
                }

                if (objectsToDrop != null && textEditor != null)
                {
                    int dropIndex = PositionToIndex(objectDropPosition);
                    if (dropIndex == -1) //dropped on last line away from last character.
                    {
                        dropIndex = ActiveText.Length;
                    }
                    dropIndex = GetNearestPoorTextIndex(dropIndex);
                    InsertObjectFields(objectsToDrop, dropIndex);
                    objectsToDrop = null;
                    objectDropPosition = Vector2.zero;
                    Undo.RecordObject(readme, "object field added");
                }
            }
        }

        private void InsertObjectFields(Object[] objects, int index)
        {
            if (liteEditor)
            {
                ShowLiteVersionDialog("Dragging and dropping object fields");
                return;
            }

            for (int i = objects.Length - 1; i >= 0; i--)
            {
                Object objectDragged = objects[i];

                AddObjectField(index, ReadmeManager.GetIdFromObject(objectDragged).ToString());
            }
        }

        private void AddObjectField(int index = -1, string id = "0000000")
        {
            if (textEditor != null)
            {
                if (index == -1)
                {
                    index = CursorIndex;
                }

                string objectString = " <o=\"" + ReadmeUtil.GetFixedLengthId(id) + "\"></o> ";
                RichText = RichText.Insert(index, objectString);

                int newIndex = GetNearestPoorTextIndex(index + objectString.Length);
                NewRepaint(newIndex, newIndex, true);

                SetTargetDirty();
            }
        }

        private Rect GetRect(int startIndex, int endIndex)
        {
            Rect textEditorRect = textEditor != null ? textEditor.position : new Rect();

            if (textEditor != null && textEditor.text != readme.RichText && TextEditorActive)
            {
                Debug.Log("TestEditor text out of sync. Forcing text update.");
                textEditor.text = readme.RichText;
            }

            int textSize = 12; //Todo get size from size map
            float padding = 1;
            string sizeWrapper = "<size={0}>{1}</size>";

            Vector2 startPositionIndex1 = GetGraphicalCursorPos(startIndex);
            Vector2 startPositionIndex2 = GetGraphicalCursorPos(startIndex + 1);
            Vector2 startPosition;

            if (startPositionIndex1.y != startPositionIndex2.y)
            {
                startPosition = startPositionIndex2 + new Vector2(padding, 0);
            }
            else
            {
                startPosition = startPositionIndex1 + new Vector2(padding, 0);
            }

            Vector2 endPosition = GetGraphicalCursorPos(endIndex) + new Vector2(-padding, 0);
            float height = editableRichText.CalcHeight(new GUIContent(string.Format(sizeWrapper, textSize, " ")), 100) -
                           10;

            if (startPosition.y != endPosition.y)
            {
                endPosition.x = textEditorRect.xMax - 20;
            }

            endPosition.y += height;

            Vector2 size = endPosition - startPosition;

            Rect rect = new Rect(startPosition, size);

            return rect;
        }

        //TODO Yeet this whole function?
        private void PrepareForTextAreaChange(string input)
        {
            if (!TagsError(input))
            {
                //TODO probably dont need this
                if (SelectIndex == 0 && CursorIndex == 0 &&
                    (currentCursorIndex != CursorIndex || currentSelectIndex != SelectIndex))
                {
                    if (!currentEvent.isMouse && !currentEvent.isKey)
                    {
                        SelectIndex = currentSelectIndex;
                        CursorIndex = currentCursorIndex;
                    }
                }

                if (currentEvent.type == EventType.KeyDown &&
                    new KeyCode[] { KeyCode.Backspace, KeyCode.Delete }.Contains(currentEvent.keyCode) &&
                    CursorIndex == SelectIndex)
                {
                    int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 0;
                    int charIndex = CursorIndex + direction;
                    string objTag = direction == 0 ? " <o=" : "</o> ";
                    int objTagStart = direction == 0 ? charIndex : charIndex - 4;
                    int objTagLength = objTag.Length;
                    bool objectField = objTagStart > 0 &&
                                       objTagStart + objTagLength <= input.Length &&
                                       input.Substring(objTagStart, objTagLength) == objTag;

                    if (objectField)
                    {
                        int nextPoorIndex = GetNearestPoorTextIndex(charIndex + (direction == 0 ? 1 : 0), direction);
                        bool poorCharFound = (nextPoorIndex - charIndex) * (direction == 0 ? 1 : -1) > 0;

                        if (!poorCharFound)
                        {
                            nextPoorIndex = 0;
                        }

                        SelectIndex = nextPoorIndex;
                        EndIndex -= 1;
                        Event.current.Use();
                    }
                    else
                    {
                        if (charIndex < 0 || CursorIndex > RichText.Length)
                        {
                            int newIndex = GetNearestPoorTextIndex(charIndex - direction);
                            CursorIndex = newIndex;
                            SelectIndex = newIndex;
                            Event.current.Use();
                        }
                        else if (IsOnTag(charIndex))
                        {
                            CursorIndex += direction == 1 ? 1 : -1;
                            SelectIndex += direction == 1 ? 1 : -1;

                            PrepareForTextAreaChange(input);
                        }
                    }
                }
            }
        }

        private void CheckKeyboardShortCuts()
        {
            //Alt + a for toggle advanced mode
            if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.A)
            {
                showAdvancedOptions = !showAdvancedOptions;
                Event.current.Use();
                Repaint();
            }

            if (editing)
            {
                //Alt + b for bold
                if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.B)
                {
                    ToggleStyle("b");
                    Event.current.Use();
                }

                //Alt + i for italic
                if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.I)
                {
                    ToggleStyle("i");
                    Event.current.Use();
                }

                //Alt + o for object
                if (currentEvent.type == EventType.KeyDown && currentEvent.alt && currentEvent.keyCode == KeyCode.O)
                {
                    AddObjectField();
                    Event.current.Use();
                }
            }

            //Ctrl + v for paste
            if (currentEvent.type == EventType.KeyDown && currentEvent.control && currentEvent.keyCode == KeyCode.V)
            {
                //TODO review why this is empty
            }
        }

        private void SetFontColor(Color color)
        {
            if (color == readme.fontColor)
            {
                return;
            }

            if (liteEditor)
            {
                ShowLiteVersionDialog("Setting the font color");
                return;
            }

            readme.fontColor = color;
        }

        private void SetFontStyle(Font font)
        {
            if (font == readme.font)
            {
                return;
            }

            if (liteEditor)
            {
                ShowLiteVersionDialog("Setting the font style");
                return;
            }

            readme.font = font;
        }

        private void SetFontSize(int size)
        {
            if (readme.fontSize != 0)
            {
                if (size == readme.fontSize)
                {
                    return;
                }

                if (liteEditor)
                {
                    ShowLiteVersionDialog("Setting the font size");
                    return;
                }
            }

            readme.fontSize = size;
        }

        private void ToggleStyle(string tag)
        {
            if (liteEditor)
            {
                ShowLiteVersionDialog("Rich text shortcuts");
                return;
            }

            if (TagsError(RichText))
            {
                Debug.LogWarning("Please fix any mismatched tags first!");
                return;
            }

            if (TextEditorActive)
            {
                int styleStartIndex = readme.GetPoorIndex(StartIndex);
                int styleEndIndex = readme.GetPoorIndex(EndIndex);
                int poorStyleLength = styleEndIndex - styleStartIndex;

                readme.ToggleStyle(tag, styleStartIndex, poorStyleLength);

                if (TagsError(RichText))
                {
                    readme.LoadLastSave(); //TODO this is sketchy. Probs should not auto load.
                    Debug.LogWarning("You can't do that!");
                }

                UpdateStyleState();

                int newCursorIndex = GetNearestPoorTextIndex(readme.GetRichIndex(styleStartIndex+1)-1);
                int newSelectIndex = GetNearestPoorTextIndex(readme.GetRichIndex(styleEndIndex+1)-1);

                NewRepaint(newCursorIndex, newSelectIndex);
            }
        }

        private void UpdateStyleState()
        {
            if (TextEditorActive)
            {
                int index = 0;
                int poorCursorIndex = readme.GetPoorIndex(CursorIndex);
                int poorSelectIndex = readme.GetPoorIndex(SelectIndex);

                if (poorSelectIndex != poorCursorIndex)
                {
                    index = Mathf.Max(poorCursorIndex, poorSelectIndex) - 1;
                }
                else
                {
                    index = poorCursorIndex;
                }

                boldOn = readme.IsStyle("b", index);
                italicOn = readme.IsStyle("i", index);
            }
        }

        private void ExportToPdf()
        {
            if (liteEditor)
            {
                ShowLiteVersionDialog("Export to PDF");
                return;
            }

            EditorGuiTextAreaObjectFields();
            string currentPath = AssetDatabase.GetAssetPath(readme);
            string pdfSavePath = EditorUtility.SaveFilePanel(
                "Save Readme",
                Path.GetDirectoryName(currentPath),
                readme.gameObject.name + ".pdf",
                "pdf");

            if (pdfSavePath != "")
            {
                PdfDocument pdf = PdfGenerator.GeneratePdf(readme.HtmlText, PageSize.A4);
                pdf.Save(pdfSavePath);
                AssetDatabase.Refresh();
            }
        }

        private void FixCursorBug()
        {
            if (fixCursorBug && TextEditorActive && !TagsError(RichText) && !sourceOn)
            {
                editorSelectIndexChanged = currentSelectIndex != SelectIndex;
                editorCursorIndexChanged = currentCursorIndex != CursorIndex;

                if (!AllTextSelected())
                {
                    FixMouseCursor();
                }

                FixArrowCursor();
            }

            richTextChanged = false;
        }

        private void ScrollToCursor()
        {
            if (TextEditorActive)
            {
                if ((currentEvent.isKey || currentEvent.isMouse) && !AllTextSelected())
                {
                    int index = GetCursors().Item1;
                    Rect cursorRect = GetRect(index, index);
                    cursorRect.position -= scroll;
                    if (cursorRect.yMax > scrollAreaRect.yMax)
                    {
                        scroll.y += cursorRect.height;
                    }

                    if (cursorRect.yMin < scrollAreaRect.yMin)
                    {
                        scroll.y -= cursorRect.height;
                    }
                }
            }
        }

        private void FixMouseCursor()
        {
            bool mouseEvent =
                new EventType[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp }.Contains(currentEvent
                    .type);

            if (currentEvent.type == EventType.MouseDown && scrollAreaRect.Contains(currentEvent.mousePosition))
            {
                mouseCaptured = true;
            }

            if (mouseCaptured && mouseEvent && Event.current.clickCount <= 1)
            {
                int rawMousePositionIndex = MousePositionToIndex;
                if (rawMousePositionIndex != -1)
                {
                    int mousePositionIndex = GetNearestPoorTextIndex(rawMousePositionIndex);

                    if (editorSelectIndexChanged)
                    {
                        SelectIndex = mousePositionIndex;
                    }

                    if (editorCursorIndexChanged)
                    {
                        CursorIndex = mousePositionIndex;
                    }
                }
            }

            if (currentEvent.type == EventType.MouseUp)
            {
                mouseCaptured = false;
            }
        }

        private void FixArrowCursor()
        {
            bool isKeyboard =
                new KeyCode[] { KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow, KeyCode.LeftArrow }.Contains(
                    Event.current.keyCode);
            bool isDoubleClick = Event.current.clickCount == 2;
            bool clickInTextArea = scrollAreaRect.Contains(currentEvent.mousePosition);
            if (isKeyboard || isDoubleClick || richTextChanged || AllTextSelected())
            {
                int direction = isDoubleClick ? 1 : 0;

                if (currentEvent.keyCode == KeyCode.LeftArrow)
                {
                    direction = -1;
                }
                else if (currentEvent.keyCode == KeyCode.RightArrow)
                {
                    direction = 1;
                }

                if (editorSelectIndexChanged || editorCursorIndexChanged || richTextChanged)
                {
                    CursorIndex = GetNearestPoorTextIndex(CursorIndex, direction);
                    SelectIndex = GetNearestPoorTextIndex(SelectIndex, direction);
                    cursorIndexChanged = false;
                    selectIndexChanged = false;
                }

                //Fixes double clicks that end with cursor within RichText tag.  
                if (isDoubleClick && clickInTextArea && readme.RichText.Length > 0)
                {
                    int mouseIndex = MousePositionToIndex;
                    char characterClicked = readme.RichText[Mathf.Clamp(mouseIndex, 0, readme.RichText.Length - 1)];
                    if (!char.IsWhiteSpace(characterClicked)) //Dont fix word select if clicked character is a a space
                    {
                        SelectIndex = mouseIndex;
                        CursorIndex = mouseIndex;
                        SelectIndex = GetNearestPoorTextIndex(WordStartIndex, -1);
                        CursorIndex = GetNearestPoorTextIndex(WordEndIndex, 1);
                    }
                }
            }
        }

        public void CopyRichText()
        {
            if (textEditor != null)
            {
                string textToCopy = textEditor.SelectedText.Length > 0 ? textEditor.SelectedText : readme.RichText;
                EditorGUIUtility.systemCopyBuffer = textToCopy;
                FixCopyBuffer();
            }
        }

        public void CopyPlainText()
        {
            CopyRichText();
            ForceCopyBufferToPoorText();
        }

        private void FixCopyBuffer()
        {
            if ((!(editing && sourceOn) && !TagsError(RichText)))
            {
                if (EditorGUIUtility.systemCopyBuffer != previousCopyBuffer && previousCopyBuffer != null)
                {
                    if (TextEditorActive)
                    {
                        List<string> tagPatterns = new List<string>
                        {
                            "<b>",
                            "<i>",
                        };

                        foreach (string tagPattern in tagPatterns)
                        {
                            int textStart = StartIndex - tagPattern.Length;
                            int textLength = tagPattern.Length;

                            if (textStart >= 0 && RichText.Substring(textStart, textLength) == tagPattern)
                            {
                                EditorGUIUtility.systemCopyBuffer = RichText.Substring(textStart, EndIndex - textStart);
                                break;
                            }
                        }
                    }
                }

                previousCopyBuffer = EditorGUIUtility.systemCopyBuffer;
            }
        }

        private void ForceCopyBufferToPoorText()
        {
            string newCopyBuffer = EditorGUIUtility.systemCopyBuffer;
            if (TextEditorActive && (!sourceOn && !TagsError(RichText)))
            {
                newCopyBuffer = Readme.MakePoorText(newCopyBuffer);
            }

            EditorGUIUtility.systemCopyBuffer = newCopyBuffer;
            previousCopyBuffer = newCopyBuffer;
        }

        private int GetNearestPoorTextIndex(int index, int direction = 0)
        {
            index = Mathf.Clamp(index, 0, RichText.Length);
            
            int maxRight = ActiveText.Length - index;
            for (int i = 0; direction >= 0 && i < maxRight; i++)
            {
                if (!IsInTag(index + i))
                {
                    return (index + i);
                }
            }
            
            int maxLeft = index + 1;
            for (int i = 0; direction <= 0 && i < maxLeft; i++)
            {
                if (!IsInTag(index - i))
                {
                    return (index - i);
                }
            }

            return index;
        }

        private bool TagsError(string richText)
        {
            bool tagsError = true;
//            bool hasTags = readme.richTextTagMap.Find(isTag => isTag);
            bool hasTags = RichText.Contains("<b>") || RichText.Contains("<\\b>") ||
                           RichText.Contains("<i>") || RichText.Contains("<\\i>") ||
                           RichText.Contains("<size") || RichText.Contains("<\\size>") ||
                           RichText.Contains("<color") || RichText.Contains("<\\color>");

            if (!hasTags)
            {
                tagsError = false;
            }
            else
            {
                string badTag = "</b>";
                GUIStyle richStyle = new GUIStyle();
                richStyle.richText = true;
                richStyle.wordWrap = false;

                richText = richText.Replace('\n', ' ');

                float minWidth;
                float maxWidth;
                richStyle.CalcMinMaxWidth(new GUIContent(richText), out minWidth, out maxWidth);

                GUILayout.MaxWidth(100000);

                float badTagWidth = richStyle.CalcSize(new GUIContent(badTag)).x;
                float textAndBadTagWidth = richStyle.CalcSize(new GUIContent(badTag + richText)).x;
                float textWidth = richStyle.CalcSize(new GUIContent(richText)).x;

                if (textWidth != textAndBadTagWidth - badTagWidth)
                {
                    tagsError = false;
                }
            }

            return tagsError;
        }

        private bool IsInTag(int index)
        {
            if (index == 0 || index == RichText.Length)
            {
                return false;
            }

            return IsOnTag(index) && IsOnTag(index - 1); 
        }

        private bool IsOnTag(int index)
        {
            bool isOnTag = false;

            if (readme != null && readme.richTextTagMap != null && readme.richTextTagMap.Count > index)
            {
                try
                {
                    isOnTag = readme.richTextTagMap[index];
                }
                catch (Exception exception)
                {
                    Debug.Log("Issue checking for tag: " + exception);
                }
            }

            return isOnTag;
        }

        private int MousePositionToIndex => PositionToIndex(currentEvent.mousePosition);

        private int PositionToIndex(Vector2 position)
        {
            int index = -1;
            SaveCursorIndex();

            Vector2 goalPosition = position - textAreaRect.position + scroll;

            float cursorYOffset = activeTextAreaStyle.lineHeight;

            textEditor.cursorIndex = 0;
            textEditor.selectIndex = 0;
            int maxAttempts = RichText.Length;
            textEditor.cursorIndex = GetNearestPoorTextIndex(CursorIndex);
            Vector2 currentGraphicalPosition = GetGraphicalCursorPos();
            int attempts = 0;
            for (int currentIndex = CursorIndex; index == -1; currentIndex = CursorIndex)
            {
                attempts++;
                if (attempts > maxAttempts)
                {
                    Debug.LogWarning("ReadmeEditor took too long to find mouse position and is giving up!");
                    break;
                }

                //TODO: Check for end of word wrapped line.
                bool isEndOfLine = RichText.Length <= currentIndex || RichText[currentIndex] == '\n';

                if (currentGraphicalPosition.y < goalPosition.y - cursorYOffset)
                {
                    textEditor.MoveRight();
                    textEditor.cursorIndex = GetNearestPoorTextIndex(CursorIndex);
                    textEditor.selectIndex = GetNearestPoorTextIndex(CursorIndex);
                }
                else if (currentGraphicalPosition.x < goalPosition.x && !isEndOfLine)
                {
                    textEditor.MoveRight();
                    textEditor.cursorIndex = GetNearestPoorTextIndex(CursorIndex);
                    textEditor.selectIndex = GetNearestPoorTextIndex(CursorIndex);

                    if (GetGraphicalCursorPos().x < currentGraphicalPosition.x)
                    {
                        index = CursorIndex;
                    }
                }
                else
                {
                    index = CursorIndex;
                }

                if (CursorIndex == RichText.Length)
                {
                    index = CursorIndex;
                }

                currentGraphicalPosition = GetGraphicalCursorPos();
            }

            LoadCursorIndex();

            return index;
        }

        private int WordStartIndex
        {
            get
            {
                SaveCursorIndex();

                textEditor.MoveWordLeft();
                int wordStartIndex = SelectIndex;

                LoadCursorIndex();

                return wordStartIndex;
            }
        }

        private int WordEndIndex
        {
            get
            {
                SaveCursorIndex();

                textEditor.MoveWordRight();
                int wordStartIndex = SelectIndex;

                LoadCursorIndex();

                return wordStartIndex;
            }
        }

        private void SaveCursorIndex()
        {
            tempCursorIndex.Push(CursorIndex);
            tempSelectIndex.Push(SelectIndex);   
        }

        private void LoadCursorIndex()
        {
            textEditor.cursorIndex = (int)tempCursorIndex.Pop();
            textEditor.selectIndex = (int)tempSelectIndex.Pop();
        }

        private Rect GetLastRect(Rect defaultRect)
        {
            if (Event.current.type == EventType.Repaint) //GetLastRect returns dummy values except on repaint. 
            {
                return GUILayoutUtility.GetLastRect();
            }

            return defaultRect;
        }

        private Vector2 GetGraphicalCursorPos(int cursorIndex = -1, bool useScroll = true)
        {
            if (!TextEditorActive)
            {
                return Vector2.zero;
            }

            cursorIndex = cursorIndex == -1 ? CursorIndex : cursorIndex;
            Vector2 position = activeTextAreaStyle.GetCursorPixelPosition(textAreaRect, new GUIContent(RichText), cursorIndex);
            position += scrollAreaRect.position;
            
            return position;
        }

        private bool AllTextSelected(string text = "", int cursorIndex = -1, int selectIndex = -1)
        {
            if (string.IsNullOrEmpty(text))
            {
                text = RichText;
            }

            int startIndex = -1;
            int endIndex = -1;

            bool defaultIndex = cursorIndex == -1 || selectIndex == -1;

            startIndex = defaultIndex ? StartIndex : Mathf.Min(cursorIndex, selectIndex);
            endIndex = defaultIndex ? EndIndex : Mathf.Max(cursorIndex, selectIndex);

            return TextEditorActive && (startIndex == 0 && endIndex == text.Length);
        }

        private static bool IsPrefab(GameObject gameObject)
        {
            bool isPrefab = gameObject != null && (gameObject.scene.name == null ||
                                                   gameObject.gameObject != null &&
                                                   gameObject.gameObject.scene.name == null);
            return isPrefab;
        }

        private int StartIndex
        {
            get => Math.Min(CursorIndex, SelectIndex);
            set
            {
                if (CursorIndex < SelectIndex)
                {
                    CursorIndex = value;
                }
                else
                {
                    SelectIndex = value;
                }
            }
        }

        private int EndIndex
        {
            get => Math.Max(CursorIndex, SelectIndex);
            set
            {
                if (CursorIndex > SelectIndex)
                {
                    CursorIndex = value;
                }
                else
                {
                    SelectIndex = value;
                }
            }
        }

        private int CursorIndex
        {
            get => textEditor != null ? textEditor.cursorIndex : 0;
            set
            {
                if (textEditor != null)
                {
                    if (currentCursorIndex != value)
                    {
                        if (verbose)
                        {
                            Debug.Log("README: Cursor index changed: " + currentCursorIndex + " -> " + value);
                        }

                        cursorIndexChanged = true;
                        previousCursorIndex = currentCursorIndex;
                        currentCursorIndex = value;
                    }

                    textEditor.cursorIndex = value;
                }

                ;
            }
        }

        private int SelectIndex
        {
            get => textEditor != null ? textEditor.selectIndex : 0;
            set
            {
                if (textEditor != null)
                {
                    if (currentSelectIndex != value)
                    {
                        if (verbose)
                        {
                            Debug.Log("README: Select index changed: " + currentSelectIndex + " -> " + value);
                        }

                        selectIndexChanged = true;
                        previousSelectIndex = currentSelectIndex;
                        currentSelectIndex = value;
                    }

                    textEditor.selectIndex = value;
                }

                ;
            }
        }

        private int GetLastControlId
        {
            get
            {
                int lastControlID = -1;

                Type type = typeof(EditorGUIUtility);
                FieldInfo field = type.GetField("s_LastControlID", BindingFlags.Static | BindingFlags.NonPublic);
                if (field != null)
                {
                    lastControlID = (int)field.GetValue(null);
                }

                return lastControlID;
            }
        }

        private void AddControl(string name, int id)
        {
            controlIdToName[id] = name;
            controlNameToId[name] = id;
        }

        private int GetControlId(string controlName)
        {
            return !controlNameToId.TryGetValue(controlName, out int controlId) ? -1 : controlId;
        }

        private string GetControlName(int controlId)
        {
            return !controlIdToName.TryGetValue(controlId, out string controlName) ? controlId.ToString() : controlName;
        }

        private string RichText
        {
            get => readme.RichText;
            set => readme.RichText = value;
        }

        private string ActiveText => readme.RichText;
        
        private bool TextEditorActive =>
            textEditor != null && activeTextAreaName != "" && GUI.GetNameOfFocusedControl() == activeTextAreaName;

        private TextAreaObjectField[] TextAreaObjectFields
        {
            get => readme.TextAreaObjectFields;
            set => readme.TextAreaObjectFields = value;
        }

        public void ToggleReadOnly()
        {
            readme.readonlyMode = !readme.readonlyMode;
        }

        public void ToggleScroll()
        {
            scrollEnabled = !scrollEnabled;
        }

        public void ToggleEdit()
        {
            editing = !editing;
        }
        
        private void TestHtmlAgilityPack()
        {
            if (RichText != null)
            {
                string html = RichTextToHtml(readme.RichText);
                
                HtmlDocument htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);
                foreach (var error in htmlDoc.ParseErrors)
                {
                    Debug.LogWarning(
                        "Code: " + error.Code + "\n" +
                        "Reason: " + error.Reason + "\n" +
                        "Line: " + error.Line + "\n" +
                        "LinePosition: " + error.LinePosition + "\n" +
                        "SourceText: " + error.SourceText + "\n" +
                        "StreamPosition: " + error.StreamPosition
                    );
                }
                Debug.Log(htmlDoc.Text);
                Debug.Log(htmlDoc.DocumentNode.InnerText);

                //List<bool> htmlTagMap = new List<bool>();
                foreach (HtmlNode node in htmlDoc.DocumentNode.Descendants())
                {
                    if (node.Name != "#text")
                    {
                        Debug.Log(string.Format("<{0}> Outer - line: {1} startline: {2} outerStart: {3} length: {4}",
                            node.Name, node.Line, node.LinePosition, node.OuterStartIndex, node.OuterHtml.Length));
                        Debug.Log(string.Format("<{0}> Inner - line: {1} startline: {2} innerStart: {3} length: {4}",
                            node.Name, node.Line, node.LinePosition, node.InnerStartIndex, node.InnerHtml.Length));
                        foreach (HtmlAttribute attribute in node.GetAttributes())
                        {
                            Debug.Log("\t" + attribute.Name + " " + attribute.Value);
                        }
                    }
                }
                
                Debug.Log(HtmlToRichText(htmlDoc.Text));
            }
        }

        private string RichTextToHtml(string richText)
        {
            // Replace elements followed by equal like "<size=" with attribute formatting "<size value="
            return Regex.Replace(richText, "<([a-zA-Z_0-9]+)=", "<$1 value=");
        }

        private string HtmlToRichText(string richText)
        {
            //Reverse RichTextToHtml
            return Regex.Replace(richText, "<([a-zA-Z_0-9]+) value=", "<$1=");
        }
    }
}

#endif