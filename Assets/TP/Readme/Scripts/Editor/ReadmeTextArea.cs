using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web.WebPages;
using UnityEditor;
using UnityEngine;

namespace TP
{
    public class ReadmeTextArea
    {
        private Readme readme;
        
        public string Text { get; private set; }
        
        private int instanceId;
        private Action<string, string> onTextChangedCallback;
        private string emptyText = "";
        
        //Scrolling
        public Vector2 Scroll { get; private set; }
        public bool ScrollEnabled { get; set; } = true;
        private int scrollMaxHeight = 400;
        private int scrollAreaPad = 6;
        private int scrollBarSize = 15;
        private bool mouseDownInScrollArea;
        
        private float availableWidth;

        private bool editing;
        private bool sourceOn;
        private string controlName;
        private GUIStyle style;
        private bool selectable;
        
        private Rect textAreaRect;
        private Rect scrollAreaRect;
        private Rect intersectRect; //Overlaying area between textArea and scrollArea.
        
        private GUIStyle activeTextAreaStyle;
        private GUIStyle emptyRichText;
        private GUIStyle selectableRichText;
        private GUIStyle editableRichText;
        private GUIStyle editableText;

        public string ActiveName { get; private set; } = "";
        public string EmptyName { get; }
        public string ReadonlyName { get; }
        public string SourceName { get; }
        public string StyleName { get; }

        //Delayed function call parameters. This is a workaround to not having access to coroutines in Unity Editors.  
        private int updateFocusFrame = int.MaxValue;
        private int updateReturnFocusFrame = int.MaxValue;
        private int updateCursorFrame = int.MaxValue;
        private int updateForceTextEditor = int.MaxValue;
        private (int, int) savedCursor;
        private string savedWindowFocus = "";
        
        private int frame = 0;
        
        public ReadmeTextEditor textEditor
        {
            get
            {
                ReadmeTextEditor rte = ReadmeTextEditor.Instance;
                rte.RegisterTextArea(this);
                return rte;
            }
        }

        private Event currentEvent => new Event(Event.current);
        
        public ReadmeTextArea(int instanceId, Action<string, string> onTextChangedCallback, string emptyText)
        {
            this.instanceId = instanceId;
            this.onTextChangedCallback = onTextChangedCallback;
            
            EmptyName = GetName(editing:false, empty:true);
            ReadonlyName = GetName(editing:false, empty:false);
            SourceName = GetName(editing:true, sourceOn:true);
            StyleName = GetName(editing:true, sourceOn:false);
        }

        public void Draw(bool editing, bool sourceOn, string text)
        {
            frame++;
            
            this.editing = editing;
            this.sourceOn = sourceOn;
            bool empty = text.IsEmpty();
            selectable = !empty;
            Text = !empty ? text : emptyText;
            
            if (Event.current.type == EventType.MouseDown)
            {
                mouseDownInScrollArea = intersectRect.Contains(Event.current.mousePosition);
            }

            textEditor?.RegisterTextArea(this);
            
            UpdateAvailableWidth();

            EditorGuiTextArea();
            
            if (editing)
            {
                if (TagsError())
                {
                    EditorGUILayout.HelpBox("Rich text error detected. Check for mismatched tags.",
                        MessageType.Warning);
                }
            }
        }

        public void Update(Editor editor)
        {
            if (ReadmeUtil.UnityInFocus || AwaitingTrigger)
            {
                ScrollToCursor();

                UpdateForceTextEditor();
                UpdateFocus();
                UpdateReturnFocus();
                UpdateCursor();

                if (AwaitingTrigger)
                {
                    TriggerOnInspectorGUI(editor);
                }
            }

            return;

            #region Local Methods
            void UpdateForceTextEditor()
            {
                if (Text.IsEmpty())
                {
                    updateForceTextEditor = int.MaxValue;
                    return;
                }
                
                if (frame >= updateForceTextEditor)
                {
                    if (textEditor == null)
                    {
                        TriggerUpdateFocus();
                    }
                    else
                    {
                        TriggerUpdateFocus();
                        updateForceTextEditor = int.MaxValue;
                        TriggerUpdateReturnFocus();
                    }
                }
            }
            
            void UpdateFocus()
            {
                if (frame >= updateFocusFrame)
                {
                    if (savedWindowFocus == "")
                    {
                        savedWindowFocus = EditorWindow.focusedWindow.titleContent.text;
                    }
                    
                    updateFocusFrame = int.MaxValue;
                    ReadmeUtil.FocusEditorWindow("Inspector");
                    EditorGUI.FocusTextInControl(ActiveName);
                    GUI.FocusControl(ActiveName);
                }
            }

            void UpdateCursor()
            {
                if (frame >= updateCursorFrame)
                {
                    updateCursorFrame = int.MaxValue;
                    if (textEditor != null)
                    {
                        textEditor.SetCursors(savedCursor);
                        savedCursor = (-1, -1);
                    }
                }
            }
            
            void UpdateReturnFocus()
            {
                if (frame >= updateReturnFocusFrame)
                {
                    updateReturnFocusFrame = int.MaxValue;
                    
                    if (savedWindowFocus != "")
                    {
                        ReadmeUtil.FocusEditorWindow(savedWindowFocus);
                        savedWindowFocus = "";
                    }
                }
            }
            #endregion
        }
        
        public void RepaintTextArea(Editor editor, int newCursorIndex = -1, int newSelectIndex = -1, bool focusText = false)
        {
            TriggerOnInspectorGUI(editor);
            textEditor.SetText(Text);
            textEditor.SetCursors((newCursorIndex, newSelectIndex));
            
            bool textAlreadyFocused = textEditor != null && GetControlId(GetName()) == textEditor.controlID;
            if (focusText && !textAlreadyFocused)
            {
                TriggerUpdateFocus(2);
                TriggerUpdateCursor(textEditor.GetCursors(), 4);
            }
        }

        private void TriggerUpdateForceEditor(int frameDelay = 0)
        {
            updateForceTextEditor = frame + frameDelay;
        }

        private void TriggerUpdateFocus(int frameDelay = 0)
        {
            updateFocusFrame = frame + frameDelay;
        }

        private void TriggerUpdateReturnFocus(int frameDelay = 0)
        {
            updateReturnFocusFrame = frame + frameDelay;
        }

        private void TriggerUpdateCursor((int, int) cursors, int frameDelay = 0)
        {
            updateCursorFrame = frame + frameDelay;
            savedCursor = cursors;
        }

        public bool AwaitingTrigger => 
            updateForceTextEditor != int.MaxValue ||
            updateFocusFrame != int.MaxValue ||
            updateReturnFocusFrame != int.MaxValue ||
            updateCursorFrame != int.MaxValue;

        private void TriggerOnInspectorGUI(Editor editor)
        {
            editor.Repaint();
        }

        private string GetName()
        {
            return GetName(editing, sourceOn, Text.IsEmpty());
        }

        private string GetName(bool editing, bool sourceOn=false, bool empty=false)
        {
            if (editing)
            {
                return "edit_" + (sourceOn ? "source_" : "style_") + instanceId;
            }
            else
            {
                return "view_" + (empty ? "empty_" : "style_") + instanceId;
            }
        }

        private void UpdateAvailableWidth()
        {
            EditorGUILayout.Space();
            float defaultWidth = availableWidth != 0 ? availableWidth : EditorGUIUtility.currentViewWidth - 20;
            float newWidth = ReadmeUtil.GetLastRect(new Rect(0, 0, defaultWidth, 0)).width;
            if (newWidth != availableWidth)
            {
                availableWidth = newWidth;
                TriggerUpdateForceEditor(0);
            }
        }
        
        public void EditorGuiTextArea()
        // private void EditorGuiTextArea(bool canEdit, string content, string controlName, GUIStyle style, bool selectable=true)
        {
            ActiveName = GetName(editing, sourceOn, Text.IsEmpty());
            GUIStyle style = GetGuiStyle(ActiveName);

            Vector2 size = GetTextAreaSize();
            GUILayoutOption[] options = new[] { GUILayout.Width(size.x), GUILayout.Height(size.y) };
            bool scrollShowing = ScrollEnabled && size.y + scrollAreaPad > scrollMaxHeight;
            Vector2 scrollAreaSize = new Vector2(size.x + scrollAreaPad, size.y + scrollAreaPad);
            if (scrollShowing)
            {
                scrollAreaSize.x += scrollBarSize;
                scrollAreaSize.y = scrollMaxHeight;
            }

            GUILayoutOption[] scrollAreaOptions = new[] { GUILayout.Width(scrollAreaSize.x), GUILayout.Height(scrollAreaSize.y) };
            Scroll = EditorGUILayout.BeginScrollView(Scroll, scrollAreaOptions);
            
            GUI.SetNextControlName(ActiveName);
            if (editing)
            {
                textEditor.BeforeTextAreaChange(this);
                string newText = EditorGUILayout.TextArea(Text, style, options);
                
                if (newText != Text)
                {
                    onTextChangedCallback(newText, Text);
                    textEditor.AfterTextAreaChange(this);
                }
            }
            else
            {
                if (selectable)
                {
                    EditorGUILayout.SelectableLabel(Text, style, options);
                }
                else
                {
                    EditorGUILayout.LabelField(Text, style, options);
                }
            }
            
            AddControl(new Control(ActiveName, GetLastControlId(), style, options));
            
            textAreaRect = ReadmeUtil.GetLastRect(textAreaRect, scrollAreaRect.position);
            EditorGUILayout.EndScrollView();
            scrollAreaRect = ReadmeUtil.GetLastRect(scrollAreaRect);
            intersectRect = new Rect()
            {
                position = textAreaRect.position,
                width = textAreaRect.width,
                height = scrollAreaRect.height - (textAreaRect.y - scrollAreaRect.y)
            };
        }

        public void UpdateGuiStyles(GUIStyle emptyRichText, GUIStyle selectableRichText, GUIStyle editableRichText, GUIStyle editableText)
        {
            this.emptyRichText = emptyRichText;
            this.selectableRichText = selectableRichText;
            this.editableRichText = editableRichText;
            this.editableText = editableText;
        }

        public GUIStyle GetGuiStyle(string activeName)
        {
            GUIStyle style = new GUIStyle();
            if (activeName == EmptyName) { style = emptyRichText; }
            else if (activeName == ReadonlyName) { style = selectableRichText; }
            else if (activeName == StyleName) { style = editableRichText; }
            else if (activeName == SourceName) { style = editableText; }
            return style;
        }

        public Vector2 GetTextAreaSize()
        {
            int xPadding = -10;
            Vector2 size = CalcSize(Text, xPadding, 0);
            bool scrollShowing = ScrollEnabled && size.y + scrollAreaPad > scrollMaxHeight;
            if (scrollShowing)
            {
                size = CalcSize(Text, xPadding - scrollBarSize);
            }

            return size;

            Vector2 CalcSize(string text, float xPadding = 0, float yPadding = 0)
            {
                Vector2 size = new Vector2();
                size.x = availableWidth + xPadding;
                size.y = CalcHeight(new GUIContent(text), size.x) + yPadding;
                return size;
            }
        }

        public GUIStyle Style => ActiveControl.Style;
        public float CalcHeight(GUIContent guiContent, float width) => editableRichText.CalcHeight(guiContent, width);
        public bool Contains(Vector2 position) => intersectRect.Contains(position);
        public bool InvalidClick => currentEvent.type == EventType.MouseDown && textAreaRect.Contains(currentEvent.mousePosition) && !scrollAreaRect.Contains(currentEvent.mousePosition);
        public Vector2 GetCursorPixelPosition(int cursorIndex) => ActiveControl.Style.GetCursorPixelPosition(textAreaRect, new GUIContent(Text), cursorIndex);
        public float lineHeight => ActiveControl.Style.lineHeight;
        public Rect Bounds => new (intersectRect);
        
        private void ScrollToCursor()
        {
            Vector2 resultScroll = Scroll;
            
            if (textEditor != null && textEditor.HasTextEditor)
            {
                int index = textEditor.GetCursors().Item1;
                Rect cursorRect = textEditor.GetRect(index, index);
                cursorRect.position -= Scroll;
                bool dragScroll = mouseDownInScrollArea && currentEvent.type == EventType.MouseDrag;
                bool fullScroll = currentEvent.isKey && !textEditor.AllTextSelected();
                float topDiff = Mathf.Min(0, cursorRect.yMin - scrollAreaRect.yMin);
                float bottomDiff = -Mathf.Min(0, scrollAreaRect.yMax - cursorRect.yMax);
                float scrollDiff = topDiff + bottomDiff;

                if (scrollDiff != 0)
                {
                    if (dragScroll)
                    {
                        resultScroll.y += Mathf.Sign(scrollDiff) * cursorRect.height; //Scroll one line at a time. 
                    }

                    if (fullScroll)
                    {
                        resultScroll.y += scrollDiff; //Scroll full distance to cursor 
                    }
                }
            }

            Scroll = resultScroll;
        }

        public bool RichTextDisplayed => !sourceOn && !TagsError();
        
        public int GetNearestPoorTextIndex(int index, int direction = 0)
        {
            index = Mathf.Clamp(index, 0, Text.Length);
            
            int maxRight = Text.Length - index;
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

        public bool TagsError()
        {
            bool tagsError = true;
//            bool hasTags = readme.richTextTagMap.Find(isTag => isTag);
            bool hasTags = Text.Contains("<b>") || Text.Contains("<\\b>") ||
                           Text.Contains("<i>") || Text.Contains("<\\i>") ||
                           Text.Contains("<size") || Text.Contains("<\\size>") ||
                           Text.Contains("<color") || Text.Contains("<\\color>");

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

                string tempRichText = Text.Replace('\n', ' ');

                float minWidth;
                float maxWidth;
                richStyle.CalcMinMaxWidth(new GUIContent(tempRichText), out minWidth, out maxWidth);

                GUILayout.MaxWidth(100000);

                float badTagWidth = richStyle.CalcSize(new GUIContent(badTag)).x;
                float textAndBadTagWidth = richStyle.CalcSize(new GUIContent(badTag + tempRichText)).x;
                float textWidth = richStyle.CalcSize(new GUIContent(tempRichText)).x;

                if (textWidth != textAndBadTagWidth - badTagWidth)
                {
                    tagsError = false;
                }
            }

            return tagsError;
        }

        private bool IsInTag(int index)
        {
            if (index == 0 || index == Text.Length)
            {
                return false;
            }

            return IsOnTag(index) && IsOnTag(index - 1); 
        }

        public bool IsOnTag(int index)
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
        
        private Dictionary<int, Control> ControlIdToName { get; } = new ();
        private Dictionary<string, Control> ControlNameToId { get; } = new ();
        
        private int GetLastControlId() 
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
        
        public struct Control
        {
            public string Name { get; }
            public int ID { get; }
            public GUIStyle Style { get; }
            public GUILayoutOption[] Options { get; }

            public Control(string name, int id, GUIStyle style, GUILayoutOption[] Options)
            {
                this.Name = name;
                this.ID = id;
                this.Style = style;
                this.Options = Options;
            }

            public bool RichTextSupported => Style.richText;
        }
        
        public Control ActiveControl { get; set; }

        private void AddControl(Control control)
        {   
            ControlIdToName[control.ID] = control;
            ControlNameToId[control.Name] = control;
            ActiveControl = control;
        }

        public int GetControlId(string controlName)
        {
            return !ControlNameToId.TryGetValue(controlName, out Control control) ? -1 : control.ID;
        }

        public string GetControlName(int controlId)
        {
            return !ControlIdToName.TryGetValue(controlId, out Control control) ? controlId.ToString() : control.Name;
        }

        public bool HasControl(int controlId)
        {
            return ControlIdToName.TryGetValue(controlId, out Control control);
        }

        public bool HasControl(string controlName)
        {
            return ControlNameToId.TryGetValue(controlName, out Control control);
        }
    }
}