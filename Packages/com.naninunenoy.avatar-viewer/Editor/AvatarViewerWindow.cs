using System;
using System.Threading;
using System.Threading.Tasks;
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
                                SetPrefabAsync(prefab, CancellationToken.None);
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
                // カメラの位置を更新
                var cameraRot = Quaternion.Euler(_cameraRotation.x, _cameraRotation.y, 0.0F);
                var cameraPos = cameraRot * Vector3.back * _cameraDistance;
                s_previewRenderUtility.camera.transform.position = cameraPos;
                s_previewRenderUtility.camera.transform.rotation = cameraRot;
                // プレビューの描画
                s_previewRenderUtility.BeginPreview(rect, GUIStyle.none);
                s_previewRenderUtility.DrawMesh(
                    _previewInstance.GetComponent<MeshFilter>()?.sharedMesh,
                    Matrix4x4.identity,
                    _previewInstance.GetComponent<MeshRenderer>()?.sharedMaterial,
                    0
                );
                s_previewRenderUtility.Render();
                s_previewRenderUtility.EndAndDrawPreview(rect);
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
        /// Prefabを設定する
        /// </summary>
        /// <param name="prefab">設定するPrefab</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        async Task SetPrefabAsync(GameObject prefab, CancellationToken cancellationToken)
        {
            try
            {
                if (_previewInstance != null)
                {
                    DestroyImmediate(_previewInstance);
                }
                
                _currentPrefab = prefab;
                _previewInstance = Instantiate(prefab);
                _previewInstance.hideFlags = HideFlags.HideAndDontSave;
                
                await Task.Yield();
                
                if (cancellationToken.IsCancellationRequested)
                    return;
                
                Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Prefabの設定中にエラーが発生しました: {ex.Message}");
            }
        }
    }
}
