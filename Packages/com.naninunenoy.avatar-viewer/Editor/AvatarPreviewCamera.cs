using UnityEngine;

namespace com.naninunenoy.avatar_viewer.Editor
{
    /// <summary>
    /// アバタープレビュー用カメラの制御
    /// </summary>
    public class AvatarPreviewCamera
    {
        const float DefaultCameraDistance = 3.0F;
        const float MinCameraDistance = 0.5F;
        const float MaxCameraDistance = 10.0F;
        const float OrbitSensitivity = 0.5F;
        const float PanSensitivity = 0.001F;
        const float ZoomSensitivity = 0.1F;
        const float MaxOrbitAngleX = 90.0F;
        const float MinOrbitAngleX = -90.0F;

        Vector2 _orbitRotation = new(0.0F, 0.0F);
        Vector3 _panOffset = Vector3.zero;
        
        float _distance = DefaultCameraDistance;

        /// <summary>
        /// カメラの位置を計算
        /// </summary>
        /// <returns>カメラの位置</returns>
        public Vector3 CalculatePosition()
        {
            var rotation = Quaternion.Euler(_orbitRotation.x, _orbitRotation.y, 0.0F);
            return _panOffset + rotation * Vector3.back * _distance;
        }

        /// <summary>
        /// カメラの回転を計算
        /// </summary>
        /// <returns>カメラの回転</returns>
        public Quaternion CalculateRotation()
        {
            return Quaternion.Euler(_orbitRotation.x, _orbitRotation.y, 0.0F);
        }

        /// <summary>
        /// オービット回転を適用
        /// </summary>
        /// <param name="delta">マウス移動量</param>
        public void ApplyOrbitRotation(Vector2 delta)
        {
            _orbitRotation.x += delta.y * OrbitSensitivity;
            _orbitRotation.y += delta.x * OrbitSensitivity;
            _orbitRotation.x = Mathf.Clamp(_orbitRotation.x, MinOrbitAngleX, MaxOrbitAngleX);
        }

        /// <summary>
        /// パン移動を適用
        /// </summary>
        /// <param name="delta">マウス移動量</param>
        /// <param name="cameraTransform">カメラのTransform</param>
        public void ApplyPanMovement(Vector2 delta, Transform cameraTransform)
        {
            var right = cameraTransform.right;
            var up = cameraTransform.up;
            
            var panSpeed = _distance * PanSensitivity;
            _panOffset -= right * delta.x * panSpeed;
            _panOffset += up * delta.y * panSpeed;
        }

        /// <summary>
        /// ズーム操作を適用
        /// </summary>
        /// <param name="scrollDelta">スクロール量</param>
        public void ApplyZoom(float scrollDelta)
        {
            _distance += scrollDelta * ZoomSensitivity;
            _distance = Mathf.Clamp(_distance, MinCameraDistance, MaxCameraDistance);
        }

        /// <summary>
        /// カメラ距離を自動調整
        /// </summary>
        /// <param name="bounds">対象のバウンディングボックス</param>
        public void AdjustDistanceForBounds(Bounds bounds)
        {
            if (bounds.size.magnitude > 0.0F)
            {
                _distance = Mathf.Max(bounds.size.magnitude * 1.5F, MinCameraDistance);
                _distance = Mathf.Min(_distance, MaxCameraDistance);
            }
        }

        /// <summary>
        /// カメラ設定をリセット
        /// </summary>
        public void Reset()
        {
            _orbitRotation.x = 0.0F;
            _orbitRotation.y = 0.0F;
            _panOffset.x = 0.0F;
            _panOffset.y = 0.0F;
            _panOffset.z = 0.0F;
            _distance = DefaultCameraDistance;
        }

        // カメラ状態の保存・復元用プロパティ
        public Vector2 OrbitRotation
        {
            get => _orbitRotation;
            set => _orbitRotation = value;
        }
        public Vector3 PanOffset
        {
            get => _panOffset;
            set => _panOffset = value;
        }
        public float Distance
        {
            get => _distance;
            set => _distance = value;
        }
    }
}
