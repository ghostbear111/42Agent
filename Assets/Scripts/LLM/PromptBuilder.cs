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
    }
}
