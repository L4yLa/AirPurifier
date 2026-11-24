using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using AirPurifier.Configuration;
using HarmonyLib;
using HMUI;
using UnityEngine;

namespace AirPurifier
{
    /// <summary>
    /// 起動時の健康上の注意画面（Health and Safety）を、一度同意したことがある場合だけ飛ばす。
    ///
    /// <para>初回起動や、利用規約・プライバシーポリシーの画面には手を入れない。
    /// 同意の記録は作らない。注意画面に同意済みで地域も選択済みのとき、つまり
    /// 注意画面を閉じた後にゲームが更新する記録がもう残っていないときだけ飛ばす。</para>
    ///
    /// <para>ゲームの版で作りが違うため、対象の処理の有無で方法を選ぶ。
    /// どちらも見つからなければ何もせず、注意画面はそのまま表示される。</para>
    /// </summary>
    internal static class HealthWarningSkipper
    {
        // 1.40.8: 画面の切り替えが終わるのを待つ上限（秒）。超えたら何もしない
        private const float TransitionTimeout = 5f;

        // 1.40.8: Continue を押したときに呼ばれる処理
        private static MethodInfo s_didFinish;

        private static PluginConfig C => PluginConfig.Instance;

        internal static void Install(Harmony harmony)
        {
            try
            {
                var type = typeof(HealthWarningFlowCoordinator);

                // 1.44.1: 起動時の流れの1段階として注意画面を表示する
                var showAsync = AccessTools.Method(type, "ShowHealthWarningAsync");
                if (showAsync != null)
                {
                    harmony.Patch(showAsync, prefix: new HarmonyMethod(typeof(HealthWarningSkipper), nameof(ShowHealthWarningAsyncPrefix)));
                    return;
                }

                // 1.40.8: 同意済みなら最初の画面として注意画面が選ばれ、Continue で次へ進む
                s_didFinish = AccessTools.Method(type, "HandleHealthWarningViewControllerDidFinish");
                var didActivate = AccessTools.Method(type, "DidActivate");
                if (s_didFinish != null && didActivate != null)
                {
                    harmony.Patch(didActivate, postfix: new HarmonyMethod(typeof(HealthWarningSkipper), nameof(DidActivatePostfix)));
                    return;
                }

                Plugin.Log.Error("Health warning flow not found. The health warning will not be skipped.");
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Failed to patch the health warning: {e}");
            }
        }

        /// <summary>同意済みで、注意画面を閉じた後に必要な記録の更新もない状態か。</summary>
        private static bool CanSkip(PlayerDataModel playerDataModel)
        {
            if (!C.SkipHealthWarning || playerDataModel == null)
            {
                return false;
            }

            var playerData = playerDataModel.playerData;
            return playerData != null &&
                   playerData.playerAgreements != null &&
                   playerData.playerAgreements.AgreedToHealthAndSafety() &&
                   playerData.DidSelectRegion();
        }

        /// <summary>
        /// 1.44.1: 表示せずに完了したタスクを返す。元の処理は表示後に「同意の記録」と「地域の選択済み」を
        /// 必要なら付けるだけなので、両方済みのときは飛ばしても結果は同じになる。
        /// </summary>
        private static bool ShowHealthWarningAsyncPrefix(ref Task __result, PlayerDataModel ____playerDataModel)
        {
            try
            {
                if (!CanSkip(____playerDataModel))
                {
                    return true;
                }

                __result = Task.CompletedTask;
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"HealthWarning: {e}");
                return true;
            }
        }

        /// <summary>
        /// 1.40.8: 最初の画面が注意画面なら、画面の切り替えが終わってから Continue と同じ処理を呼ぶ。
        /// 切り替え中に次の画面へ差し替えると HMUI の遷移が壊れるおそれがあるため、待ってから行う。
        /// </summary>
        private static void DidActivatePostfix(
            HealthWarningFlowCoordinator __instance,
            bool addedToHierarchy,
            PlayerDataModel ____playerDataModel,
            HealthWarningViewController ____healthWarningViewController)
        {
            try
            {
                if (!addedToHierarchy || ____healthWarningViewController == null || !CanSkip(____playerDataModel))
                {
                    return;
                }

                if (__instance.topViewController != ____healthWarningViewController)
                {
                    return;
                }

                __instance.StartCoroutine(SkipWhenReady(__instance, ____healthWarningViewController));
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"HealthWarning: {e}");
            }
        }

        private static IEnumerator SkipWhenReady(HealthWarningFlowCoordinator coordinator, ViewController healthWarning)
        {
            var deadline = Time.unscaledTime + TransitionTimeout;

            // 少なくとも1フレーム待ち、画面の切り替えが終わるまで待つ
            do
            {
                yield return null;
            }
            while (coordinator != null && coordinator.isInTransition && Time.unscaledTime < deadline);

            if (coordinator == null || coordinator.isInTransition ||
                !coordinator.isActivated || coordinator.topViewController != healthWarning)
            {
                yield break;
            }

            try
            {
                s_didFinish.Invoke(coordinator, null);
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"HealthWarning: {e}");
            }
        }
    }
}
