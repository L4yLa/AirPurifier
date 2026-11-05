using System;
using System.Collections;
using System.Reflection;
using AirPurifier.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AirPurifier
{
    /// <summary>
    /// ゲームシーンの演出（カットの火花・発光、デブリ、セイバー同士・ウォールとの接触の火花、ウォールの中身）を消す。
    ///
    /// <para><b>ゲーム中は一切処理しない。</b>各オブジェクトの初期化（Awake / Start / プール生成時）に
    /// 一度だけ値を書き換え、以後はゲーム本来の処理がそのまま空振りする状態にする。
    /// 振動（ハプティクス）やイベントを出す処理には手を入れない。</para>
    ///
    /// <para>設定は各初期化の時点で読むため、変更は次にゲームシーンを読み込んだときから反映される。</para>
    /// </summary>
    internal static class EffectRemover
    {
        // いずれも各オブジェクトの初期化時に一度だけ読むため、リフレクションで足りる
        private static readonly FieldInfo s_wallSparkle =
            AccessTools.Field(typeof(ObstacleSaberSparkleEffect), "_sparkleParticleSystem");

        private static readonly FieldInfo s_wallBurn =
            AccessTools.Field(typeof(ObstacleSaberSparkleEffect), "_burnParticleSystem");

        private static readonly FieldInfo s_obstacleMaterialSetter =
            AccessTools.Field(typeof(ObstacleController), "_obstacleMaterialSetter");

        private static readonly FieldInfo s_obstacleCoreRenderer =
            AccessTools.Field(typeof(ObstacleMaterialSetter), "_obstacleCoreRenderer");

        private static readonly FieldInfo s_debrisPools =
            AccessTools.Field(typeof(NoteDebrisSpawner), "_poolForNoteGameplayType");

        private const int MaxSubEmitterDepth = 4;

        private static PluginConfig C => PluginConfig.Instance;

        internal static void Install(Harmony harmony)
        {
            Patch(harmony, typeof(NoteCutParticlesEffect), "Awake", nameof(NoteCutParticlesEffectAwakePostfix));
            Patch(harmony, typeof(NoteDebrisSpawner), "Start", nameof(NoteDebrisSpawnerStartPostfix));
            Patch(harmony, typeof(SaberClashEffect), "Start", nameof(SaberClashEffectStartPostfix));
            Patch(harmony, typeof(ObstacleSaberSparkleEffectManager), "Start", nameof(ObstacleSaberSparkleEffectManagerStartPostfix));
            Patch(harmony, typeof(ObstacleController), "InitGraphics", nameof(ObstacleControllerInitGraphicsPostfix));
        }

        private static void Patch(Harmony harmony, Type type, string method, string postfix)
        {
            try
            {
                var target = AccessTools.Method(type, method);
                if (target == null)
                {
                    Plugin.Log.Error($"{type.Name}.{method} not found. This effect will not be removed.");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(typeof(EffectRemover), postfix));
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Failed to patch {type.Name}.{method}: {e}");
            }
        }

        /// <summary>
        /// カット時の <c>SpawnParticles</c> は各 ParticleSystem へ <c>Emit</c> するだけなので、
        /// 発生上限と描画を止めておけば毎回のカットは空振りになる。
        /// </summary>
        private static void NoteCutParticlesEffectAwakePostfix(
            ParticleSystem ____sparklesPS,
            ParticleSystem ____explosionPS,
            ParticleSystem ____explosionCorePS,
            ParticleSystem ____explosionPrePassBloomPS)
        {
            try
            {
                if (C.RemoveNoteCutSparkles)
                {
                    Silence(____sparklesPS);
                }

                if (C.RemoveNoteCutFlash)
                {
                    Silence(____explosionPS);
                    Silence(____explosionCorePS);
                    Silence(____explosionPrePassBloomPS);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"NoteCutParticlesEffect: {e}");
            }
        }

        /// <summary>
        /// <c>SpawnNoteDebris</c> はこの辞書からプールを引けないと null を返し、
        /// <c>SpawnDebris</c> は乱数を消費する前に return する。
        /// </summary>
        private static void NoteDebrisSpawnerStartPostfix(NoteDebrisSpawner __instance)
        {
            try
            {
                if (!C.RemoveDebris)
                {
                    return;
                }

                if (s_debrisPools?.GetValue(__instance) is IDictionary pools)
                {
                    pools.Clear();
                }
                else
                {
                    Plugin.Log.Error("NoteDebrisSpawner._poolForNoteGameplayType not found. Debris will not be removed.");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"NoteDebrisSpawner: {e}");
            }
        }

        /// <summary>
        /// 振動は同じコンポーネントの LateUpdate が出すため、コンポーネントは止めずにパーティクルだけを止める。
        /// LateUpdate はエミッションモジュールの ON/OFF しか切り替えず、停止中の ParticleSystem を再生しない。
        /// </summary>
        private static void SaberClashEffectStartPostfix(
            ParticleSystem ____sparkleParticleSystem,
            ParticleSystem ____glowParticleSystem)
        {
            try
            {
                if (C.RemoveSaberClashSparkles)
                {
                    Silence(____sparkleParticleSystem);
                    Silence(____glowParticleSystem);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"SaberClashEffect: {e}");
            }
        }

        /// <summary>
        /// 振動と接触イベントはマネージャの Update が出すため、マネージャは止めずに
        /// Start で生成された2つのエフェクトのパーティクルだけを止める。
        /// 接触判定（IsEmitting）はエミッションモジュールの状態を見るだけなので影響しない。
        /// </summary>
        private static void ObstacleSaberSparkleEffectManagerStartPostfix(ObstacleSaberSparkleEffect[] ____effects)
        {
            try
            {
                if (!C.RemoveWallSparkles || ____effects == null)
                {
                    return;
                }

                if (s_wallSparkle == null || s_wallBurn == null)
                {
                    Plugin.Log.Error("ObstacleSaberSparkleEffect particle fields not found. Wall sparkles will not be removed.");
                    return;
                }

                foreach (var effect in ____effects)
                {
                    if (effect == null)
                    {
                        continue;
                    }

                    Silence(s_wallSparkle.GetValue(effect) as ParticleSystem);
                    Silence(s_wallBurn.GetValue(effect) as ParticleSystem);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"ObstacleSaberSparkleEffectManager: {e}");
            }
        }

        /// <summary>
        /// プールの生成時に一度だけ呼ばれる。ゲーム側にコアのレンダラーを有効へ戻す処理はない。
        /// 枠（ParametricBoxFrameController）と外側の発光（ParametricBoxFakeGlowController）は別のレンダラーなので残る。
        /// </summary>
        private static void ObstacleControllerInitGraphicsPostfix(ObstacleController __instance)
        {
            try
            {
                if (!C.HideWallCore)
                {
                    return;
                }

                if (s_obstacleMaterialSetter == null || s_obstacleCoreRenderer == null)
                {
                    Plugin.Log.Error("Obstacle core renderer field not found. The inside of walls will not be hidden.");
                    return;
                }

                var setter = s_obstacleMaterialSetter.GetValue(__instance);
                if (setter == null)
                {
                    return;
                }

                if (s_obstacleCoreRenderer.GetValue(setter) is Renderer core && core != null)
                {
                    core.enabled = false;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"ObstacleController: {e}");
            }
        }

        /// <summary>
        /// 発生上限を 0 にして描画を止め、再生中の粒子を消す。サブエミッターも同様に止める。
        /// 描画の停止は発生上限が効かなかった場合の保険を兼ねる。
        /// </summary>
        private static void Silence(ParticleSystem ps, int depth = 0)
        {
            if (ps == null)
            {
                return;
            }

            var subEmitters = ps.subEmitters;
            if (subEmitters.enabled && depth < MaxSubEmitterDepth)
            {
                for (var i = 0; i < subEmitters.subEmittersCount; i++)
                {
                    var sub = subEmitters.GetSubEmitterSystem(i);
                    if (sub != ps)
                    {
                        Silence(sub, depth + 1);
                    }
                }
            }

            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.maxParticles = 0;

            if (ps.TryGetComponent(out ParticleSystemRenderer renderer))
            {
                renderer.enabled = false;
            }
        }
    }
}
