using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using AirPurifier.Configuration;
using HarmonyLib;
using UnityEngine;

namespace AirPurifier
{
    /// <summary>
    /// ゲームシーンの演出（デブリ、セイバー同士・ウォールとの接触の火花、ウォールの中身）を消し、
    /// カット時の火花（全方向・斬撃方向）の大きさと時間を変える。
    ///
    /// <para><b>ゲーム中は処理を追加しない。</b>各オブジェクトの初期化（Awake / Start / プール生成時）に
    /// 一度だけ値を書き換え、以後はゲーム本来の処理がそのまま空振りする状態にする。
    /// 斬撃方向の火花の寿命だけは、カット時の処理にある定数を静的フィールドの読み込みに置き換える
    /// （呼び出しは増えない）。振動（ハプティクス）やイベントを出す処理には手を入れない。</para>
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

        // 幅・長さ・時間の倍率の上限（%）
        private const int MaxPercent = 5000;

        // 発生上限を引き上げるときの倍率
        private const int LimitMultiplier = 10;

        // 全方向の火花に付ける重力（地球の重力に対する割合）
        private const float CutSparkleGravityModifier = 0.05f;

        // SpawnParticles が斬撃方向の火花の寿命に使う定数（寿命 = 定数 × lifetimeMultiplier）
        private const float SlashLifetimeMin = 0.8f;
        private const float SlashLifetimeMax = 1.2f;

        // SpawnParticles の定数の置き換え先。Awake のたびに設定から決め直す。
        // 読み出しはトランスパイラが差し込む命令だけなので、コンパイラからは未使用に見える
#pragma warning disable CS0414
        private static float s_slashLifetimeMin = SlashLifetimeMin;
        private static float s_slashLifetimeMax = SlashLifetimeMax;
#pragma warning restore CS0414

        private static PluginConfig C => PluginConfig.Instance;

        internal static void Install(Harmony harmony)
        {
            Patch(harmony, typeof(NoteCutParticlesEffect), "Awake", nameof(NoteCutParticlesEffectAwakePostfix));
            PatchSlashLifetime(harmony);
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
        /// 斬撃方向の火花（<c>_sparklesPS</c>）の寿命を変えるため、<c>SpawnParticles</c> の定数 0.8 / 1.2 を
        /// 静的フィールドの読み込みに置き換える。両方が見つからなければ何も変えない（寿命だけが変わらない）。
        /// </summary>
        private static void PatchSlashLifetime(Harmony harmony)
        {
            try
            {
                var target = AccessTools.Method(typeof(NoteCutParticlesEffect), "SpawnParticles");
                if (target == null)
                {
                    Plugin.Log.Error("NoteCutParticlesEffect.SpawnParticles not found. Slash duration will not change.");
                    return;
                }

                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(EffectRemover), nameof(SpawnParticlesTranspiler)));
            }
            catch (Exception e)
            {
                Plugin.Log.Error($"Failed to patch NoteCutParticlesEffect.SpawnParticles: {e}");
            }
        }

        private static IEnumerable<CodeInstruction> SpawnParticlesTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var curve = AccessTools.Field(typeof(NoteCutParticlesEffect), "_sparklesLifetimeMinMaxCurve");
            var setMin = AccessTools.PropertySetter(typeof(ParticleSystem.MinMaxCurve), nameof(ParticleSystem.MinMaxCurve.constantMin));
            var setMax = AccessTools.PropertySetter(typeof(ParticleSystem.MinMaxCurve), nameof(ParticleSystem.MinMaxCurve.constantMax));

            var min = FindLifetimeConstant(codes, curve, SlashLifetimeMin, setMin);
            var max = FindLifetimeConstant(codes, curve, SlashLifetimeMax, setMax);

            if (min < 0 || max < 0)
            {
                Plugin.Log.Error("Slash lifetime constants not found in SpawnParticles. Slash duration will not change.");
                return codes;
            }

            // ラベルなどを保つため、命令オブジェクトは差し替えずに中身だけを変える
            codes[min].opcode = OpCodes.Ldsfld;
            codes[min].operand = AccessTools.Field(typeof(EffectRemover), nameof(s_slashLifetimeMin));
            codes[max].opcode = OpCodes.Ldsfld;
            codes[max].operand = AccessTools.Field(typeof(EffectRemover), nameof(s_slashLifetimeMax));
            return codes;
        }

        /// <summary>
        /// 斬撃方向の火花の寿命を設定する並び
        /// <c>ldflda _sparklesLifetimeMinMaxCurve / ldc.r4 定数 / ldarg 9 / mul / call setter</c>
        /// がちょうど1つあるときだけ、その定数の位置を返す。値だけでなく前後の命令まで一致させ、
        /// ゲームの更新で別の用途の定数を書き換えないようにする。
        /// </summary>
        private static int FindLifetimeConstant(List<CodeInstruction> codes, FieldInfo curve, float value, MethodInfo setter)
        {
            if (curve == null || setter == null)
            {
                return -1;
            }

            var found = -1;
            for (var i = 1; i + 3 < codes.Count; i++)
            {
                if (codes[i - 1].opcode == OpCodes.Ldflda && Equals(codes[i - 1].operand, curve) &&
                    codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand is float f && f == value &&
                    IsLoadArg(codes[i + 1], 9) &&
                    codes[i + 2].opcode == OpCodes.Mul &&
                    (codes[i + 3].opcode == OpCodes.Call || codes[i + 3].opcode == OpCodes.Callvirt) &&
                    Equals(codes[i + 3].operand, setter))
                {
                    if (found >= 0)
                    {
                        return -1;
                    }

                    found = i;
                }
            }

            return found;
        }

        private static bool IsLoadArg(CodeInstruction code, int index)
        {
            if (code.opcode == OpCodes.Ldarg_S || code.opcode == OpCodes.Ldarg)
            {
                return code.operand is byte b ? b == index
                    : code.operand is short s ? s == index
                    : code.operand is int n ? n == index
                    : code.operand is ParameterInfo p && p.Position + 1 == index;
            }

            return false;
        }

        /// <summary>
        /// カット時の <c>SpawnParticles</c> は各 ParticleSystem へ <c>Emit</c> するだけなので、
        /// 発生上限と描画を止めておけば毎回のカットは空振りになる。
        ///
        /// <para>全方向の火花は <c>_explosionPS</c>、斬撃方向へ伸びる火花は <c>_sparklesPS</c>。
        /// カットごとにゲームが上書きするのは色・位置・向きと、斬撃方向の火花の寿命だけで、
        /// 大きさ・描画の伸び方・全方向の火花の寿命はここで書き換えた値がそのまま使われる。</para>
        /// </summary>
        private static void NoteCutParticlesEffectAwakePostfix(
            ParticleSystem ____sparklesPS,
            ParticleSystem ____explosionPS)
        {
            try
            {
                s_slashLifetimeMin = SlashLifetimeMin;
                s_slashLifetimeMax = SlashLifetimeMax;

                if (C.ChangeNoteCutSparkles)
                {
                    ChangeCutSparkles(____explosionPS);
                }

                if (C.ChangeSlashEffect)
                {
                    ChangeSlash(____sparklesPS);
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
        /// 全方向の火花の幅・長さ・時間を変える。寿命はカット時に上書きされないため、ここで直接書き換える。
        /// </summary>
        private static void ChangeCutSparkles(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            if (!TryGetScales(C.CutSparkleWidthPercent, C.CutSparkleLengthPercent, C.CutSparkleDurationPercent,
                    out var width, out var length, out var duration))
            {
                Silence(ps);
                return;
            }

            ScaleShape(ps, width, length);

            var main = ps.main;
            main.startLifetime = ScaleCurve(main.startLifetime, duration);

            if (C.CutSparkleLimit10x)
            {
                RaiseLimit(ps);
            }

            // 元は重力 0。元から付いている減速と合わさり、飛び散って勢いが落ちたところで垂れ下がる
            if (C.CutSparkleGravity)
            {
                main.gravityModifier = new ParticleSystem.MinMaxCurve(CutSparkleGravityModifier);
            }
        }

        /// <summary>
        /// 斬撃方向の火花の幅・長さ・時間を変える。寿命はカット時に毎回設定し直されるため、
        /// <c>SpawnParticles</c> の定数の置き換え先（静的フィールド）に倍率を入れる。
        /// </summary>
        private static void ChangeSlash(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            if (!TryGetScales(C.SlashWidthPercent, C.SlashLengthPercent, C.SlashDurationPercent,
                    out var width, out var length, out var duration))
            {
                Silence(ps);
                return;
            }

            ScaleShape(ps, width, length);

            if (C.SlashLimit10x)
            {
                RaiseLimit(ps);
            }

            s_slashLifetimeMin = SlashLifetimeMin * duration;
            s_slashLifetimeMax = SlashLifetimeMax * duration;
        }

        /// <summary>
        /// 同時に存在できる粒子の上限を引き上げる。時間を伸ばすと同時に存在する粒子が増え、
        /// 元の上限（両方の火花とも 500）に達すると以後のカットで火花が出なくなるため。
        /// </summary>
        private static void RaiseLimit(ParticleSystem ps)
        {
            var main = ps.main;
            var current = main.maxParticles;
            main.maxParticles = current > int.MaxValue / LimitMultiplier ? int.MaxValue : current * LimitMultiplier;
        }

        /// <summary>% の設定値を倍率にする。いずれかが 0 なら false（非表示にする）。</summary>
        private static bool TryGetScales(int widthPercent, int lengthPercent, int durationPercent,
            out float width, out float length, out float duration)
        {
            width = Mathf.Clamp(widthPercent, 0, MaxPercent) / 100f;
            length = Mathf.Clamp(lengthPercent, 0, MaxPercent) / 100f;
            duration = Mathf.Clamp(durationPercent, 0, MaxPercent) / 100f;
            return width > 0f && length > 0f && duration > 0f;
        }

        /// <summary>
        /// カットの火花は進行方向に伸ばして描かれ、見た目の長さは「太さ × lengthScale + 速さ × velocityScale」。
        /// 幅は大きさ、長さは2つの係数で変え、lengthScale を幅の倍率で割って幅を変えても長さが変わらないようにする。
        /// </summary>
        private static void ScaleShape(ParticleSystem ps, float width, float length)
        {
            var main = ps.main;
            if (main.startSize3D)
            {
                main.startSizeX = ScaleCurve(main.startSizeX, width);
                main.startSizeY = ScaleCurve(main.startSizeY, width);
                main.startSizeZ = ScaleCurve(main.startSizeZ, width);
            }
            else
            {
                main.startSize = ScaleCurve(main.startSize, width);
            }

            if (ps.TryGetComponent(out ParticleSystemRenderer renderer))
            {
                renderer.lengthScale *= length / width;
                renderer.velocityScale *= length;
            }
        }

        /// <summary>
        /// 値の形式（定数・2つの定数の間でランダム・曲線）ごとに倍率を掛ける。
        /// 2つの定数の形式では、乗数だけを変えても下限に効かない可能性があるため、両方の値に掛ける。
        /// </summary>
        private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float scale)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    curve.constant *= scale;
                    break;
                case ParticleSystemCurveMode.TwoConstants:
                    curve.constantMin *= scale;
                    curve.constantMax *= scale;
                    break;
                default:
                    curve.curveMultiplier *= scale;
                    break;
            }

            return curve;
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
