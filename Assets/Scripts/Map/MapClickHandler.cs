/// <summary>
/// 地图点击处理器
/// 处理玩家在地图上的点击事件
/// 点击基地/Agent/资源时触发对应UI面板
/// </summary>
using GalaxyAgent.Core;
using GalaxyAgent.Data.Enums;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace GalaxyAgent.Map
{
    public class MapClickHandler : MonoBehaviour
    {
        // Tilemap引用
        private Tilemap _tilemap;
        // 摄像机引用
        private Camera _camera;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(Tilemap tilemap)
        {
            _tilemap = tilemap;
            _camera = Camera.main;
        }

        private void Update()
        {
            if (_tilemap == null || _camera == null) return;

            // 鼠标左键点击
            if (Input.GetMouseButtonDown(0))
            {
                // 将鼠标屏幕坐标转为世界坐标
                Vector3 worldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
                // 将世界坐标转为格子坐标
                Vector3Int cellPos = _tilemap.WorldToCell(worldPos);

                // 发布地图点击事件
                EventBus.Publish(new MapClickedEvent
                {
                    TilePosition = new Vector2Int(cellPos.x, cellPos.y)
                });
            }
        }
    }
}
