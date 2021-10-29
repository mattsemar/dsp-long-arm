using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using LongArm.Scripts;
using LongArm.Util;
using UnityEngine;
using Action = System.Action;

namespace LongArm.UI
{
    public enum BuildHelperMode
    {
        [Description("Fly mecha to build locations")]
        FlyToBuild,

        [Description("Build automatically using inventory contents but not relying on construction bots")]
        FastBuild,

        [Description("Build automatically, ignoring inventory contents (disables achievements)")]
        FreeBuild,

        [Description("Leave construction bots alone to do their work")]
        None,
    }

    public enum FactoryTourMode
    {
        [Description("Visit mining veins and oil seeps")]
        Veins,

        [Description("Visit assemblers, fractionators, colliders, chemical planets")]
        Assemblers,

        [Description("Visit logistics stations")]
        Stations,

        [Description("Disabled")] None,
    }

    public class LongArmUi : MonoBehaviour
    {
        protected static readonly Resolution DefaultRes = new Resolution { width = 1920, height = 1080 };
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
        private BuildHelperMode _buildHelperMode = BuildHelperMode.None;
        private GUIStyle _textStyle;
        private readonly int _defaultFontSize = ScaleToDefault(12);
        public static LongArmUi instance;
        private bool _popupOpened;
        public static BuildPreviewSummary buildPreviewSummary;

        private GUIStyle ToolTipStyle { get; set; }


        private void Update()
        {
            if (instance == null)
                instance = this;

            if (KeyBindPatch.GetKeyBind("ShowLongArmWindow").keyValue)
            {
                if (instance != null)
                    instance.Visible = !instance.Visible;
            }
        }

        void OnClose()
        {
            Visible = false;
            _requestHide = false;
            RestoreGuiSkinOptions();
        }

        void OnGUI()
        {
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
            _buildHelperMode = PluginConfig.buildBuildHelperMode;
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
                DrawOverrideBuildRange();
                DrawOverrideInspectRange();
                GUILayout.EndVertical();
                DrawPrebuildSection();

                GUILayout.BeginVertical("Box");
                DrawModeSelector();
                GUILayout.EndVertical();

                DrawActionSection();

                // DrawFactoryTourSection();
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

        public static void DrawCenteredLabel(string text, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawActionSection()
        {
            GUILayout.BeginVertical("Box");
            DrawCenteredLabel("Actions");
            AddAction("Power", "Add Fuel", "Add fuel from inventory to power generators that are empty", () =>
            {
                if (FactoryActionExecutor.Instance != null) FactoryActionExecutor.Instance.RequestAddFuel();
            });

            AddAction("Stations", "Add Bots", "Add drones/vessels from inventory to stations (only adds if none of type are found)", () =>
            {
                if (FactoryActionExecutor.Instance != null) FactoryActionExecutor.Instance.RequestAddBots();
            });

            var btnLabel = "Show Preview State";
            if (buildPreviewSummary != null && buildPreviewSummary.Visible)
            {
                btnLabel = "Hide Preview State";
            }

            AddAction("Build Preview", btnLabel, "Summary of remaining build previews to be built", () =>
            {
                PrebuildManager.Instance.GetSummary(true);
                buildPreviewSummary.Visible = !buildPreviewSummary.Visible;
            });
            GUILayout.EndVertical();
        }

        private void AddAction(string actionCategory, string buttonText, string buttonTip, Action buttonAction)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(actionCategory);
            var pressed = GUILayout.Button(new GUIContent(buttonText, buttonTip));
            if (pressed)
            {
                buttonAction();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawFactoryTourSection()
        {
            GUILayout.BeginVertical("Box");
            var names = Enum.GetNames(typeof(FactoryTourMode));
            var selectedName = Enum.GetName(typeof(FactoryTourMode), PluginConfig.TourMode);
            var guiContents = names.Select(n => GetModeAsGuiContent(n, "", selectedName == n));
            GUILayout.Label("Factory Tour Mode");
            var curIndex = names.ToList().IndexOf(selectedName);
            var index = GUILayout.SelectionGrid(curIndex, guiContents.ToArray(), 2);

            if (index != curIndex)
            {
                if (Enum.TryParse(names[index], out FactoryTourMode newMode))
                {
                    PluginConfig.TourMode = newMode;
                }
            }

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            var prevButton = GUILayout.Button(new GUIContent("Previous", "Go back to previous location in tour"));
            if (prevButton)
            {
                TourFactoryScript.RequestPrevious();
            }

            var nextButton = GUILayout.Button(new GUIContent("Next", "Go back to next location in tour"));
            if (nextButton)
            {
                TourFactoryScript.RequestNext();
            }

            var autoButton = GUILayout.Button(new GUIContent("Auto", "Automatically advance to next location for tour"));
            if (autoButton)
            {
                TourFactoryScript.RequestAuto();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Current location index: {TourFactoryScript.GetCurrentIndex()} / total points {TourFactoryScript.GetTotalLocations()}");
            GUILayout.EndVertical();

            GUILayout.EndVertical();
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
            GUILayout.Label("Build range");
            GUILayout.Label(new GUIContent(GameMain.mainPlayer.mecha.buildArea.ToString(CultureInfo.CurrentCulture), "Current build range of mecha"));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawOverrideInspectRange()
        {
            GUILayout.BeginHorizontal();
            var result = GUILayout.Toggle(PluginConfig.overrideInspectionRange.Value,
                new GUIContent("Increase inspect range", "Allow opening assemblers/storage windows from map view and from far away in the normal view"));
            if (result != PluginConfig.overrideInspectionRange.Value)
            {
                PluginConfig.overrideInspectionRange.Value = result;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawOverrideBuildRange()
        {
            GUILayout.BeginHorizontal();
            var result = GUILayout.Toggle(PluginConfig.overrideBuildRange.Value,
                new GUIContent("Increase build range", "Increases build range to include the entire planet"));
            if (result != PluginConfig.overrideBuildRange.Value)
            {
                PluginConfig.overrideBuildRange.Value = result;
                if (result)
                    LongArmPlugin.instance.Enable();
                else
                    LongArmPlugin.instance.Disable();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawModeSelector()
        {
            var names = Enum.GetNames(typeof(BuildHelperMode));
            var selectedName = Enum.GetName(typeof(BuildHelperMode), _buildHelperMode);
            var guiContents = names.Select(n => GetModeAsGuiContent(n, "", selectedName == n));
            GUILayout.Label("Build Helper Mode");
            GUILayout.BeginVertical("Box");
            var curIndex = names.ToList().IndexOf(selectedName);
            var index = GUILayout.SelectionGrid(curIndex, guiContents.ToArray(), 2);

            if (index != curIndex)
            {
                if (Enum.TryParse(names[index], out BuildHelperMode newMode))
                {
                    switch (newMode)
                    {
                        case BuildHelperMode.None:
                            _buildHelperMode = newMode;
                            PluginConfig.buildBuildHelperMode = newMode;
                            break;
                        case BuildHelperMode.FlyToBuild:
                            PluginConfig.buildBuildHelperMode = newMode;
                            _buildHelperMode = newMode;
                            break;
                        case BuildHelperMode.FastBuild:
                            PluginConfig.buildBuildHelperMode = newMode;
                            _buildHelperMode = newMode;
                            break;
                        case BuildHelperMode.FreeBuild:
                            if (AbnormalityConfirmed())
                            {
                                PluginConfig.buildBuildHelperMode = newMode;
                                _buildHelperMode = newMode;
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

            return !GameMain.data.abnormalityCheck.IsFunctionNormal(GameAbnormalityCheck.BIT_MECHA) || PluginConfig.playerConfirmedAbnormalityTrigger;
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
            var enumMember = typeof(BuildHelperMode).GetMember(sourceValue).FirstOrDefault();
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
            if (instance == null)
                return;
            if (instance.Visible)
            {
                instance._requestHide = true;
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
            if (!PluginConfig.playerConfirmedAbnormalityTrigger && !_popupOpened)
            {
                _popupOpened = true;
                UIMessageBox.Show("Build for free", "Please confirm that you would like to build items for free (disabling achievements)".Translate(),
                    "Ok", "Cancel", 0, delegate
                    {
                        PluginConfig.playerConfirmedAbnormalityTrigger = true;
                        PluginConfig.buildBuildHelperMode = BuildHelperMode.FreeBuild;
                        _popupOpened = false;
                        _buildHelperMode = BuildHelperMode.FreeBuild;
                    }, delegate
                    {
                        Log.LogAndPopupMessage($"Cancelled");
                        _popupOpened = false;
                    });
            }
        }

        public static int ScaleToDefault(int input, bool horizontal = true)
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