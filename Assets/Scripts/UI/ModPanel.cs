/// <summary>
/// Mod 换图管理面板（主菜单用）。
/// 在 MainMenu 点击「Mod」按钮打开，让玩家管理游戏目录下的自定图片。
///
/// 内容：
/// ┌──────── 全屏半透明遮罩 ────────┐
/// │ ┌────── 居中 Mod 窗口 ──────┐   │
/// │ │ 标题：Mod 换图             │   │
/// │ │ Mod 目录: .../Mods         │   │
/// │ │ 当前 Mods/Sprites 有 N 张   │   │
/// │ │ [重新导出模板][打开文件夹]  │   │
/// │ │ 命名规则说明...            │   │
/// │ │ [关闭]                     │   │
/// │ └──────────────────────────┘   │
/// └────────────────────────────────┘
///
/// 交互：
/// - 重新导出模板：ModManager.ExportDefaultTemplate(false) 补全缺失模板图（不覆盖玩家已改图）
/// - 打开 Mods 文件夹：Application.OpenURL 用系统资源管理器打开 Mods 目录
/// 详细命名规则见 Mods/README.txt（导出模板时自动生成）。
/// </summary>
using System.IO;
using GalaxyAgent.Modding;
using UnityEngine;
using UnityEngine.UI;

namespace GalaxyAgent.UI
{
    public class ModPanel : MonoBehaviour
    {
        private GameObject _root;             // 全屏遮罩根
        private Button _exportBtn;            // 重新导出模板
        private Button _openFolderBtn;        // 打开 Mods 文件夹
        private Button _closeBtn;             // 关闭
        private Text _pathText;               // Mod 目录路径
        private Text _hintText;               // 当前图数 / 操作结果

        /// <summary>面板是否可见</summary>
        public bool IsVisible => _root != null && _root.activeSelf;

        // ==================== 构建 ====================

        /// <summary>运行时构建面板（幂等，由 MainMenuUI 调用一次）</summary>
        public void BuildUI(Transform parent)
        {
            RuntimeUIBuilder.EnsureEventSystem();

            _root = MakeFull("ModOverlay", parent);
            _root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var win = RuntimeUIBuilder.CreatePanel("ModWindow", _root.transform,
                new Color(0.07f, 0.07f, 0.13f, 0.98f), 0.22f, 0.16f, 0.78f, 0.86f);

            // 标题
            RuntimeUIBuilder.CreateText("Title", win.transform, "Mod 换图", 22,
                new Color(0.5f, 0.85f, 1f), TextAnchor.MiddleCenter, 0f, 0.90f, 1f, 0.98f);

            // Mod 目录路径（换行显示）
            _pathText = RuntimeUIBuilder.CreateText("Path", win.transform,
                "Mod 目录: " + ModManager.GetModDir(), 14,
                new Color(0.85f, 0.85f, 0.85f), TextAnchor.UpperLeft,
                0.05f, 0.78f, 0.95f, 0.89f);
            _pathText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 当前图数 / 操作结果提示
            _hintText = RuntimeUIBuilder.CreateText("Hint", win.transform, "", 14,
                new Color(0.9f, 0.85f, 0.4f), TextAnchor.UpperLeft,
                0.05f, 0.66f, 0.95f, 0.77f);
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 按钮行：重新导出模板 / 打开 Mods 文件夹
            _exportBtn = RuntimeUIBuilder.CreateButton("BtnExport", win.transform, "重新导出模板",
                new Color(0.2f, 0.4f, 0.3f), 0.06f, 0.52f, 0.46f, 0.63f);
            _openFolderBtn = RuntimeUIBuilder.CreateButton("BtnOpen", win.transform, "打开 Mods 文件夹",
                new Color(0.25f, 0.35f, 0.5f), 0.50f, 0.52f, 0.94f, 0.63f);

            // 命名规则说明
            var rule = RuntimeUIBuilder.CreateText("Rule", win.transform,
                "把同名 PNG 放进「Mods/Sprites」即替换对应元素（头像 / 资源 / 天气 / 背景 / 皮肤 / 图标）。\n" +
                "命名须完全一致（如 avatar_scout.png），改图后【重启游戏】生效。\n" +
                "删除某张图自动回退内置图。详见 Mods/README.txt。",
                13, new Color(0.7f, 0.7f, 0.7f), TextAnchor.UpperLeft,
                0.05f, 0.22f, 0.95f, 0.49f);
            rule.horizontalOverflow = HorizontalWrapMode.Wrap;

            // 关闭按钮
            _closeBtn = RuntimeUIBuilder.CreateButton("BtnClose", win.transform, "关闭",
                new Color(0.35f, 0.2f, 0.2f), 0.35f, 0.06f, 0.65f, 0.17f);

            // 绑定事件
            _exportBtn.onClick.AddListener(OnExport);
            _openFolderBtn.onClick.AddListener(OnOpenFolder);
            _closeBtn.onClick.AddListener(Hide);

            _root.SetActive(false);
            Debug.Log("[ModPanel] UI 构建完成");
        }

        // ==================== 显示/隐藏 ====================

        public void Show()
        {
            if (_pathText != null)
                _pathText.text = "Mod 目录: " + ModManager.GetModDir();
            RefreshHint();
            if (_root != null) _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        // ==================== 按钮事件 ====================

        /// <summary>重新导出模板：补全缺失模板图（不覆盖玩家已改图）+ 刷新 README</summary>
        private void OnExport()
        {
            int n = ModManager.ExportDefaultTemplate(overwrite: false);
            RefreshHint($"已补全 {n} 张模板图到 Mods/Sprites（已有图未覆盖）。重启游戏生效。");
        }

        /// <summary>用系统资源管理器打开 Mods 文件夹</summary>
        private void OnOpenFolder()
        {
            ModManager.EnsureModDirs(); // 确保目录存在再打开
            string dir = ModManager.GetModDir();
            // file:/// 前缀 + 正斜杠路径，Windows/macOS/Linux 资源管理器通用打开
            Application.OpenURL("file:///" + dir.Replace("\\", "/"));
        }

        /// <summary>刷新提示文本：显示当前 Mods/Sprites 已有图数</summary>
        private void RefreshHint(string extra = null)
        {
            if (_hintText == null) return;
            string dir = ModManager.GetModSpritesDir();
            int count = Directory.Exists(dir) ? Directory.GetFiles(dir, "*.png").Length : 0;
            _hintText.text = $"当前 Mods/Sprites 有 {count} 张图。" + (extra ?? "");
        }

        // ==================== 辅助 ====================

        /// <summary>创建撑满父级的 RectTransform 容器</summary>
        private static GameObject MakeFull(string name, Transform parent)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return obj;
        }
    }
}
