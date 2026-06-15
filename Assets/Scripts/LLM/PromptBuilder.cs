/// <summary>
/// 提示词构建器
/// 将游戏状态转换为结构化的LLM提示词
/// </summary>
using System.Collections.Generic;
using System.Text;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;

namespace GalaxyAgent.LLM
{
    public static class PromptBuilder
    {
        /// <summary>
        /// 构建Agent决策提示词
        /// </summary>
        public static string BuildAgentDecisionPrompt(AgentData agent,
            List<ResourceNodeData> nearbyResources, List<ThreatData> nearbyThreats,
            string sharedMemory, string recentMemories)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"你是{agent.DisplayName}，{GetTypeName(agent.AgentType)}型星球探索Agent。");
            sb.AppendLine();
            sb.AppendLine("==当前状态==");
            sb.AppendLine($"生命: {agent.Health:F0}/{agent.MaxHealth:F0}");
            sb.AppendLine($"饥饿: {agent.Hunger:F0}/100");
            sb.AppendLine($"能量: {agent.Energy:F0}/100");
            sb.AppendLine($"携带: {(agent.CarryingType.HasValue ? $"{agent.CarryingType.Value} x{agent.CarryingAmount:F0}" : "空手")}");
            sb.AppendLine($"当前任务: {agent.CurrentTask}");
            sb.AppendLine($"位置: ({agent.Position.x:F0}, {agent.Position.y:F0})");
            sb.AppendLine();

            sb.AppendLine("==周围环境==");
            if (nearbyResources.Count > 0)
            {
                sb.AppendLine($"发现 {nearbyResources.Count} 个资源点:");
                foreach (var r in nearbyResources)
                    sb.AppendLine($"  - {r.Name} 剩余:{r.Amount:F0}");
            }
            if (nearbyThreats.Count > 0)
            {
                sb.AppendLine($"警告: 发现 {nearbyThreats.Count} 个威胁:");
                foreach (var t in nearbyThreats)
                    sb.AppendLine($"  - {t.Name} 生命:{t.Health:F0}");
            }
            if (nearbyResources.Count == 0 && nearbyThreats.Count == 0)
                sb.AppendLine("周围安全，无特殊发现。");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(sharedMemory))
            {
                sb.AppendLine("==团队共享信息==");
                sb.AppendLine(sharedMemory);
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(recentMemories))
            {
                sb.AppendLine("==最近记忆==");
                sb.AppendLine(recentMemories);
                sb.AppendLine();
            }

            sb.AppendLine("==决策要求==");
            sb.AppendLine("请选择你的下一步行动，以JSON格式回复:");
            sb.AppendLine("{\"action\":\"explore/gather/return/flee/rest\",\"direction\":\"north/south/east/west/stay\",");
            sb.AppendLine(" \"target\":\"具体目标描述\",\"reasoning\":\"决策理由\"}");

            return sb.ToString();
        }

        /// <summary>
        /// 构建系统提示词
        /// </summary>
        public static string BuildSystemPrompt()
        {
            return "你是一个星球探索AI Agent的高层决策系统。" +
                   "目标：确保Agent生存并完成探索任务。优先级: 生存 > 完成任务 > 探索未知 > 采集资源。" +
                   "你必须【只】回复一行JSON，格式严格如下，不得增删字段、不得输出思考或解释或其它任何文字：" +
                   "{\"action\":\"explore|gather|return|flee|rest\"," +
                   "\"direction\":\"north|south|east|west|stay\"," +
                   "\"target\":\"具体目标\",\"reasoning\":\"一句话理由\"}。" +
                   "当action为explore时direction必填，其余情况direction填stay。";
        }

        private static string GetTypeName(AgentType type)
        {
            return type switch
            {
                AgentType.Scout => "探索者",
                AgentType.Worker => "采集者",
                AgentType.Guard => "守卫",
                _ => "通用"
            };
        }

        // ==================== 星球生成（LLM 自然语言创建星球） ====================

        /// <summary>
        /// 地球基准描述（作为玩家描述的对比标尺）。
        /// 调整地球参数时只需改这一处，系统提示词会引用它。
        /// </summary>
        public const string EARTH_BASELINE =
            "地图大小=中型(Medium)、瓦片精度=Size64、地形复杂度=丰富(Rich)、" +
            "资源丰富度=适中(Moderate)、风险等级=中(Medium)、天气模式=温和(Mild)、昼夜模式=交替(Alternating)。";

        /// <summary>
        /// 星球生成向导的系统提示词（三段式）。
        /// 要求模型：理解玩家意图 → 说明为何如此创造（对比地球基准）→ 撰写星球介绍档案，
        /// 并【只】输出一行结构化 JSON（含 understanding/reasoning/description 三段文本 + 各枚举）。
        /// /no_think 由 OllamaProvider 自动追加，这里无需再写。
        /// </summary>
        public static string BuildPlanetCreationSystemPrompt()
        {
            return "你是星际探索任务中的「星球生成向导AI」。玩家会用自然语言描述想要的星球。\n" +
                   "【输出要求（最重要）】你必须【只】回复一行JSON，不得输出思考、解释、markdown代码块或任何JSON之外的文字。" +
                   "JSON必须包含下列全部字段，缺一不可。特别强调：mapSize/tileSize/terrain/resources/risk/weather/dayNight 这7个枚举字段" +
                   "必须是独立的JSON键值对，绝不能只在 reasoning 文本里提及而不作为字段输出。\n" +
                   "【字段清单（严格按此顺序输出）】\n" +
                   "1. planetName：中文星球名\n" +
                   "2. mapSize：\"Tiny\"或\"Small\"或\"Medium\"或\"Large\"或\"Huge\"\n" +
                   "3. tileSize：\"Size32\"或\"Size64\"或\"Size128\"\n" +
                   "4. terrain：\"Flat\"或\"Rich\"或\"Dangerous\"\n" +
                   "5. resources：\"Scarce\"或\"Moderate\"或\"Rich\"\n" +
                   "6. risk：\"Low\"或\"Medium\"或\"High\"\n" +
                   "7. weather：\"Mild\"或\"Variable\"或\"Harsh\"\n" +
                   "8. dayNight：\"EternalDay\"或\"Alternating\"或\"EternalNight\"\n" +
                   "9. understanding：你对玩家意图的理解，1-2句中文\n" +
                   "10. reasoning：为何如此创造，逐项说明各维度相对地球的选择与理由，中文\n" +
                   "11. description：星球介绍档案，2-4句中文，适合游戏内展示\n" +
                   "【地球基准】" + EARTH_BASELINE + "\n" +
                   "对比维度：地图大小、瓦片精度、地形复杂度、资源丰富度、风险等级、天气模式、昼夜模式，全部以地球为参照。\n" +
                   "【示例】玩家说\"和地球一样\"时，你应输出：\n" +
                   "{\"planetName\":\"新地球\",\"mapSize\":\"Medium\",\"tileSize\":\"Size64\",\"terrain\":\"Rich\",\"resources\":\"Moderate\",\"risk\":\"Medium\",\"weather\":\"Mild\",\"dayNight\":\"Alternating\",\"understanding\":\"您希望寻找一颗与地球相似的星球\",\"reasoning\":\"各项参数均取地球基准值\",\"description\":\"新地球是一颗与地球高度相似的宜居星球。\"}\n" +
                   "要求：reasoning 偏参数决策说明，description 偏世界观叙述，二者不要重复。" +
                   "玩家若说\"和地球一样/类似地球\"，各项取地球基准值；描述含糊时合理推断。";
        }

        /// <summary>
        /// 星球生成的用户提示词（拼入玩家描述）。
        /// </summary>
        public static string BuildPlanetCreationUserPrompt(string userDescription)
        {
            return $"玩家描述：{userDescription}\n请据此生成星球，只输出上述JSON一行。";
        }
    }
}
