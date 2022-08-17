using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TP.ExtensionMethods;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TP
{
    public class ReadmeTextEditor
    {
        private static ReadmeTextEditor instance;
        public static ReadmeTextEditor Instance => instance ??= new ReadmeTextEditor(); //Returns instance if not null, otherwise instantiates new instance.

        private TextEditor textEditor;
        public TextEditor TextEditor => textEditor ??= GetPrivateTextEditor;
        public bool HasTextEditor => TextEditor != null;
        
        private bool selectIndexChanged;
        private bool cursorIndexChanged;
        private bool editorSelectIndexChanged;
        private bool editorCursorIndexChanged;
        private int currentCursorIndex = -1;
        private int currentSelectIndex = -1;
        private bool richTextChanged;
        private bool mouseCaptured;
        private readonly Stack tempCursorIndex = new ();
        private readonly Stack tempSelectIndex = new ();
        
        private Action<int> onCursorChangedCallback;

        // Cursor Fix 
        public bool ApplyCursorBugFix { get; set; } = true;

        private readonly List<ReadmeTextArea> RegisteredTextAreas = new ();

        private ReadmeTextEditor()
        {
            textEditor = TextEditor;
        }

        public void RegisterTextArea(ReadmeTextArea readmeTextArea)
        {
            RegisteredTextAreas.AddUnique(readmeTextArea);
        }

        public ReadmeTextArea ActiveTextArea => 
            RegisteredTextAreas.FirstOrDefault(readmeTextArea =>
                readmeTextArea.HasControl(controlID) || readmeTextArea.HasControl(GUI.GetNameOfFocusedControl()));

        private TextEditor GetPrivateTextEditor =>
            typeof(EditorGUI)
                .GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(null) as TextEditor;
        
        private Event currentEvent => new Event(Event.current);

        public void Update(Editor editor)
        {
            FixCursorBug();
        }
        
        public bool CursorChanged => selectIndexChanged || cursorIndexChanged;
        public bool InternalCursorChanged => currentSelectIndex != SelectIndex || currentCursorIndex != CursorIndex;

        public void SetText(string text)
        {
            if (textEditor != null)
            {
                textEditor.text = text;
            }
        }

        public void SetCursors((int, int) cursors)
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

        public (int, int)  GetCursors()
        {
            return textEditor == null ? (-1, -1) : (textEditor.cursorIndex, textEditor.selectIndex);
        }
        
        public void BeforeTextAreaChange(ReadmeTextArea readmeTextArea)
        {
            if (!readmeTextArea.TagsError())
            {
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
                                       objTagStart + objTagLength <= readmeTextArea.Text.Length &&
                                       readmeTextArea.Text.Substring(objTagStart, objTagLength) == objTag;

                    if (objectField)
                    {
                        int nextPoorIndex = readmeTextArea.GetNearestPoorTextIndex(charIndex + (direction == 0 ? 1 : 0), direction);
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
                        if (charIndex < 0 || CursorIndex > readmeTextArea.Text.Length)
                        {
                            int newIndex = readmeTextArea.GetNearestPoorTextIndex(charIndex - direction);
                            CursorIndex = newIndex;
                            SelectIndex = newIndex;
                            Event.current.Use();
                        }
                        else if (readmeTextArea.IsOnTag(charIndex))
                        {
                            CursorIndex += direction == 1 ? 1 : -1;
                            SelectIndex += direction == 1 ? 1 : -1;
                        
                            BeforeTextAreaChange(readmeTextArea);
                        }
                    }
                }
            }
        }

        public void AfterTextAreaChange(ReadmeTextArea readmeTextArea)
        {
            int direction = currentEvent.keyCode == KeyCode.Backspace ? -1 : 0;
            CursorIndex = readmeTextArea.GetNearestPoorTextIndex(CursorIndex, -direction);
            SelectIndex = readmeTextArea.GetNearestPoorTextIndex(SelectIndex, -direction);
        }
        
        private void FixCursorBug()
        {
            if (ApplyCursorBugFix && TextEditorActive && ActiveTextArea.RichTextDisplayed)
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

        private void FixMouseCursor()
        {
            bool mouseEvent =
                new EventType[] { EventType.MouseDown, EventType.MouseDrag, EventType.MouseUp }.Contains(currentEvent
                    .type);

            if (currentEvent.type == EventType.MouseDown && ActiveTextArea.Contains(currentEvent.mousePosition))
            {
                mouseCaptured = true;
            }

            if (mouseCaptured && mouseEvent && Event.current.clickCount <= 1)
            {
                int rawMousePositionIndex = MousePositionToIndex;
                if (rawMousePositionIndex != -1)
                {
                    int mousePositionIndex = ActiveTextArea.GetNearestPoorTextIndex(rawMousePositionIndex);

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
            bool clickInTextArea = ActiveTextArea.Contains(currentEvent.mousePosition);
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
                    CursorIndex = ActiveTextArea.GetNearestPoorTextIndex(CursorIndex, direction);
                    SelectIndex = ActiveTextArea.GetNearestPoorTextIndex(SelectIndex, direction);
                    cursorIndexChanged = false;
                    selectIndexChanged = false;
                }

                //Fixes double clicks that end with cursor within RichText tag.  
                if (isDoubleClick && clickInTextArea && text.Length > 0)
                {
                    int mouseIndex = MousePositionToIndex;
                    char characterClicked = text[Mathf.Clamp(mouseIndex, 0, text.Length - 1)];
                    if (!char.IsWhiteSpace(characterClicked)) //Dont fix word select if clicked character is a a space
                    {
                        SelectIndex = mouseIndex;
                        CursorIndex = mouseIndex;
                        SelectIndex = ActiveTextArea.GetNearestPoorTextIndex(WordStartIndex, -1);
                        CursorIndex = ActiveTextArea.GetNearestPoorTextIndex(WordEndIndex, 1);
                    }
                }
            }
        }

        public bool AllTextSelected(string text = "", int cursorIndex = -1, int selectIndex = -1)
        {
            // if (string.IsNullOrEmpty(text))
            // {
            //     text = RichText;
            // }

            int startIndex = -1;
            int endIndex = -1;

            bool defaultIndex = cursorIndex == -1 || selectIndex == -1;

            startIndex = defaultIndex ? StartIndex : Mathf.Min(cursorIndex, selectIndex);
            endIndex = defaultIndex ? EndIndex : Mathf.Max(cursorIndex, selectIndex);

            return TextEditorActive && (startIndex == 0 && endIndex == text.Length);
        }

        public Rect GetRect(int startIndex, int endIndex)
        {
            Rect textEditorRect = textEditor?.position ?? new Rect();

            if (textEditor != null && ActiveTextArea != null && text != ActiveTextArea.Text && TextEditorActive)
            {
                textEditor.text = ActiveTextArea.Text;
            }

            int textSize = 12; //Todo get size from size map
            float padding = 1;
            string sizeWrapper = "<size={0}>{1}</size>";

            Vector2 startPositionIndex1 = GetGraphicalCursorPos(startIndex);
            Vector2 startPositionIndex2 = GetGraphicalCursorPos(startIndex + 1);
            Vector2 startPosition;

            if (startPositionIndex1.y != startPositionIndex2.y && startIndex != endIndex)
            {
                startPosition = startPositionIndex2 + new Vector2(padding, 0);
            }
            else
            {
                startPosition = startPositionIndex1 + new Vector2(padding, 0);
            }

            Vector2 endPosition = GetGraphicalCursorPos(endIndex) + new Vector2(-padding, 0);
            float height = (ActiveTextArea?.Style ?? new GUIStyle()).CalcHeight(new GUIContent(string.Format(sizeWrapper, textSize, " ")), 100) - 10;

            if (startPosition.y != endPosition.y)
            {
                endPosition.x = textEditorRect.xMax - 20;
            }

            endPosition.y += height;

            Vector2 size = endPosition - startPosition;

            Rect rect = new Rect(startPosition, size);

            return rect;
        }

        private Vector2 GetGraphicalCursorPos(int cursorIndex = -1)
        {
            if (!TextEditorActive)
            {
                return Vector2.zero;
            }

            cursorIndex = cursorIndex == -1 ? CursorIndex : cursorIndex;
            return ActiveTextArea.GetCursorPixelPosition(cursorIndex);
        }

        private int MousePositionToIndex => PositionToIndex(currentEvent.mousePosition);

        public int PositionToIndex(Vector2 position)
        {
            int index = -1;
            SaveCursorIndex();

            Vector2 goalPosition = position + ActiveTextArea.Scroll;

            float cursorYOffset = ActiveTextArea.lineHeight;

            textEditor.cursorIndex = 0;
            textEditor.selectIndex = 0;
            int maxAttempts = text.Length;
            textEditor.cursorIndex = ActiveTextArea.GetNearestPoorTextIndex(CursorIndex);
            Vector2 currentGraphicalPosition = GetGraphicalCursorPos();
            int attempts = 0;
            for (int currentIndex = CursorIndex; index == -1; currentIndex = CursorIndex)
            {
                attempts++;
                if (attempts > maxAttempts)
                {
                    break;
                }

                //TODO: Check for end of word wrapped line.
                bool isEndOfLine = text.Length <= currentIndex || text[currentIndex] == '\n';

                if (currentGraphicalPosition.y < goalPosition.y - cursorYOffset)
                {
                    textEditor.MoveRight();
                    textEditor.cursorIndex = ActiveTextArea.GetNearestPoorTextIndex(CursorIndex);
                    textEditor.selectIndex = ActiveTextArea.GetNearestPoorTextIndex(CursorIndex);
                }
                else if (currentGraphicalPosition.x < goalPosition.x && !isEndOfLine)
                {
                    textEditor.MoveRight();
                    textEditor.cursorIndex = ActiveTextArea.GetNearestPoorTextIndex(CursorIndex);
                    textEditor.selectIndex = ActiveTextArea.GetNearestPoorTextIndex(CursorIndex);

                    if (GetGraphicalCursorPos().x < currentGraphicalPosition.x)
                    {
                        index = CursorIndex;
                    }
                }
                else
                {
                    index = CursorIndex;
                }

                if (CursorIndex == text.Length)
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

        public int StartIndex
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

        public int EndIndex
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

        public int CursorIndex
        {
            get => textEditor?.cursorIndex ?? 0;
            private set
            {
                if (textEditor != null)
                {
                    if (currentCursorIndex != value)
                    {
                        cursorIndexChanged = true;
                        currentCursorIndex = value;
                    }

                    textEditor.cursorIndex = value;
                };
            }
        }

        public int SelectIndex
        {
            get => textEditor?.selectIndex ?? 0;
            private set
            {
                if (textEditor != null)
                {
                    if (currentSelectIndex != value)
                    {
                        selectIndexChanged = true;
                        currentSelectIndex = value;
                    }

                    textEditor.selectIndex = value;
                };
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

        

        // private bool TextEditorActive => controlID == ActiveControl.id && GUI.GetNameOfFocusedControl() == ActiveControl.name;
        public bool TextEditorActive => HasTextEditor && ActiveTextArea != null;
        
        #region Passthrough to public TextEditor interface
        
        public int controlID { get => TextEditor?.controlID ?? -1; set { if(HasTextEditor) TextEditor.controlID = value; }}
        public string text { get => TextEditor.text; set { if(HasTextEditor) TextEditor.text = value; }}
        public int cursorIndex  { get => TextEditor.cursorIndex; set { if(HasTextEditor) TextEditor.cursorIndex = value; }} 
        public int selectIndex { get => TextEditor.selectIndex; set { if(HasTextEditor) TextEditor.selectIndex = value; }}
        public Vector2 scrollOffset { get => TextEditor.scrollOffset; set { if(HasTextEditor) TextEditor.scrollOffset = value; }}
        public bool multiline { get => TextEditor.multiline; set { if(HasTextEditor) TextEditor.multiline = value; }}
        public Rect position { get => TextEditor.position; set { if(HasTextEditor) TextEditor.position = value; }}
        public GUIStyle style { get => TextEditor.style; set { if(HasTextEditor) TextEditor.style = value; }}
        public int altCursorPosition { get => TextEditor.altCursorPosition; set { if(HasTextEditor) TextEditor.altCursorPosition = value; }}
        public Vector2 graphicalSelectCursorPos { get => TextEditor.graphicalSelectCursorPos; set { if(HasTextEditor) TextEditor.graphicalSelectCursorPos = value; }}
        public Vector2 graphicalCursorPos { get => TextEditor.graphicalCursorPos; set { if(HasTextEditor) TextEditor.graphicalCursorPos = value; }}
        public TextEditor.DblClickSnapping doubleClickSnapping { get => TextEditor.doubleClickSnapping; set { if(HasTextEditor) TextEditor.doubleClickSnapping = value; }}
        public bool isPasswordField { get => TextEditor.isPasswordField; set { if(HasTextEditor) TextEditor.isPasswordField = value; }}
        public TouchScreenKeyboard keyboardOnScreen { get => TextEditor.keyboardOnScreen; set { if(HasTextEditor) TextEditor.keyboardOnScreen = value; }}
        public bool hasHorizontalCursorPos { get => TextEditor.hasHorizontalCursorPos; set { if(HasTextEditor) TextEditor.hasHorizontalCursorPos = value; }}
        
        public string SelectedText { get => TextEditor.SelectedText; }
        public bool hasSelection { get => TextEditor.hasSelection; }

        public void MoveRight() => TextEditor.MoveRight();
        public void MoveLeft() => TextEditor.MoveLeft();
        public void MoveWordRight() => TextEditor.MoveWordRight();
        public void MoveWordLeft() => TextEditor.MoveWordLeft();

        #endregion
    }
}