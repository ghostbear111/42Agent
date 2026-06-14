/// <summary>
/// 游戏配置编辑器窗口（仅编辑器）
/// 通过菜单 Tools/游戏配置 打开，可视化编辑 game_config.json（与运行时 GameConfigManager 同一份文件）。
/// 保存后运行时单例下次访问即读到新值；若游戏正在运行，配置类系统实时读取会即时生效。
///
/// 注意：必须放在 Editor 文件夹下（Unity 自动排除出包），并用 #if UNITY_EDITOR 保护。
/// </summary>
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using GalaxyAgent.Config;

namespace GalaxyAgent.Editor
{
    public class GameConfigEditorWindow : EditorWindow
    {
        private GameConfig _config;
        private Vector2 _scroll;

        [MenuItem("Tools/游戏配置 (GameConfig)")]
        public static void Open()
        {
            GetWindow<GameConfigEditorWindow>("游戏配置");
        }

        private void OnEnable()
        {
            _config = GameConfigStore.Load();
        }

        private void OnGUI()
        {
            if (_config == null) _config = GameConfigStore.Load();

            GUILayout.Label("游戏配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "文件: " + GameConfigStore.GetPath() + "\n" +
                "保存后写入 game_config.json。运行时各系统实时读取，立即生效。",
                MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, false, true);

            DrawGroup("Agent 平衡", DrawAgent);
            DrawGroup("世界 / 时间", DrawWorld);
            DrawGroup("战斗 / 升级", DrawCombat);
            DrawGroup("采集", DrawGather);
            DrawGroup("探索发现", DrawDiscovery);
            DrawGroup("LLM", DrawLlm);
            DrawGroup("存档 / 自动保存", DrawSave);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("保存", GUILayout.Height(30)))
            {
                GameConfigStore.Save(_config);
            }
            if (GUILayout.Button("重新加载", GUILayout.Height(30)))
            {
                _config = GameConfigStore.Load();
            }
            if (GUILayout.Button("重置默认", GUILayout.Height(30)))
            {
                _config = GameConfigStore.CreateDefault();
                GameConfigStore.Save(_config);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawGroup(string title, System.Action drawer)
        {
            EditorGUILayout.BeginVertical("HelpBox");
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space();
            drawer();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawAgent()
        {
            var a = _config.Agent;
            a.PerceptionRadius = EditorGUILayout.IntField("感知半径(格)", a.PerceptionRadius);
            a.MoveSpeed = EditorGUILayout.FloatField("移动速度(格/秒)", a.MoveSpeed);
            a.MaxHealth = EditorGUILayout.FloatField("最大生命", a.MaxHealth);
            a.MaxHunger = EditorGUILayout.FloatField("最大饥饿", a.MaxHunger);
            a.MaxEnergy = EditorGUILayout.FloatField("最大能量", a.MaxEnergy);
            a.MaxCarry = EditorGUILayout.FloatField("最大携带量", a.MaxCarry);
            a.HungerDrain = EditorGUILayout.FloatField("饥饿消耗/秒", a.HungerDrain);
            a.EnergyDrain = EditorGUILayout.FloatField("能量消耗/秒", a.EnergyDrain);
            a.MidLevelDecisionInterval = EditorGUILayout.FloatField("中层决策间隔(秒)", a.MidLevelDecisionInterval);
            a.HighLevelMinInterval = EditorGUILayout.FloatField("高层决策最小间隔(秒)", a.HighLevelMinInterval);
            a.HighLevelMaxInterval = EditorGUILayout.FloatField("高层决策最大间隔(秒)", a.HighLevelMaxInterval);
        }

        private void DrawWorld()
        {
            var w = _config.World;
            w.TimeRatio = EditorGUILayout.FloatField("时间比例(288=5分/天)", w.TimeRatio);
            w.DayStartHour = EditorGUILayout.IntField("白天开始小时", w.DayStartHour);
            w.NightStartHour = EditorGUILayout.FloatField("夜晚开始小时", w.NightStartHour);
        }

        private void DrawCombat()
        {
            var c = _config.Combat;
            c.AttackCooldown = EditorGUILayout.FloatField("攻击冷却(秒)", c.AttackCooldown);
            c.MinDamage = EditorGUILayout.FloatField("最低伤害", c.MinDamage);
            c.ThreatAttackRange = EditorGUILayout.FloatField("威胁攻击范围(格)", c.ThreatAttackRange);
            c.KillThreatXP = EditorGUILayout.FloatField("击杀威胁经验", c.KillThreatXP);
            c.XpPerLevel = EditorGUILayout.FloatField("每级经验倍数", c.XpPerLevel);
            c.LevelUpHealPercent = EditorGUILayout.FloatField("升级回血比例", c.LevelUpHealPercent);
        }

        private void DrawGather()
        {
            var g = _config.Gather;
            g.BaseGatherTime = EditorGUILayout.FloatField("基础采集时间(秒)", g.BaseGatherTime);
            g.GatherResourceXP = EditorGUILayout.FloatField("采集经验", g.GatherResourceXP);
        }

        private void DrawDiscovery()
        {
            var d = _config.Discovery;
            d.Density = EditorGUILayout.FloatField("发现物密度(0-1)", d.Density);
            d.SampleInterval = EditorGUILayout.IntField("发现物采样间隔(格)", d.SampleInterval);
            d.DiscoveryXP = EditorGUILayout.FloatField("调查发现经验", d.DiscoveryXP);
        }

        private void DrawLlm()
        {
            var l = _config.Llm;
            l.Url = EditorGUILayout.TextField("服务地址", l.Url);
            l.Model = EditorGUILayout.TextField("模型名", l.Model);
            l.RequestTimeout = EditorGUILayout.FloatField("请求超时(秒)", l.RequestTimeout);
            l.MaxTokens = EditorGUILayout.IntField("最大Token", l.MaxTokens);
            l.ConversationLogMax = EditorGUILayout.IntField("对话记录上限", l.ConversationLogMax);
            l.EventTriggerCooldown = EditorGUILayout.FloatField("事件触发冷却(秒)", l.EventTriggerCooldown);
        }

        private void DrawSave()
        {
            var s = _config.Save;
            s.AutoSaveEnabled = EditorGUILayout.Toggle("启用自动保存", s.AutoSaveEnabled);
            s.AutoSaveInterval = EditorGUILayout.FloatField("自动保存间隔(秒)", s.AutoSaveInterval);
        }
    }
}
#endif
