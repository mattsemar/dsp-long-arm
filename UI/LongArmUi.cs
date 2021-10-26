using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using LongArm.Scripts;
using LongArm.Util;
using UnityEngine;

namespace LongArm.UI
{
    public enum Mode
    {
        [Description("Extend construction bot build range (default)")]
        ExtendedRange,

        [Description("Fly mecha to build locations")]
        FlyToBuild,

        [Description("Build automatically using inventory contents but not relying on construction bots")]
        FastBuild,

        [Description("Build automatically, ignoring inventory contents (disables achievements)")]
        FreeBuild,

        [Description("Disable plugin and set build range back to normal")]
        Disabled,
    }

    public class LongArmUi : MonoBehaviour
    {
        private static readonly Resolution DefaultRes = new Resolution { width = 1920, height = 1080 };
        public bool Visible { get; set; }
        private bool _requestHide;
        public Rect windowRect = new Rect(ScaleToDefault(300), ScaleToDefault(150, false), ScaleToDefault(250), ScaleToDefault(400, false));

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
        private int _chosenPlanet = -1;
        private Mode _mode = Mode.ExtendedRange;
        private GUIStyle _textStyle;
        private readonly int _defaultFontSize = ScaleToDefault(12);
        private static LongArmUi _instance;
        private bool _gotConfirmationOfAbnormality;
        private bool _popupOpened;

        private GUIStyle ToolTipStyle { get; set; }

        void OnClose()
        {
            Visible = false;
            _requestHide = false;
            RestoreGuiSkinOptions();
        }

        void OnGUI()
        {
            if (_instance == null)
                _instance = this;
            if (!Visible)
                return;
            if (Input.GetKeyDown(KeyCode.Escape) || _requestHide)
            {
                OnClose();
                return;
            }

            Init();

            windowRect = GUILayout.Window(1297895112, windowRect, WindowFnWrapper, "Long Arm");


            EatInputInRect(windowRect);
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
            _mode = PluginConfig.buildMode;
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

        private void WindowFnWrapper(int id)
        {
            SaveCurrentGuiOptions();
            WindowFn();
            GUI.DragWindow();
            RestoreGuiSkinOptions();
        }

        private void WindowFn()
        {
            GUILayout.BeginArea(new Rect(windowRect.width - 25f, 0f, 25, 30));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();

            {
                GUILayout.BeginVertical("Box");
                DrawModeSelector();
                GUILayout.EndVertical();
                DrawPrebuildSection();
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

        private void DrawPrebuildSection()
        {
            GUILayout.BeginVertical("Box");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Build Preview Count");
            var prebuildManager = PrebuildManager.Instance;
            GUILayout.Label(prebuildManager != null
                ? new GUIContent(prebuildManager.RemainingCount().ToString(), "Build previews remaining to be built")
                : new GUIContent("Unknown", "Build previews remaining to be built"));
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            GUILayout.Label("Percent complete");
            var percentDone = "0%";
            if (prebuildManager != null)
            {
                percentDone = prebuildManager.GetPercentDone();
            }

            GUILayout.Label(new GUIContent(percentDone, "Percent of seen preview builds that are now built"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Build range");
            GUILayout.Label(new GUIContent(GameMain.mainPlayer.mecha.buildArea.ToString(CultureInfo.CurrentCulture), "Current build range of mecha"));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            var result = GUILayout.Toggle(PluginConfig.overrideInspectionRange.Value, new GUIContent("Increase inspect range", "Allow opening assemblers/storage windows from map view"));
            if (result != PluginConfig.overrideInspectionRange.Value)
            {
                PluginConfig.overrideInspectionRange.Value = result;
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawModeSelector()
        {
            var names = Enum.GetNames(typeof(Mode));
            var selectedName = Enum.GetName(typeof(Mode), _mode);
            var guiContents = names.Select(n => GetModeAsGuiContent(n, "", selectedName == n));
            GUILayout.Label("Mode");
            GUILayout.BeginVertical("Box");
            var curIndex = names.ToList().IndexOf(selectedName);
            var index = GUILayout.SelectionGrid(curIndex, guiContents.ToArray(), 1);

            if (index != curIndex)
            {
                if (Enum.TryParse(names[index], out Mode newMode))
                {
                    switch (newMode)
                    {
                        case Mode.Disabled:
                            LongArmPlugin.instance.Disable();
                            _mode = newMode;

                            break;
                        case Mode.ExtendedRange:
                            LongArmPlugin.instance.Enable();
                            PluginConfig.buildMode = Mode.ExtendedRange;
                            _mode = newMode;
                            break;
                        case Mode.FlyToBuild:
                            LongArmPlugin.instance.Disable();
                            PluginConfig.buildMode = Mode.FlyToBuild;
                            _mode = newMode;
                            break;
                        case Mode.FastBuild:
                            LongArmPlugin.instance.Disable();
                            PluginConfig.buildMode = Mode.FastBuild;
                            _mode = newMode;
                            break;
                        case Mode.FreeBuild:
                            if (AbnormalityConfirmed())
                            {
                                PluginConfig.buildMode = newMode;
                                _mode = newMode;
                            }
                            else
                                ConfirmAbnormality();

                            break;
                    }
                }
            }

            GUILayout.EndVertical();
        }

        private bool AbnormalityConfirmed()
        {
            if (GameMain.data?.abnormalityCheck == null)
                return false;
            if (!GameMain.data.abnormalityCheck.isGameNormal())
                return true;

            return !GameMain.data.abnormalityCheck.IsFunctionNormal(GameAbnormalityCheck.BIT_MECHA) || _gotConfirmationOfAbnormality;
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

        private static GUIContent GetModeAsGuiContent(string sourceValue, string parentDescription, bool currentlySelected)
        {
            var enumMember = typeof(Mode).GetMember(sourceValue).FirstOrDefault();
            var attr = enumMember?.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().FirstOrDefault();
            var currentlySelectedIndicator = currentlySelected ? "<b>(selected)</b> " : "";
            var sval = attr?.Description ?? sourceValue;
            var label = currentlySelected ? $"<b>{sourceValue}</b>" : sourceValue;
            return new GUIContent(label, $"<b>{parentDescription}</b> {currentlySelectedIndicator} {sval}");
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(UIGame), "On_E_Switch")]
        public static void UIGame_On_E_Switch_Postfix()
        {
            if (_instance == null)
                return;
            if (_instance.Visible)
            {
                _instance._requestHide = true;
            }
        }

        // copied from https://github.com/starfi5h/DSP_Mod/blob/d38b52eb895d43e6feee09e6bb537a5726d7d466/SphereEditorTools/UIWindow.cs#L221
        private void EatInputInRect(Rect eatRect)
        {
            if (!(Input.GetMouseButton(0) || Input.GetMouseButtonDown(0))) //Eat only when left-click
                return;
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }

        private void ConfirmAbnormality()
        {
            if (!_gotConfirmationOfAbnormality && !_popupOpened)
            {
                _popupOpened = true;
                UIMessageBox.Show("Build for free", "Please confirm that you would like to build items for free (disabling achievements)".Translate(),
                    "Ok", "Cancel", 0, delegate
                    {
                        _gotConfirmationOfAbnormality = true;
                        PluginConfig.buildMode = Mode.FreeBuild;
                        _popupOpened = false;
                        _mode = Mode.FreeBuild;
                    }, delegate
                    {
                        Log.LogAndPopupMessage($"Cancelled");
                        _popupOpened = false;
                    });
            }
        }

        private static int ScaleToDefault(int input, bool horizontal = true)
        {
            if (Screen.currentResolution.Equals(DefaultRes))
            {
                return input;
            }

            float ratio;
            if (horizontal)
            {
                ratio = (float)Screen.currentResolution.width / DefaultRes.width;
            }
            else
            {
                ratio = (float)Screen.currentResolution.height / DefaultRes.height;
                return (int)(input * ratio);
            }

            return (int)(input * ratio);
        }
    }
}