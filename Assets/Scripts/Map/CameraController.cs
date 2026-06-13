/// <summary>
/// 摄像机控制器
/// 支持鼠标拖拽平移和滚轮缩放，用于在游戏场景中浏览地图
///
/// 操作方式：
///   - 鼠标中键拖拽：平移摄像机
///   - 鼠标右键拖拽：平移摄像机（备选）
///   - 鼠标滚轮：缩放视野
///
/// 边界限制：摄像机不会移出地图范围
/// </summary>
using UnityEngine;

namespace GalaxyAgent.Map
{
    public class CameraController : MonoBehaviour
    {
        // ==================== 配置参数 ====================

        [Header("平移设置")]
        [Tooltip("拖拽灵敏度（值越大拖拽越快）")]
        public float dragSpeed = 1f;

        [Header("缩放设置")]
        [Tooltip("最小正交尺寸（最大放大）")]
        public float minOrthographicSize = 5f;
        [Tooltip("最大正交尺寸（最大缩小）")]
        public float maxOrthographicSize = 60f;
        [Tooltip("滚轮缩放速度")]
        public float zoomSpeed = 5f;

        // ==================== 内部状态 ====================

        // 拖拽相关
        private Vector3 _lastMouseWorldPos;
        private bool _isDragging;

        // 地图边界
        private float _mapMinX, _mapMaxX, _mapMinY, _mapMaxY;
        private bool _hasBounds;

        // 摄像机引用
        private Camera _camera;

        // ==================== 初始化 ====================

        /// <summary>
        /// 设置地图边界（限制摄像机不超出地图范围）
        /// </summary>
        /// <param name="mapWidth">地图边长（格数）</param>
        public void SetMapBounds(float mapWidth)
        {
            _mapMinX = 0f;
            _mapMaxX = mapWidth;
            _mapMinY = 0f;
            _mapMaxY = mapWidth;
            _hasBounds = true;

            // 立即限制摄像机位置
            ClampCameraPosition();
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        // ==================== 每帧更新 ====================

        private void Update()
        {
            HandleDrag();
            HandleZoom();
        }

        // ==================== 拖拽平移 ====================

        /// <summary>
        /// 处理鼠标拖拽平移
        /// 中键或右键按下时记录起始位置，按住期间计算世界坐标差值移动摄像机
        /// </summary>
        private void HandleDrag()
        {
            // 中键或右键按下 → 开始拖拽
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                _lastMouseWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
                _isDragging = true;
            }

            // 松开 → 停止拖拽
            if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
            {
                _isDragging = false;
            }

            // 拖拽中：计算鼠标在世界坐标中的位移，反向移动摄像机
            if (_isDragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
            {
                Vector3 currentMouseWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = _lastMouseWorldPos - currentMouseWorldPos;

                // 移动摄像机（乘以灵敏度）
                transform.position += delta * dragSpeed;

                // 限制在地图范围内
                ClampCameraPosition();

                // 更新参考点（移动后重新获取，避免累积误差）
                _lastMouseWorldPos = _camera.ScreenToWorldPoint(Input.mousePosition);
            }
        }

        // ==================== 滚轮缩放 ====================

        /// <summary>
        /// 处理鼠标滚轮缩放
        /// 滚轮向上 → 放大（减小orthographicSize）
        /// 滚轮向下 → 缩小（增大orthographicSize）
        /// </summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            // 获取缩放前的鼠标世界坐标（缩放中心点）
            Vector3 mouseWorldBefore = _camera.ScreenToWorldPoint(Input.mousePosition);

            // 调整正交尺寸
            _camera.orthographicSize -= scroll * zoomSpeed;
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize, minOrthographicSize, maxOrthographicSize);

            // 获取缩放后的鼠标世界坐标
            Vector3 mouseWorldAfter = _camera.ScreenToWorldPoint(Input.mousePosition);

            // 补偿摄像机位置，使鼠标指向的世界坐标点保持不变（像Google Maps那样的缩放体验）
            transform.position += mouseWorldBefore - mouseWorldAfter;

            // 限制在地图范围内
            ClampCameraPosition();
        }

        // ==================== 边界限制 ====================

        /// <summary>
        /// 将摄像机位置限制在地图范围内
        /// 根据当前orthographicSize计算可视区域边缘，确保不超出地图边界
        /// </summary>
        private void ClampCameraPosition()
        {
            if (!_hasBounds) return;

            Vector3 pos = transform.position;

            // 计算当前缩放下可视区域的半宽和半高
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            // 限制摄像机位置，使可视区域不超出地图
            pos.x = Mathf.Clamp(pos.x, _mapMinX + halfWidth, _mapMaxX - halfWidth);
            pos.y = Mathf.Clamp(pos.y, _mapMinY + halfHeight, _mapMaxY - halfHeight);

            // 保持Z轴不变（-10用于2D摄像机）
            // pos.z 不变

            transform.position = pos;
        }
    }
}
