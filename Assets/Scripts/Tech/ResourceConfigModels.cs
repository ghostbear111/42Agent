/// <summary>
/// 资源配置数据模型
/// 让 ResourceType 的展示属性、可采集性、采集所需科技、文明归属全部可配。
/// 与科技树配合：ResourceTypeConfig.RequiredTech 引用 TechNode.Id，实现"采集绑科技"。
///
/// 设计要点：
/// - [Serializable] + 公共字段，兼容 JsonUtility（Color 是可序列化 struct）
/// - ResourceType 枚举保持不变（避免大规模重构），仅把"属性/采集条件"外置为配置
/// - CreateDefault 内置 5 资源默认配置（颜色取自 Constants），RequiredTech 默认空（无条件采集）
/// </summary>
using System;
using System.Collections.Generic;
using GalaxyAgent.Data.Enums;
using UnityEngine;

namespace GalaxyAgent.Tech
{
    /// <summary>单个资源配置</summary>
    [Serializable]
    public class ResourceTypeConfig
    {
        /// <summary>资源类型（枚举键）</summary>
        public ResourceType Type;
        /// <summary>显示名称（中文）</summary>
        public string DisplayName = "";
        /// <summary>描述说明</summary>
        public string Description = "";
        /// <summary>标识颜色（地图色块/UI方块）</summary>
        public Color Color = Color.white;
        /// <summary>是否可采集（基础开关，false 则永远不可采）</summary>
        public bool Gatherable = true;
        /// <summary>采集所需科技 Id（引用 TechNode.Id；空=无条件可采）</summary>
        public string RequiredTech = "";
        /// <summary>文明归属（编辑器按文明分组显示用）</summary>
        public CivLevel CivLevel = CivLevel.Outpost;
    }

    /// <summary>资源配置根（对应一份 resource_config.json）</summary>
    [Serializable]
    public class ResourceConfigData
    {
        /// <summary>全部资源配置</summary>
        public List<ResourceTypeConfig> Resources = new List<ResourceTypeConfig>();
        /// <summary>数据 schema 版本</summary>
        public int Version = 1;
    }
}
