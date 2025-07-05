using System;
using UnityEditor;
using UnityEngine;

namespace com.naninunenoy.avatar_viewer.Editor
{
    /// <summary>
    /// アバタープレビューのレンダリング管理
    /// </summary>
    public class AvatarPreviewRenderer : IDisposable
    {
        const float CameraFieldOfView = 30.0F;
        const float CameraNearClipPlane = 0.1F;
        const float CameraFarClipPlane = 1000.0F;
        const float MainLightIntensity = 1.0F;
        const float SubLightIntensity = 0.5F;

        static readonly Vector3 s_mainLightRotation = new(30.0F, 30.0F, 0.0F);
        static readonly Vector3 s_subLightRotation = new(-30.0F, -30.0F, 0.0F);
        static readonly Color s_backgroundColor = Color.grey;

        PreviewRenderUtility _previewRenderUtility;
        GameObject _currentInstance;
        AvatarAnimationController _animationController;

        /// <summary>
        /// レンダリング用カメラ
        /// </summary>
        public Camera Camera => _previewRenderUtility.camera;

        /// <summary>
        /// 現在表示中のGameObject
        /// </summary>
        public GameObject CurrentInstance => _currentInstance;

        /// <summary>
        /// アニメーション制御クラス
        /// </summary>
        public AvatarAnimationController AnimationController => _animationController;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public void Initialize()
        {
            _previewRenderUtility = new PreviewRenderUtility(true, true);
            SetupCamera();
            SetupLights();
            _animationController = new AvatarAnimationController();
        }

        /// <summary>
        /// リソースの破棄
        /// </summary>
        public void Dispose()
        {
            if (_currentInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(_currentInstance);
            }
            _animationController?.Dispose();
            _previewRenderUtility?.Cleanup();
        }

        /// <summary>
        /// プレビューを描画
        /// </summary>
        /// <param name="rect">描画領域</param>
        /// <param name="cameraPosition">カメラ位置</param>
        /// <param name="cameraRotation">カメラ回転</param>
        public void RenderPreview(Rect rect, Vector3 cameraPosition, Quaternion cameraRotation)
        {
            UpdateCameraTransform(cameraPosition, cameraRotation);
            
            _previewRenderUtility.BeginPreview(rect, GUIStyle.none);
            
            // 階層的な更新順序
            _animationController?.UpdatePlayableGraph();
            _animationController?.UpdateAnimationClipPlayable();
            
            _previewRenderUtility.Render();
            _previewRenderUtility.EndAndDrawPreview(rect);
        }

        /// <summary>
        /// 表示するGameObjectを設定
        /// </summary>
        /// <param name="prefab">設定するPrefab</param>
        public void SetGameObject(GameObject prefab)
        {
            ClearCurrentInstance();
            
            _currentInstance = UnityEngine.Object.Instantiate(prefab);
            _currentInstance.hideFlags = HideFlags.HideAndDontSave;
            
            // アニメーション制御を初期化
            _animationController?.Initialize(_currentInstance);
            
            _previewRenderUtility.AddSingleGO(_currentInstance);
        }

        /// <summary>
        /// GameObjectのバウンディングボックスを計算
        /// </summary>
        /// <returns>バウンディングボックス</returns>
        public Bounds CalculateBounds()
        {
            if (_currentInstance == null)
                return default;
                
            return CalculateBoundsForGameObject(_currentInstance);
        }

        /// <summary>
        /// カメラの初期設定
        /// </summary>
        void SetupCamera()
        {
            _previewRenderUtility.camera.transform.position = Vector3.zero;
            _previewRenderUtility.camera.transform.rotation = Quaternion.identity;
            _previewRenderUtility.camera.fieldOfView = CameraFieldOfView;
            _previewRenderUtility.camera.nearClipPlane = CameraNearClipPlane;
            _previewRenderUtility.camera.farClipPlane = CameraFarClipPlane;
            _previewRenderUtility.camera.backgroundColor = s_backgroundColor;
            _previewRenderUtility.camera.clearFlags = CameraClearFlags.Color;
        }

        /// <summary>
        /// ライトの初期設定
        /// </summary>
        void SetupLights()
        {
            _previewRenderUtility.lights[0].intensity = MainLightIntensity;
            _previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(s_mainLightRotation);
            _previewRenderUtility.lights[1].intensity = SubLightIntensity;
            _previewRenderUtility.lights[1].transform.rotation = Quaternion.Euler(s_subLightRotation);
        }

        /// <summary>
        /// カメラのTransformを更新
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="rotation">回転</param>
        void UpdateCameraTransform(Vector3 position, Quaternion rotation)
        {
            _previewRenderUtility.camera.transform.position = position;
            _previewRenderUtility.camera.transform.rotation = rotation;
        }

        /// <summary>
        /// アニメーションクリップを設定して再生
        /// </summary>
        /// <param name="clip">アニメーションクリップ</param>
        public void SetAnimationClip(AnimationClip clip)
        {
            _animationController?.PlayClip(clip);
        }

        /// <summary>
        /// アニメーションを停止
        /// </summary>
        public void StopAnimation()
        {
            _animationController?.Stop();
        }
        

        /// <summary>
        /// 現在のインスタンスをクリア
        /// </summary>
        void ClearCurrentInstance()
        {
            if (_currentInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(_currentInstance);
                _currentInstance = null;
            }
        }

        /// <summary>
        /// GameObjectのバウンディングボックスを計算
        /// </summary>
        /// <param name="gameObject">計算対象のGameObject</param>
        /// <returns>バウンディングボックス</returns>
        static Bounds CalculateBoundsForGameObject(GameObject gameObject)
        {
            var renderers = gameObject.GetComponentsInChildren<Renderer>();
            
            if (renderers.Length == 0)
                return default;
            
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            
            return bounds;
        }
    }
}
