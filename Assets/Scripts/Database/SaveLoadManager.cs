/// <summary>
/// 存档/读档管理器
/// 提供游戏存档的创建、加载、列表、删除功能
/// 将运行时游戏状态序列化到SQLite数据库
/// 使用SqliteConnection（P/Invoke封装）进行数据库操作
/// </summary>
using System;
using System.Collections.Generic;
using System.Globalization;
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using GalaxyAgent.Data.Models;
using GalaxyAgent.Map;
using UnityEngine;

namespace GalaxyAgent.Database
{
    public class SaveLoadManager
    {
        // 数据库管理器引用
        private DatabaseManager _db;

        /// <summary>
        /// 构造函数
        /// </summary>
        public SaveLoadManager(DatabaseManager db)
        {
            _db = db;
        }

        // ==================== 存档元数据操作 ====================

        /// <summary>
        /// 获取所有存档列表（按创建时间倒序）
        /// </summary>
        public List<GameSaveData> GetAllSaves()
        {
            var saves = new List<GameSaveData>();

            _db.ExecuteQuery(
                "SELECT save_id, planet_name, seed, map_size, tile_size, " +
                "terrain_type, resource_level, risk_level, weather_type, day_night_mode, " +
                "created_at, play_time_seconds, game_day, game_time_seconds FROM saves ORDER BY created_at DESC",
                columns =>
                {
                    saves.Add(new GameSaveData
                    {
                        SaveId = columns[0],
                        PlanetName = columns[1],
                        Seed = int.Parse(columns[2]),
                        MapSize = int.Parse(columns[3]),
                        TileSize = int.Parse(columns[4]),
                        TerrainType = ParseEnum<TerrainComplexity>(columns[5]),
                        ResourceLevel = ParseEnum<ResourceAbundance>(columns[6]),
                        RiskLevel = ParseEnum<RiskLevel>(columns[7]),
                        WeatherType = ParseEnum<WeatherPattern>(columns[8]),
                        DayNightMode = ParseEnum<DayNightMode>(columns[9]),
                        CreatedAt = columns[10],
                        PlayTimeSeconds = ParseFloat(columns[11]),
                        GameDay = int.Parse(columns[12]),
                        GameTimeSeconds = columns.Length > 13 ? ParseFloat(columns[13]) : 0f
                    });
                });

            return saves;
        }

        /// <summary>
        /// 获取单个存档数据
        /// </summary>
        public GameSaveData GetSave(string saveId)
        {
            GameSaveData save = null;
            string safeId = DatabaseManager.Escape(saveId);
            _db.ExecuteQuery(
                $"SELECT save_id, planet_name, seed, map_size, tile_size, " +
                $"terrain_type, resource_level, risk_level, weather_type, day_night_mode, " +
                $"created_at, play_time_seconds, game_day, game_time_seconds, " +
                $"llm_url, llm_model FROM saves WHERE save_id = '{safeId}'",
                columns =>
                {
                    save = new GameSaveData
                    {
                        SaveId = columns[0],
                        PlanetName = columns[1],
                        Seed = int.Parse(columns[2]),
                        MapSize = int.Parse(columns[3]),
                        TileSize = int.Parse(columns[4]),
                        TerrainType = ParseEnum<TerrainComplexity>(columns[5]),
                        ResourceLevel = ParseEnum<ResourceAbundance>(columns[6]),
                        RiskLevel = ParseEnum<RiskLevel>(columns[7]),
                        WeatherType = ParseEnum<WeatherPattern>(columns[8]),
                        DayNightMode = ParseEnum<DayNightMode>(columns[9]),
                        CreatedAt = columns[10],
                        PlayTimeSeconds = ParseFloat(columns[11]),
                        GameDay = int.Parse(columns[12]),
                        GameTimeSeconds = columns.Length > 13 ? ParseFloat(columns[13]) : 0f,
                        LlmUrl = columns.Length > 14 ? (columns[14] ?? "") : "",
                        LlmModel = columns.Length > 15 ? (columns[15] ?? "") : ""
                    };
                    Debug.Log($"[SaveLoadManager] 读取存档: gameDay={save.GameDay}, gameTimeSeconds={save.GameTimeSeconds:F1}, llmUrl={save.LlmUrl}, llmModel={save.LlmModel}, columns.Length={columns.Length}");
                });
            return save;
        }

        /// <summary>
        /// 检查是否存在任何存档
        /// </summary>
        public bool HasAnySave()
        {
            string result = _db.ExecuteScalar("SELECT COUNT(*) FROM saves");
            return result != null && int.Parse(result) > 0;
        }

        /// <summary>
        /// 删除存档（级联删除所有关联数据）
        /// </summary>
        public void DeleteSave(string saveId)
        {
            string safeId = DatabaseManager.Escape(saveId);

            // 删除关联的所有数据
            _db.ExecuteNonQuery($"DELETE FROM agent_states WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM base_state WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM resources WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM threats WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM modified_tiles WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM zones WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM agent_memories WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM procedural_memory WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM shared_memories WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM unlocked_techs WHERE save_id = '{safeId}'");
            _db.ExecuteNonQuery($"DELETE FROM saves WHERE save_id = '{safeId}'");

            Debug.Log($"[SaveLoadManager] 存档已删除: {saveId}");
        }

        // ==================== 新游戏存档 ====================

        /// <summary>
        /// 创建新游戏存档（在地图生成后调用）
        /// </summary>
        public string CreateNewSave(MapGenerator mapGenerator, MapConfig config, int seed,
            AgentData[] agents, Vector2 basePosition)
        {
            string saveId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string safePlanetName = DatabaseManager.Escape(config.PlanetName);

            // 插入存档元数据（新游戏时game_time_seconds为0；LLM配置留空，由游戏内首次保存写入）
            _db.ExecuteNonQuery($@"
                INSERT INTO saves (save_id, planet_name, seed, map_size, tile_size,
                    terrain_type, resource_level, risk_level, weather_type, day_night_mode,
                    created_at, play_time_seconds, game_day, game_time_seconds,
                    llm_url, llm_model)
                VALUES ('{saveId}', '{safePlanetName}', {seed}, {config.MapWidth},
                    {config.PixelSize}, '{config.Terrain}', '{config.Resources}',
                    '{config.Risk}', '{config.Weather}', '{config.DayNight}',
                    '{now}', 0, 1, 0, '', '')");

            // 保存资源节点
            foreach (var res in mapGenerator.Resources)
            {
                _db.ExecuteNonQuery($@"
                    INSERT INTO resources (save_id, resource_type, position_x, position_y,
                        amount, max_amount, hardness, name)
                    VALUES ('{saveId}', '{res.ResourceType}', {res.Position.x}, {res.Position.y},
                        {res.Amount}, {res.MaxAmount}, {res.Hardness}, '{DatabaseManager.Escape(res.Name)}')");
            }

            // 保存威胁
            foreach (var threat in mapGenerator.Threats)
            {
                _db.ExecuteNonQuery($@"
                    INSERT INTO threats (save_id, threat_type, name, position_x, position_y,
                        health, max_health, damage, detection_range, attack_range, is_alive, threat_level)
                    VALUES ('{saveId}', '{DatabaseManager.Escape(threat.ThreatType)}',
                        '{DatabaseManager.Escape(threat.Name)}', {threat.Position.x}, {threat.Position.y},
                        {threat.Health}, {threat.MaxHealth}, {threat.Damage},
                        {threat.DetectionRange}, {threat.AttackRange},
                        {(threat.IsAlive ? 1 : 0)}, {threat.ThreatLevel})");
            }

            // 保存Agent状态
            foreach (var agent in agents)
            {
                SaveAgentState(saveId, agent);
            }

            // 保存基地状态
            _db.ExecuteNonQuery($@"
                INSERT INTO base_state (save_id, position_x, position_y, health, storage_json)
                VALUES ('{saveId}', {basePosition.x}, {basePosition.y}, 100, '{{}}')");

            Debug.Log($"[SaveLoadManager] 新存档已创建: {saveId} - {config.PlanetName}");
            return saveId;
        }

        // ==================== Agent状态存取 ====================

        /// <summary>
        /// 保存单个Agent的状态
        /// </summary>
        public void SaveAgentState(string saveId, AgentData agent)
        {
            string safeId = DatabaseManager.Escape(saveId);
            string safeAgentId = DatabaseManager.Escape(agent.AgentId);
            string carryingType = agent.CarryingType.HasValue ? agent.CarryingType.Value.ToString() : "NULL";
            string inventoryJson = SerializeStorage(agent.Inventory);

            _db.ExecuteNonQuery($@"
                INSERT OR REPLACE INTO agent_states
                    (agent_id, save_id, agent_type, display_name, position_x, position_y,
                     health, max_health, hunger, energy, carrying_type, carrying_amount, max_carry,
                     current_state, current_task, attack_power, defense, explore_speed,
                     gather_efficiency, level, inventory_json)
                VALUES ('{safeAgentId}', '{safeId}', '{agent.AgentType}',
                    '{DatabaseManager.Escape(agent.DisplayName)}',
                    {agent.Position.x}, {agent.Position.y},
                    {agent.Health}, {agent.MaxHealth}, {agent.Hunger}, {agent.Energy},
                    '{carryingType}', {agent.CarryingAmount}, {agent.MaxCarry},
                    '{agent.CurrentState}', '{DatabaseManager.Escape(agent.CurrentTask)}',
                    {agent.AttackPower}, {agent.Defense}, {agent.ExploreSpeed},
                    {agent.GatherEfficiency}, {agent.Level}, '{DatabaseManager.Escape(inventoryJson)}')");
        }

        /// <summary>
        /// 加载指定存档的所有Agent状态
        /// </summary>
        public List<AgentData> LoadAgentStates(string saveId)
        {
            var agents = new List<AgentData>();
            string safeId = DatabaseManager.Escape(saveId);

            _db.ExecuteQuery(
                $"SELECT agent_id, agent_type, display_name, position_x, position_y, " +
                $"health, max_health, hunger, energy, carrying_type, carrying_amount, max_carry, " +
                $"current_state, current_task, attack_power, defense, explore_speed, " +
                $"gather_efficiency, level, inventory_json FROM agent_states WHERE save_id = '{safeId}'",
                columns =>
                {
                    var agent = new AgentData
                    {
                        AgentId = columns[0],
                        AgentType = ParseEnum<AgentType>(columns[1]),
                        DisplayName = columns[2],
                        Position = new Vector2(ParseFloat(columns[3]), ParseFloat(columns[4])),
                        Health = ParseFloat(columns[5]),
                        MaxHealth = ParseFloat(columns[6]),
                        Hunger = ParseFloat(columns[7]),
                        Energy = ParseFloat(columns[8]),
                        CarryingAmount = ParseFloat(columns[10]),
                        MaxCarry = ParseFloat(columns[11]),
                        CurrentState = ParseEnum<AgentState>(columns[12]),
                        CurrentTask = columns[13],
                        AttackPower = ParseFloat(columns[14]),
                        Defense = ParseFloat(columns[15]),
                        ExploreSpeed = ParseFloat(columns[16]),
                        GatherEfficiency = ParseFloat(columns[17]),
                        Level = int.Parse(columns[18])
                    };

                    // 解析携带资源类型
                    string carryingStr = columns[9];
                    if (!string.IsNullOrEmpty(carryingStr) && carryingStr != "NULL")
                    {
                        agent.CarryingType = ParseEnum<ResourceType>(carryingStr);
                    }

                    var inventory = columns.Length > 19
                        ? DeserializeStorage(columns[19])
                        : new Dictionary<ResourceType, float>();
                    if (inventory.Count == 0 && agent.CarryingType.HasValue && agent.CarryingAmount > 0f)
                    {
                        // 兼容旧存档：用旧版携带字段重建单槽背包。
                        inventory[agent.CarryingType.Value] = agent.CarryingAmount;
                    }
                    agent.SetInventory(inventory);

                    agents.Add(agent);
                });

            return agents;
        }

        // ==================== 游戏保存（覆盖已有存档） ====================

        /// <summary>
        /// 保存当前游戏状态
        /// </summary>
        /// <param name="llmUrl">当前LLM服务地址（随存档保存，加载后恢复）</param>
        /// <param name="llmModel">当前LLM模型名（随存档保存，加载后恢复）</param>
        public void SaveGame(string saveId, AgentData[] agents, Vector2 basePosition,
            float playTime, int gameDay, Dictionary<ResourceType, float> baseStorage,
            float gameTimeSeconds = 0f, string llmUrl = "", string llmModel = "")
        {
            if (string.IsNullOrEmpty(saveId))
            {
                Debug.LogError("[SaveLoadManager] 保存失败：当前存档ID为空");
                return;
            }

            // 更新存档元数据（包含游戏内时间秒数，决定加载后的昼夜时刻；
            // 以及当前LLM配置，便于加载后恢复相同的服务地址与模型）
            _db.ExecuteNonQuery($@"
                UPDATE saves SET
                    play_time_seconds = {playTime},
                    game_day = {gameDay},
                    game_time_seconds = {gameTimeSeconds},
                    llm_url = '{DatabaseManager.Escape(llmUrl ?? "")}',
                    llm_model = '{DatabaseManager.Escape(llmModel ?? "")}'
                WHERE save_id = '{DatabaseManager.Escape(saveId)}'");

            Debug.Log($"[SaveLoadManager] 保存时间数据: gameDay={gameDay}, gameTimeSeconds={gameTimeSeconds:F1}, playTime={playTime:F1}");

            // 更新Agent状态
            foreach (var agent in agents)
            {
                SaveAgentState(saveId, agent);
            }

            // 更新基地状态
            string storageJson = SerializeStorage(baseStorage);
            _db.ExecuteNonQuery($@"
                UPDATE base_state SET
                    position_x = {basePosition.x},
                    position_y = {basePosition.y},
                    storage_json = '{DatabaseManager.Escape(storageJson)}'
                WHERE save_id = '{DatabaseManager.Escape(saveId)}'");

            // 持久化已解锁科技集合（先清后写，保持与当前内存集合一致）
            _db.ExecuteNonQuery($"DELETE FROM unlocked_techs WHERE save_id = '{DatabaseManager.Escape(saveId)}'");
            var unlocked = GalaxyAgent.Tech.TechTreeManager.Instance != null
                ? GalaxyAgent.Tech.TechTreeManager.Instance.GetUnlockedForSave()
                : null;
            if (unlocked != null)
            {
                string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                foreach (var techId in unlocked)
                {
                    if (string.IsNullOrEmpty(techId)) continue;
                    _db.ExecuteNonQuery(
                        $"INSERT INTO unlocked_techs (save_id, tech_id, unlocked_at) VALUES (" +
                        $"'{DatabaseManager.Escape(saveId)}', '{DatabaseManager.Escape(techId)}', '{now}')");
                }
            }

            Debug.Log($"[SaveLoadManager] 游戏已保存: {saveId} (第{gameDay}天, {playTime:F0}秒)");
        }

        /// <summary>
        /// 加载已解锁科技集合（加载存档时调用，恢复 TechTreeManager 解锁状态）
        /// </summary>
        public List<string> LoadUnlockedTechs(string saveId)
        {
            var ids = new List<string>();
            string safeId = DatabaseManager.Escape(saveId);
            _db.ExecuteQuery(
                $"SELECT tech_id FROM unlocked_techs WHERE save_id = '{safeId}'",
                columns =>
                {
                    if (columns != null && columns.Length > 0 && !string.IsNullOrEmpty(columns[0]))
                        ids.Add(columns[0]);
                });
            return ids;
        }

        /// <summary>
        /// 加载基地仓库数据
        /// </summary>
        public Dictionary<ResourceType, float> LoadBaseStorage(string saveId)
        {
            var storage = new Dictionary<ResourceType, float>();
            string safeId = DatabaseManager.Escape(saveId);

            _db.ExecuteQuery(
                $"SELECT storage_json FROM base_state WHERE save_id = '{safeId}'",
                columns =>
                {
                    storage = DeserializeStorage(columns[0]);
                });

            return storage;
        }

        /// <summary>
        /// 获取基地位置
        /// </summary>
        public Vector2? LoadBasePosition(string saveId)
        {
            Vector2? pos = null;
            string safeId = DatabaseManager.Escape(saveId);

            _db.ExecuteQuery(
                $"SELECT position_x, position_y FROM base_state WHERE save_id = '{safeId}'",
                columns =>
                {
                    pos = new Vector2(ParseFloat(columns[0]), ParseFloat(columns[1]));
                });

            return pos;
        }

        // ==================== 辅助方法 ====================

        /// <summary>
        /// 安全解析浮点数
        /// </summary>
        private static float ParseFloat(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0f;
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                return result;
            return float.TryParse(value, out result) ? result : 0f;
        }

        /// <summary>
        /// 安全解析枚举
        /// </summary>
        private static T ParseEnum<T>(string value) where T : struct
        {
            if (string.IsNullOrEmpty(value)) return default;
            return Enum.TryParse<T>(value, out T result) ? result : default;
        }

        /// <summary>
        /// 序列化基地仓库为简单JSON
        /// </summary>
        private static string SerializeStorage(Dictionary<ResourceType, float> storage)
        {
            if (storage == null || storage.Count == 0) return "{}";

            var parts = new List<string>();
            foreach (var kvp in storage)
            {
                parts.Add($"\"{kvp.Key}\":{kvp.Value.ToString(CultureInfo.InvariantCulture)}");
            }
            return "{" + string.Join(",", parts) + "}";
        }

        /// <summary>
        /// 从简单JSON反序列化基地仓库
        /// </summary>
        private static Dictionary<ResourceType, float> DeserializeStorage(string json)
        {
            var storage = new Dictionary<ResourceType, float>();
            if (string.IsNullOrEmpty(json) || json == "{}") return storage;

            try
            {
                json = json.Trim('{', '}');
                foreach (var pair in json.Split(','))
                {
                    var kv = pair.Split(':');
                    if (kv.Length == 2)
                    {
                        string key = kv[0].Trim().Trim('"');
                        if (Enum.TryParse<ResourceType>(key, out var resType))
                        {
                            storage[resType] = ParseFloat(kv[1].Trim());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveLoadManager] 仓库数据解析失败: {e.Message}");
            }

            return storage;
        }
    }
}
