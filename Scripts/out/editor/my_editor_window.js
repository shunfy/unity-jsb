"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.MyEditorWindow = void 0;
const System_1 = require("System");
const UnityEditor_1 = require("UnityEditor");
const UnityEngine_1 = require("UnityEngine");
// @jsb.Shortcut("Window/JS/MyEditorWindow")
class MyEditorWindow extends UnityEditor_1.EditorWindow {
    constructor() {
        super(...arguments);
        this._parentWindowRect = new UnityEngine_1.Rect(0, 0, 0, 0);
        this._resizeStart = new UnityEngine_1.Rect(0, 0, 0, 0);
        this._minWindowSize = new UnityEngine_1.Vector2(120, 100);
        this._thisWindowRect = new UnityEngine_1.Rect(50, 50, 400, 300);
        this._resizerContent = new UnityEngine_1.GUIContent("* ", "Resize");
        this._isResizing = false;
        this._lastHour = -1;
        this._lastMinute = -1;
        this._lastSecond = -1;
    }
    Awake() {
        this._onSceneGui = this.onSceneGui.bind(this);
        this._onMenuTest = this.onMenuTest.bind(this);
        this._onWindowGUI = this.onWindowGUI.bind(this);
    }
    OnEnable() {
        this.titleContent = new UnityEngine_1.GUIContent("Blablabla6");
        UnityEditor_1.SceneView.duringSceneGui("add", this._onSceneGui);
    }
    OnDisable() {
        UnityEditor_1.SceneView.duringSceneGui("remove", this._onSceneGui);
    }
    onWindowGUI() {
        if (UnityEngine_1.GUILayout.Button("Click")) {
            console.log("you clicked");
        }
        let mousePosition = UnityEngine_1.Event.current.mousePosition;
        if (this._styleWindowResize == null) {
            this._styleWindowResize = UnityEngine_1.GUI.skin.box;
        }
        let resizerRect = UnityEngine_1.GUILayoutUtility.GetRect(this._resizerContent, this._styleWindowResize, UnityEngine_1.GUILayout.ExpandWidth(false));
        resizerRect = new UnityEngine_1.Rect(this._thisWindowRect.width - resizerRect.width, this._thisWindowRect.height - resizerRect.height, resizerRect.width, resizerRect.height);
        if (UnityEngine_1.Event.current.type == UnityEngine_1.EventType.MouseDown && resizerRect.Contains(mousePosition)) {
            this._isResizing = true;
            this._resizeStart = new UnityEngine_1.Rect(mousePosition.x, mousePosition.y, this._thisWindowRect.width, this._thisWindowRect.height);
            //Event.current.Use();  // the GUI.Button below will eat the event, and this way it will show its active state
        }
        else if (UnityEngine_1.Event.current.type == UnityEngine_1.EventType.MouseUp && this._isResizing) {
            this._isResizing = false;
        }
        else if (UnityEngine_1.Event.current.type != UnityEngine_1.EventType.MouseDrag && UnityEngine_1.Event.current.isMouse) {
            // if the mouse is over some other window we won't get an event, this just kind of circumvents that by checking the button state directly
            this._isResizing = false;
        }
        else if (this._isResizing) {
            // console.log("resizing");
            this._thisWindowRect.width = Math.max(this._minWindowSize.x, this._resizeStart.width + (mousePosition.x - this._resizeStart.x));
            this._thisWindowRect.height = Math.max(this._minWindowSize.y, this._resizeStart.height + (mousePosition.y - this._resizeStart.y));
            this._thisWindowRect.xMax = Math.min(this._parentWindowRect.width, this._thisWindowRect.xMax); // modifying xMax affects width, not x
            this._thisWindowRect.yMax = Math.min(this._parentWindowRect.height, this._thisWindowRect.yMax); // modifying yMax affects height, not y
        }
        UnityEngine_1.GUI.Button(resizerRect, this._resizerContent, this._styleWindowResize);
        UnityEngine_1.GUI.DragWindow(new UnityEngine_1.Rect(0, 0, 10000, 10000));
    }
    onSceneGui(sv) {
        let id = UnityEngine_1.GUIUtility.GetControlID(UnityEngine_1.FocusType.Passive);
        this._parentWindowRect = sv.position;
        if (this._thisWindowRect.yMax > this._parentWindowRect.height) {
            this._thisWindowRect.yMax = this._parentWindowRect.height;
        }
        this._thisWindowRect = UnityEngine_1.GUILayout.Window(id, this._thisWindowRect, this._onWindowGUI, "My JS Editor Window");
        UnityEditor_1.HandleUtility.AddDefaultControl(UnityEngine_1.GUIUtility.GetControlID(UnityEngine_1.FocusType.Passive));
    }
    onMenuTest() {
        console.log("menu item test");
    }
    AddItemsToMenu(menu) {
        menu.AddItem(new UnityEngine_1.GUIContent("Test"), false, this._onMenuTest);
    }
    Update() {
        let d = System_1.DateTime.Now;
        if (this._lastSecond != d.Second) {
            this._lastSecond = d.Second;
            this._lastMinute = d.Minute;
            this._lastHour = d.Hour;
            this.Repaint();
        }
    }
    OnGUI() {
        UnityEditor_1.EditorGUILayout.HelpBox("Hello", UnityEditor_1.MessageType.Info);
        if (UnityEngine_1.GUILayout.Button("I am Javascript")) {
            console.log("Thanks!", System_1.DateTime.Now);
        }
        if (UnityEngine_1.GUILayout.Button("CreateWindow")) {
            UnityEditor_1.EditorWindow.CreateWindow(MyEditorWindow);
        }
        let w = this.position.width;
        let h = this.position.height;
        let center = new UnityEngine_1.Vector3(w * 0.5, h * 0.5, 0);
        let rotSecond = UnityEngine_1.Quaternion.Euler(0, 0, 360 * this._lastSecond / 60 + 180);
        let rotHour = UnityEngine_1.Quaternion.Euler(0, 0, 360 * this._lastHour / 24 + 180);
        let rotMinute = UnityEngine_1.Quaternion.Euler(0, 0, 360 * this._lastMinute / 60 + 180);
        //@ts-ignore
        UnityEditor_1.Handles.DrawLine(center, center + rotSecond * new UnityEngine_1.Vector3(0, 90, 0));
        //@ts-ignore
        UnityEditor_1.Handles.DrawLine(center, center + rotMinute * new UnityEngine_1.Vector3(0, 75, 0));
        //@ts-ignore
        UnityEditor_1.Handles.DrawLine(center, center + rotHour * new UnityEngine_1.Vector3(0, 60, 0));
        UnityEditor_1.Handles.DrawWireDisc(center, UnityEngine_1.Vector3.back, 100);
        UnityEditor_1.EditorGUILayout.BeginHorizontal();
        UnityEditor_1.EditorGUILayout.IntField(this._lastHour);
        UnityEditor_1.EditorGUILayout.IntField(this._lastMinute);
        UnityEditor_1.EditorGUILayout.IntField(this._lastSecond);
        UnityEditor_1.EditorGUILayout.EndHorizontal();
    }
}
exports.MyEditorWindow = MyEditorWindow;
//# sourceMappingURL=my_editor_window.js.map