using System;
using UnityEditor;
using UnityEngine;

namespace com.naninunenoy.avatarviewer.Editor
{
    /// <summary>
    /// VRChatアバターを表示・操作するEditorWindow
    /// </summary>
    public class AvatarViewerWindow : EditorWindow
    {
        private static PreviewRenderUtility s_previewRenderUtility;
        private GameObject _currentPrefab;
        private GameObject _previewInstance;
        private Vector2 _cameraRotation = new Vector2(0.0F, 0.0F);
        private float _cameraDistance = 3.0F;
        private bool _isDragging;
        private Vector2 _lastMousePosition;

        /// <summary>
        /// Windowメニューから開く
        /// </summary>
        [MenuItem("Window/AvatarViewer")]
        public static void Open()
        {
            s_previewRenderUtility = new PreviewRenderUtility();
            SetupPreviewCamera();
            GetWindow<AvatarViewerWindow>("AvatarViewer");
        }

        /// <summary>
        /// GUIの描画
        /// </summary>
        void OnGUI()
        {
            HandleDragAndDrop();
            DrawPreview();
            HandleMouseInput();
        }

        /// <summary>
        /// ウィンドウが閉じられる際の処理
        /// </summary>
        void OnDestroy()
        {
            s_previewRenderUtility?.Cleanup();
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
            }
        }

        /// <summary>
        /// プレビューカメラの初期設定
        /// </summary>
        static void SetupPreviewCamera()
        {
            s_previewRenderUtility.camera.transform.position = Vector3.zero;
            s_previewRenderUtility.camera.transform.rotation = Quaternion.identity;
            s_previewRenderUtility.camera.fieldOfView = 30.0F;
            s_previewRenderUtility.camera.nearClipPlane = 0.1F;
            s_previewRenderUtility.camera.farClipPlane = 1000.0F;
            s_previewRenderUtility.camera.backgroundColor = Color.grey;
            s_previewRenderUtility.camera.clearFlags = CameraClearFlags.Color;
            
            s_previewRenderUtility.lights[0].intensity = 1.0F;
            s_previewRenderUtility.lights[0].transform.rotation = Quaternion.Euler(30.0F, 30.0F, 0.0F);
            s_previewRenderUtility.lights[1].intensity = 0.5F;
            s_previewRenderUtility.lights[1].transform.rotation = Quaternion.Euler(-30.0F, -30.0F, 0.0F);
        }

        /// <summary>
        /// ドラッグ&ドロップの処理
        /// </summary>
        void HandleDragAndDrop()
        {
            var evt = Event.current;
            var dropArea = GUILayoutUtility.GetRect(0.0F, 50.0F, GUILayout.ExpandWidth(true));
            
            GUI.Box(dropArea, "Prefabをここにドラッグ&ドロップしてください");
            
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition))
                        return;
                    
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        foreach (var draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is GameObject prefab)
                            {
                                SetPrefab(prefab);
                                break;
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// プレビューの描画
        /// </summary>
        void DrawPreview()
        {
            if (_previewInstance == null)
            {
                GUILayout.Label("プレビューするPrefabを選択してください", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            
            var rect = GUILayoutUtility.GetRect(300.0F, 300.0F, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            if (Event.current.type == EventType.Repaint)
            {
                if (s_previewRenderUtility == null) return;
                
                UpdateCameraPosition();
                RenderPreview(rect);
            }
        }

        /// <summary>
        /// マウス入力の処理
        /// </summary>
        void HandleMouseInput()
        {
            var evt = Event.current;
            var controlID = GUIUtility.GetControlID(FocusType.Passive);
            
            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (evt.button == 0)
                    {
                        _isDragging = true;
                        _lastMousePosition = evt.mousePosition;
                        GUIUtility.hotControl = controlID;
                        evt.Use();
                    }
                    break;
                    
                case EventType.MouseDrag:
                    if (_isDragging && GUIUtility.hotControl == controlID)
                    {
                        var delta = evt.mousePosition - _lastMousePosition;
                        _cameraRotation.x += delta.y * 0.5F;
                        _cameraRotation.y += delta.x * 0.5F;
                        _cameraRotation.x = Mathf.Clamp(_cameraRotation.x, -90.0F, 90.0F);
                        _lastMousePosition = evt.mousePosition;
                        Repaint();
                        evt.Use();
                    }
                    break;
                    
                case EventType.MouseUp:
                    if (evt.button == 0 && _isDragging)
                    {
                        _isDragging = false;
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;
                    
                case EventType.ScrollWheel:
                    _cameraDistance += evt.delta.y * 0.1F;
                    _cameraDistance = Mathf.Clamp(_cameraDistance, 0.5F, 10.0F);
                    Repaint();
                    evt.Use();
                    break;
            }
        }

        /// <summary>
        /// カメラの位置を更新
        /// </summary>
        void UpdateCameraPosition()
        {
            var cameraRot = Quaternion.Euler(_cameraRotation.x, _cameraRotation.y, 0.0F);
            var cameraPos = cameraRot * Vector3.back * _cameraDistance;
            s_previewRenderUtility.camera.transform.position = cameraPos;
            s_previewRenderUtility.camera.transform.rotation = cameraRot;
        }

        /// <summary>
        /// プレビューのレンダリング
        /// </summary>
        /// <param name="rect">描画エリア</param>
        void RenderPreview(Rect rect)
        {
            s_previewRenderUtility.BeginPreview(rect, GUIStyle.none);
            s_previewRenderUtility.Render();
            s_previewRenderUtility.EndAndDrawPreview(rect);
        }

        /// <summary>
        /// GameObjectのバウンディングボックスを計算
        /// </summary>
        /// <param name="gameObject">計算対象のGameObject</param>
        /// <returns>バウンディングボックス</returns>
        Bounds CalculateBounds(GameObject gameObject)
        {
            var bounds = new Bounds();
            var renderers = gameObject.GetComponentsInChildren<Renderer>();
            
            if (renderers.Length == 0)
                return bounds;
            
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            
            return bounds;
        }

        /// <summary>
        /// Prefabを設定する
        /// </summary>
        /// <param name="prefab">設定するPrefab</param>
        void SetPrefab(GameObject prefab)
        {
            if (_previewInstance != null)
            {
                DestroyImmediate(_previewInstance);
            }
                
            _currentPrefab = prefab;
            _previewInstance = Instantiate(prefab);
            _previewInstance.hideFlags = HideFlags.HideAndDontSave;
                
            // AddSingleGOでプレビューシーンに追加
            s_previewRenderUtility.AddSingleGO(_previewInstance);
                
            // バウンディングボックスを計算してカメラ距離を調整
            var bounds = CalculateBounds(_previewInstance);
            if (bounds.size.magnitude > 0.0F)
            {
                _cameraDistance = Mathf.Max(bounds.size.magnitude * 1.5F, 1.0F);
            }
                
            Repaint();
        }
    }
}
