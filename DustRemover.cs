using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AirPurifier
{
    /// <summary>
    /// 環境・メニュー・マルチプレイのダスト（DustPS / DustBritney）を無効化する。
    ///
    /// <para><b>ゲームシーン中は一切処理しない。</b>Update もコルーチンも持たず、
    /// シーン読み込み時とマルチプレイのプレイヤー生成時に一度だけ動く。
    /// シーン全体の検索（FindObjectsOfType 等）は使わず、既知の階層を Transform.Find で直接引く。</para>
    ///
    /// <para>ダストの階層（1.40.8 / 1.44.1 のシーンファイルを解析して確認）:
    /// <list type="bullet">
    /// <item>環境シーン: ルート <c>Environment</c> 直下の <c>DustPS</c>（Britney のみ <c>DustBritney</c>）</item>
    /// <item>メインメニュー（MainMenu）: ルート <c>Wrapper</c> 配下の <c>MenuCore/Origin/MenuMainCamera/DustPS</c></item>
    /// <item>起動時の画面（HealthWarning / Credits / StartupError）: ルート <c>MenuCore</c> 配下の <c>Origin/MenuMainCamera/DustPS</c></item>
    /// <item>ShaderWarmup: ルート <c>MenuMainCamera</c> 直下の <c>DustPS</c></item>
    /// <item>マルチプレイ: 実行時に生成されるローカルプレイヤーのプレハブ内
    ///   （アクティブ側 <c>IsActiveObjects/DustPS</c>、観戦側 <c>DustPS</c>）</item>
    /// </list></para>
    /// </summary>
    internal static class DustRemover
    {
        private const string HarmonyId = "com.l4yla.AirPurifier";

        private const string EnvironmentRoot = "Environment";
        private const string MainMenuRoot = "Wrapper";
        private const string MenuCoreRoot = "MenuCore";
        private const string MenuCameraRoot = "MenuMainCamera";
        private static readonly string[] EnvironmentDustPaths = { "DustPS", "DustBritney" };
        private const string MainMenuDustPath = "MenuCore/Origin/MenuMainCamera/DustPS";
        private const string MenuCoreDustPath = "Origin/MenuMainCamera/DustPS";
        private const string MenuCameraDustPath = "DustPS";
        private const string ActivePlayerDustPath = "IsActiveObjects/DustPS";
        private const string InactivePlayerDustPath = "DustPS";

        /// <summary>
        /// 1.44.1 の MoveAndRotateWithMainCamera は Awake で static な Camera.onPreCull に購読し、
        /// GameObject を非アクティブにしてもコールバックが走り続ける。購読解除に使う。
        /// 1.40.8 には存在しない（LateUpdate 方式のため非アクティブ化だけで止まる）ので null になる。
        /// </summary>
        private static MethodInfo s_onPreCullCallback;

        internal static void Install()
        {
            s_onPreCullCallback = typeof(MoveAndRotateWithMainCamera).GetMethod(
                "OnPreCullCallback", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            SceneManager.sceneLoaded += OnSceneLoaded;

            try
            {
                var target = typeof(MultiplayerPlayersManager).GetMethod(
                    "SpawnPlayers", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (target == null)
                {
                    Plugin.Log.Error("MultiplayerPlayersManager.SpawnPlayers not found. Multiplayer dust will not be removed.");
                }
                else
                {
                    new Harmony(HarmonyId).Patch(
                        target,
                        postfix: new HarmonyMethod(typeof(DustRemover), nameof(SpawnPlayersPostfix)));
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Failed to patch MultiplayerPlayersManager.SpawnPlayers: {e}");
            }

            Plugin.Log.Info($"Installed (onPreCull unsubscribe: {(s_onPreCullCallback != null ? "on" : "off")}).");
        }

        /// <summary>
        /// シーン読み込み直後に呼ばれる。環境シーンのルート Environment はシーンファイル上で非アクティブのため、
        /// この時点ではダストの Awake はまだ走っておらず、ここで止めれば以後一度も起動しない。
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    switch (root.name)
                    {
                        case EnvironmentRoot:
                            foreach (var path in EnvironmentDustPaths)
                            {
                                Disable(root.transform, path, scene.name);
                            }
                            break;
                        case MainMenuRoot:
                            // ゲームシーンにも同名ルートがあるが、その場合は Find が null を返すだけ
                            Disable(root.transform, MainMenuDustPath, scene.name);
                            break;
                        case MenuCoreRoot:
                            Disable(root.transform, MenuCoreDustPath, scene.name);
                            break;
                        case MenuCameraRoot:
                            Disable(root.transform, MenuCameraDustPath, scene.name);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"OnSceneLoaded({scene.name}): {e}");
            }
        }

        /// <summary>
        /// プレイヤーのプレハブは Zenject の Create で生成・アクティブ化まで済んでいるため、
        /// 戻った時点で非アクティブ化しても有効化処理の途中に割り込まない。
        /// 途中で観戦側に切り替わる場合は、観戦側プレイヤーの生成後に発火するイベントで処理する。
        /// </summary>
        private static void SpawnPlayersPostfix(MultiplayerPlayersManager __instance)
        {
            try
            {
                var active = __instance.activeLocalPlayerFacade;
                if (active != null)
                {
                    Disable(active.transform, ActivePlayerDustPath, "Multiplayer(active)");
                }

                var inactive = __instance.inactivePlayerFacade;
                if (inactive != null)
                {
                    Disable(inactive.transform, InactivePlayerDustPath, "Multiplayer(inactive)");
                }

                __instance.didSwitchPlayerToInactiveEvent += () =>
                {
                    var facade = __instance.inactivePlayerFacade;
                    if (facade != null)
                    {
                        Disable(facade.transform, InactivePlayerDustPath, "Multiplayer(inactive)");
                    }
                };
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"SpawnPlayersPostfix: {e}");
            }
        }

        private static void Disable(Transform parent, string path, string context)
        {
            var target = parent.Find(path);
            if (target == null)
            {
                return;
            }

            var go = target.gameObject;

            // 1.44.1: Awake 済みなら Camera.onPreCull の購読を外す（未購読なら何も起きない）
            if (s_onPreCullCallback != null && go.TryGetComponent(out MoveAndRotateWithMainCamera mover))
            {
                var callback = (Camera.CameraCallback)Delegate.CreateDelegate(
                    typeof(Camera.CameraCallback), mover, s_onPreCullCallback);
                Camera.onPreCull -= callback;
            }

            if (go.activeSelf)
            {
                go.SetActive(false);
                Plugin.Log.Info($"Disabled {context}/{path}");
            }
        }
    }
}
