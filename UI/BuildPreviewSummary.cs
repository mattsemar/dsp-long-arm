using LongArm.Model;
using LongArm.Scripts;
using LongArm.Util;
using UnityEngine;
using static LongArm.UI.LongArmUi;

namespace LongArm.UI
{
    public class BuildPreviewSummary : MonoBehaviour
    {
        public bool Visible { get; set; }
        private bool _requestHide;
        public Rect windowRect = new Rect(ScaleToDefault(0), ScaleToDefault(500, false), ScaleToDefault(400), ScaleToDefault(400, false));

        public bool needReinit;

        private Texture2D _tooltipBg;

        private int _loggedMessageCount = 0;
        private string _savedGUISkin;
        private GUISkin _savedGUISkinObj;
        private Color _savedColor;
        private GUIStyle _savedTextStyle;
        private Color _savedBackgroundColor;
        private Color _savedContentColor;
        private GUISkin _mySkin;
        private GUIStyle _textStyle;
        private readonly int _defaultFontSize = ScaleToDefault(12);
        public static BuildPreviewSummary instance;
        private bool _popupOpened;
        private Vector2 _scrollViewVector = Vector2.zero;
        private int _maxHeightSz;
        private GUILayoutOption _maxHeight;
        private PrebuildSummary _prebuildSummary;


        private GUIStyle ToolTipStyle { get; set; }

        private void Update()
        {
            if (instance == null)
                instance = this;
            if (buildPreviewSummary == null)
                buildPreviewSummary = this;
            _prebuildSummary = PrebuildManager.Instance.GetSummary();
        }

        void OnClose()
        {
            Visible = false;
            _requestHide = false;
            RestoreGuiSkinOptions();
        }

        void OnGUI()
        {
            if (instance == null)
                instance = this;

            if (!Visible || (_prebuildSummary.items.Count == 0 && PluginConfig.autoShowPreviewStatusWindow.Value))
                return;
            if (_requestHide)
            {
                OnClose();
                return;
            }

            var uiGame = UIRoot.instance.uiGame;
            if (uiGame == null)
            {
                return;
            }

            if (uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active || uiGame.techTree.active)
            {
                return;
            }
            
            if (_maxHeight == null)
            {
                var imgHeight = ItemUtil.GetItemImageHeight();
                _maxHeightSz = imgHeight / 2;
                _maxHeight = GUILayout.MaxHeight(_maxHeightSz);
            }

            Init();
            _scrollViewVector = GUI.BeginScrollView(windowRect, _scrollViewVector, new Rect(0, 0, 400, 400), false, false);
            WindowFnWrapper();
            GUI.EndScrollView();
        }

        void RestoreGuiSkinOptions()
        {
            GUI.skin = _savedGUISkinObj;
            GUI.backgroundColor = _savedBackgroundColor;
            GUI.contentColor = _savedContentColor;
            GUI.color = _savedColor;
        }

        void SaveCurrentGuiOptions()
        {
            _savedBackgroundColor = GUI.backgroundColor;
            _savedContentColor = GUI.contentColor;
            _savedColor = GUI.color;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            GUI.color = Color.white;


            if (_mySkin == null || needReinit)
            {
                _savedGUISkin = JsonUtility.ToJson(GUI.skin);
                _savedGUISkinObj = GUI.skin;
                _mySkin = ScriptableObject.CreateInstance<GUISkin>();
                JsonUtility.FromJsonOverwrite(_savedGUISkin, _mySkin);
                GUI.skin = _mySkin;
                GUI.skin.button.normal.textColor = Color.white;
                GUI.skin.textArea.normal.textColor = Color.white;
                GUI.skin.textField.normal.textColor = Color.white;
                GUI.skin.toggle.normal.textColor = Color.white;
                GUI.skin.toggle.onNormal.textColor = Color.white;
                GUI.skin.button.normal.textColor = Color.white;
                GUI.skin.button.onNormal.textColor = Color.white;
                GUI.skin.button.onActive.textColor = Color.white;
                GUI.skin.button.active.textColor = Color.white;
                GUI.skin.label.hover.textColor = Color.white;
                GUI.skin.label.onNormal.textColor = Color.white;
                GUI.skin.label.normal.textColor = Color.white;
                if (_textStyle == null)
                    _textStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = _defaultFontSize
                    };
                GUI.skin.label = _textStyle;
                GUI.skin.button = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = _defaultFontSize
                };
                GUI.skin.textField = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = _defaultFontSize
                };
                GUI.skin.toggle = new GUIStyle(GUI.skin.toggle)
                {
                    fontSize = _defaultFontSize,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            else
            {
                _savedGUISkinObj = GUI.skin;
                GUI.skin = _mySkin;
            }
        }

        private void Init()
        {
            if (_tooltipBg == null && !needReinit)
            {
                return;
            }

            var background = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            background.SetPixel(0, 0, Color.black);
            background.Apply();
            _tooltipBg = background;
            InitWindowRect();
            needReinit = false;
        }

        private void WindowFnWrapper()
        {
            SaveCurrentGuiOptions();
            WindowFn();
            RestoreGuiSkinOptions();
        }

        private void WindowFn()
        {
            GUILayout.BeginArea(new Rect(windowRect.width - 25f, 0, 25f, 30f));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();
            {
                GUILayout.BeginVertical("Box");
                DrawCenteredLabel("Build Status");

                DrawBuildPreviewSection();
                GUILayout.EndVertical();
            }
            if (GUI.tooltip != null)
            {
                GUI.skin = null;
                if (ToolTipStyle == null)
                    ToolTipStyle = new GUIStyle
                    {
                        normal = new GUIStyleState { textColor = Color.white, },
                        wordWrap = true,
                        alignment = TextAnchor.MiddleCenter,
                        stretchHeight = true,
                        stretchWidth = true,
                        fontSize = _defaultFontSize
                    };

                var height = ToolTipStyle.CalcHeight(new GUIContent(GUI.tooltip), windowRect.width) + 10;
                var rect = GUILayoutUtility.GetRect(windowRect.width - 20, height * 1.25f);
                rect.y += 20;
                GUI.Box(rect, GUI.tooltip, ToolTipStyle);
            }
        }

        private void DrawBuildPreviewSection()
        {
            GUILayout.BeginHorizontal(new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold
            });

            var layoutOptions = GUILayout.Width(150);
            GUILayout.Label(new GUIContent("#"), layoutOptions);
            var headRect = GUILayoutUtility.GetRect(_maxHeightSz, _maxHeightSz);
            GUI.Label(headRect, new GUIContent("Item"));
            GUILayout.Label(new GUIContent("Inventory", "Count in inventory"), layoutOptions);
            GUILayout.Label(new GUIContent("Previews", "Count of build previews for item"), layoutOptions);
            GUILayout.EndHorizontal();


            for (var index = 0; index < _prebuildSummary.items.Count; index++)
            {
                var item = _prebuildSummary.items[index];
                GUILayout.BeginHorizontal(_textStyle, _maxHeight);
                GUILayout.Label(new GUIContent((index + 1).ToString()), layoutOptions);
                var rect = GUILayoutUtility.GetRect(_maxHeightSz, _maxHeightSz);
                GUI.Label(rect, new GUIContent(item.itemImage.texture, item.itemName));
                var invCountText = (item.inventoryCount < item.neededCount) ? $"<b>{item.inventoryCount}</b>" : item.inventoryCount.ToString();
                GUILayout.Label(new GUIContent(invCountText, $"{item.itemName} count in inventory"), layoutOptions);
                GUILayout.Label(new GUIContent(item.neededCount.ToString(), $"{item.itemName} count to be placed"), layoutOptions);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal(_textStyle, _maxHeight);
            GUILayout.Label(new GUIContent("<b>Total</b>"), layoutOptions);
            var summaryRect = GUILayoutUtility.GetRect(_maxHeightSz, _maxHeightSz);
            GUI.Label(summaryRect, new GUIContent(" "));
            GUILayout.Label(new GUIContent($"<b>{_prebuildSummary.missingCount}</b>", "Total missing items (needed but not in inventory)"), layoutOptions);
            GUILayout.Label(new GUIContent(  $"<b>{_prebuildSummary.total}</b>", "Total build previews"), layoutOptions);
            GUILayout.EndHorizontal();
           
        }


        private void InitWindowRect()
        {
            var width = Mathf.Min(Screen.width, ScaleToDefault(650));
            var height = Screen.height < 560 ? Screen.height : ScaleToDefault(560, false);
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            windowRect = new Rect(offsetX, offsetY, width, height);


            Mathf.RoundToInt(windowRect.width / 2.5f);
        }
    }
}