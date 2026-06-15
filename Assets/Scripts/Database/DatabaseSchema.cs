/// <summary>
/// 数据库表结构定义
/// 集中管理所有建表SQL语句和数据库版本迁移
/// 使用SqliteConnection直接执行SQL
/// </summary>
using UnityEngine;

namespace GalaxyAgent.Database
{
    public static class DatabaseSchema
    {
        // 当前数据库版本
        private const int DB_VERSION = 6;

        /// <summary>
        /// 创建所有表（如果不存在）
        /// </summary>
        public static void CreateTables(SqliteConnection connection)
        {
            // 游戏存档元数据
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS saves (
                    save_id         TEXT PRIMARY KEY,
                    planet_name     TEXT NOT NULL,
                    seed            INTEGER NOT NULL,
                    map_size        INTEGER NOT NULL,
                    tile_size       INTEGER NOT NULL,
                    terrain_type    TEXT NOT NULL,
                    resource_level  TEXT NOT NULL,
                    risk_level      TEXT NOT NULL,
                    weather_type    TEXT NOT NULL,
                    day_night_mode  TEXT NOT NULL,
                    created_at      TEXT NOT NULL,
                    play_time_seconds REAL NOT NULL DEFAULT 0,
                    game_day        INTEGER NOT NULL DEFAULT 1,
                    game_time_seconds REAL NOT NULL DEFAULT 0
                );");

            // 版本迁移：旧数据库可能缺少game_time_seconds列
            if (!ColumnExists(connection, "saves", "game_time_seconds"))
            {
                connection.ExecuteNonQuery("ALTER TABLE saves ADD COLUMN game_time_seconds REAL NOT NULL DEFAULT 0");
            }

            // 版本迁移：LLM配置列（随存档保存/恢复当前LLM服务地址与模型）
            if (!ColumnExists(connection, "saves", "llm_url"))
            {
                connection.ExecuteNonQuery("ALTER TABLE saves ADD COLUMN llm_url TEXT NOT NULL DEFAULT ''");
            }
            if (!ColumnExists(connection, "saves", "llm_model"))
            {
                connection.ExecuteNonQuery("ALTER TABLE saves ADD COLUMN llm_model TEXT NOT NULL DEFAULT ''");
            }

            // 版本迁移：星球介绍列（LLM 创建星球时生成，游戏内顶栏点击星球名可查看）
            if (!ColumnExists(connection, "saves", "planet_description"))
            {
                connection.ExecuteNonQuery("ALTER TABLE saves ADD COLUMN planet_description TEXT NOT NULL DEFAULT ''");
            }

            // 地图区域数据
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS zones (
                    zone_id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    save_id         TEXT NOT NULL,
                    zone_x          INTEGER NOT NULL,
                    zone_y          INTEGER NOT NULL,
                    zone_width      INTEGER NOT NULL,
                    zone_height     INTEGER NOT NULL,
                    biome           TEXT NOT NULL,
                    temperature     REAL NOT NULL DEFAULT 20,
                    radiation       REAL NOT NULL DEFAULT 0,
                    visibility      REAL NOT NULL DEFAULT 1.0,
                    visited         INTEGER NOT NULL DEFAULT 0,
                    memory_color    TEXT NOT NULL DEFAULT 'Grey',
                    risk_score      REAL NOT NULL DEFAULT 0,
                    resource_value  REAL NOT NULL DEFAULT 0,
                    last_visited    TEXT NOT NULL DEFAULT '',
                    known_resources TEXT NOT NULL DEFAULT '',
                    known_threats   TEXT NOT NULL DEFAULT '',
                    visited_by      TEXT NOT NULL DEFAULT ''
                );");

            // 已修改的瓦片
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS modified_tiles (
                    tile_key        TEXT PRIMARY KEY,
                    save_id         TEXT NOT NULL,
                    tile_x          INTEGER NOT NULL,
                    tile_y          INTEGER NOT NULL,
                    tile_type       TEXT NOT NULL,
                    biome           TEXT NOT NULL DEFAULT 'Grassland',
                    properties_json TEXT
                );");

            // 资源节点
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS resources (
                    resource_id     INTEGER PRIMARY KEY AUTOINCREMENT,
                    save_id         TEXT NOT NULL,
                    resource_type   TEXT NOT NULL,
                    position_x      INTEGER NOT NULL,
                    position_y      INTEGER NOT NULL,
                    amount          REAL NOT NULL,
                    max_amount      REAL NOT NULL,
                    hardness        REAL NOT NULL DEFAULT 1,
                    name            TEXT NOT NULL DEFAULT ''
                );");

            // 威胁/敌人
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS threats (
                    threat_id       INTEGER PRIMARY KEY AUTOINCREMENT,
                    save_id         TEXT NOT NULL,
                    threat_type     TEXT NOT NULL,
                    name            TEXT NOT NULL DEFAULT '',
                    position_x      INTEGER NOT NULL,
                    position_y      INTEGER NOT NULL,
                    health          REAL NOT NULL,
                    max_health      REAL NOT NULL,
                    damage          REAL NOT NULL,
                    detection_range REAL NOT NULL DEFAULT 5,
                    attack_range    REAL NOT NULL DEFAULT 1.5,
                    is_alive        INTEGER NOT NULL DEFAULT 1,
                    threat_level    REAL NOT NULL DEFAULT 0.5
                );");

            // Agent状态快照
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS agent_states (
                    agent_id        TEXT NOT NULL,
                    save_id         TEXT NOT NULL,
                    agent_type      TEXT NOT NULL,
                    display_name    TEXT NOT NULL,
                    position_x      REAL NOT NULL,
                    position_y      REAL NOT NULL,
                    health          REAL NOT NULL,
                    max_health      REAL NOT NULL,
                    hunger          REAL NOT NULL,
                    energy          REAL NOT NULL,
                    carrying_type   TEXT,
                    carrying_amount REAL NOT NULL DEFAULT 0,
                    max_carry       REAL NOT NULL DEFAULT 50,
                    current_state   TEXT NOT NULL DEFAULT 'Idle',
                    current_task    TEXT NOT NULL DEFAULT '',
                    attack_power    REAL NOT NULL DEFAULT 10,
                    defense         REAL NOT NULL DEFAULT 5,
                    explore_speed   REAL NOT NULL DEFAULT 1,
                    gather_efficiency REAL NOT NULL DEFAULT 1,
                    level           INTEGER NOT NULL DEFAULT 1,
                    inventory_json  TEXT NOT NULL DEFAULT '{}',
                    PRIMARY KEY (agent_id, save_id)
                );");

            // 版本迁移：旧数据库只保存单一携带资源，新版本保存完整多槽背包
            if (!ColumnExists(connection, "agent_states", "inventory_json"))
            {
                connection.ExecuteNonQuery("ALTER TABLE agent_states ADD COLUMN inventory_json TEXT NOT NULL DEFAULT '{}'");
            }

            // 基地状态
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS base_state (
                    save_id         TEXT PRIMARY KEY,
                    position_x      REAL NOT NULL,
                    position_y      REAL NOT NULL,
                    health          REAL NOT NULL DEFAULT 100,
                    storage_json    TEXT NOT NULL DEFAULT '{}'
                );");

            // 已解锁科技（每存档独立的科技解锁集合，P1 科技系统）
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS unlocked_techs (
                    save_id         TEXT NOT NULL,
                    tech_id         TEXT NOT NULL,
                    unlocked_at     TEXT NOT NULL DEFAULT '',
                    PRIMARY KEY (save_id, tech_id)
                );");

            // Agent长期记忆
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS agent_memories (
                    memory_id       INTEGER PRIMARY KEY AUTOINCREMENT,
                    save_id         TEXT NOT NULL,
                    agent_id        TEXT NOT NULL,
                    memory_type     TEXT NOT NULL,
                    category        TEXT NOT NULL,
                    importance      REAL NOT NULL DEFAULT 0.5,
                    content_json    TEXT NOT NULL,
                    position_x      INTEGER,
                    position_y      INTEGER,
                    created_at      TEXT NOT NULL,
                    expires_at      TEXT
                );");

            // 过程记忆（学习规则）
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS procedural_memory (
                    rule_id         INTEGER PRIMARY KEY AUTOINCREMENT,
                    save_id         TEXT NOT NULL,
                    agent_id        TEXT NOT NULL,
                    rule_name       TEXT NOT NULL,
                    rule_context    TEXT NOT NULL,
                    weight          REAL NOT NULL DEFAULT 1.0,
                    success_count   INTEGER NOT NULL DEFAULT 0,
                    fail_count      INTEGER NOT NULL DEFAULT 0,
                    updated_at      TEXT NOT NULL
                );");

            // 共享团队记忆
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS shared_memories (
                    entry_id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    save_id         TEXT NOT NULL,
                    source_agent_id TEXT NOT NULL,
                    category        TEXT NOT NULL,
                    content_json    TEXT NOT NULL,
                    importance      REAL NOT NULL DEFAULT 0.5,
                    upvotes         INTEGER NOT NULL DEFAULT 0,
                    created_at      TEXT NOT NULL
                );");

            // 数据库版本记录
            connection.ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS db_version (
                    version         INTEGER PRIMARY KEY,
                    applied_at      TEXT NOT NULL
                );");

            // 记录当前版本
            string now = System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            connection.ExecuteNonQuery($@"
                INSERT OR IGNORE INTO db_version (version, applied_at)
                VALUES ({DB_VERSION}, '{now}');");

            Debug.Log("[DatabaseSchema] 数据库表结构创建/验证完成");
        }

        /// <summary>
        /// 检查表字段是否存在，用于无损迁移旧数据库
        /// </summary>
        private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            bool exists = false;
            connection.ExecuteQuery($"PRAGMA table_info({tableName})", columns =>
            {
                if (columns.Length > 1 && columns[1] == columnName)
                    exists = true;
            });
            return exists;
        }
    }
}
