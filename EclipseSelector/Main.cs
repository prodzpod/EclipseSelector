using BepInEx;
using BepInEx.Logging;
using HG;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EclipseSelector
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class Main : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "prodzpod";
        public const string PluginName = "EclipseSelector";
        public const string PluginVersion = "1.0.0";
        public static ManualLogSource Log;
        internal static PluginInfo pluginInfo;

        public static int currentEclipseDifficulty;
        public static EclipseRunScreenController runScreenController;
        public static List<EclipseDifficultyMedalDisplay> selectors = new();

        public void Awake()
        {
            pluginInfo = Info;
            Log = Logger;
            On.RoR2.UI.EclipseRunScreenController.UpdateDisplayedSurvivor += (orig, self) =>
            {
                orig(self);
                runScreenController = self;
                currentEclipseDifficulty = Mathf.Clamp(EclipseRun.GetLocalUserSurvivorCompletedEclipseLevel(self.localUser, self.eventSystemLocator.eventSystem?.localUser?.userProfile.GetSurvivorPreference()) + 1, EclipseRun.minEclipseLevel, EclipseRun.maxEclipseLevel);
            };
            On.EclipseDifficultyMedalDisplay.OnEnable += (orig, self) => { orig(self); selectors.Add(self); };
            On.EclipseDifficultyMedalDisplay.OnDisable += (orig, self) => { orig(self); selectors.Remove(self); };
            On.EclipseDifficultyMedalDisplay.Refresh += (orig, self) =>
            {
                orig(self);
                var button = self.GetComponent<Selector>();
                if (!button) button = self.gameObject.AddComponent<Selector>();
                button.medalDisplay = self;
                button.localUser = LocalUserManager.GetFirstLocalUser();
                button.survivorDef = button.localUser?.userProfile.GetSurvivorPreference();
            };
            IL.RoR2.EclipseRun.OverrideRuleChoices += (il) =>
            {
                ILCursor c = new(il);
                c.GotoNext(x => x.MatchCallOrCallvirt<EclipseRun>(nameof(EclipseRun.GetEclipseDifficultyIndex)));
                c.Emit(OpCodes.Pop);
                c.EmitDelegate(() => currentEclipseDifficulty);
            };
        }

        public class Selector : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
        {
            public LocalUser localUser;
            public SurvivorDef survivorDef;
            public EclipseDifficultyMedalDisplay medalDisplay;

            public void OnEnable()
            {
                InstanceTracker.Add(this);
            }

            public void OnDisable()
            {
                InstanceTracker.Remove(this);
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (eventData.button != PointerEventData.InputButton.Left) return;
                int currentLevel = Mathf.Clamp(EclipseRun.GetLocalUserSurvivorCompletedEclipseLevel(localUser, survivorDef) + 1, EclipseRun.minEclipseLevel, EclipseRun.maxEclipseLevel);
                if (medalDisplay.eclipseLevel > currentLevel) return;
                currentEclipseDifficulty = medalDisplay.eclipseLevel;
                foreach (EclipseDifficultyMedalDisplay selector in selectors)
                {
                    if (selector.eclipseLevel > currentEclipseDifficulty) selector.iconImage.sprite = selector.unearnedSprite;
                    else if (selector.eclipseLevel == currentLevel) selector.iconImage.sprite = selector.incompleteSprite;
                    else selector.iconImage.sprite = selector.completeSprite;
                }
                if (runScreenController != null)
                {
                    DifficultyDef def = DifficultyCatalog.GetDifficultyDef(EclipseRun.GetEclipseDifficultyIndex(currentEclipseDifficulty));
                    runScreenController.eclipseDifficultyName.token = def.nameToken;
                    runScreenController.eclipseDifficultyDescription.token = def.descriptionToken;
                }
            }
        }
    }
}