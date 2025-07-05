using System.Collections;
using UnityEditor;
using UnityEngine;

namespace com.naninunenoy.avatar_viewer.Editor
{
    public class AvatarViewerWindow : EditorWindow
    {
        AvatarPreviewCamera _camera;
        AvatarPreviewRenderer _renderer;
        
        bool _isDraggingOrbit;
        bool _isDraggingPan;
        Vector2 _lastMousePosition;
        
        [MenuItem("Window/AvatarViewer")]
        public static void Open()
        {
            GetWindow<AvatarViewerWindow>("AvatarViewer");
        }

        /// <summary>
        /// ウィンドウが有効化された際の処理
        /// </summary>
        void OnEnable()
        {
            _camera ??= new AvatarPreviewCamera();
            _renderer ??= new AvatarPreviewRenderer();
            _renderer?.Initialize();
            
            // アニメーション更新のためのUpdateイベント登録
            EditorApplication.update -= OnEditorUpdate; // 重複登録を避ける
            EditorApplication.update += OnEditorUpdate;
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
        void OnDisable()
        {
            // Updateイベントの登録解除
            EditorApplication.update -= OnEditorUpdate;
        }
        
        /// <summary>
        /// ウィンドウが破棄される際の処理
        /// </summary>
        void OnDestroy()
        {
            _renderer?.Dispose();
        }

        /// <summary>
        /// ドラッグ&ドロップの処理
        /// </summary>
        void HandleDragAndDrop()
        {
            var evt = Event.current;
            
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    // 画面全体でドラッグ&ドロップを受け付ける
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
                            else if (draggedObject is AnimationClip animationClip)
                            {
                                SetAnimationClip(animationClip);
                                break;
                            }
                        }
                    }
                    evt.Use();
                    break;
            }
        }

        /// <summary>
        /// プレビューの描画
        /// </summary>
        void DrawPreview()
        {
            if (_renderer == null || _renderer.CurrentInstance == null)
            {
                GUILayout.Label("プレビューするPrefabを選択してください", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            
            var rect = GUILayoutUtility.GetRect(300.0F, 300.0F, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            if (Event.current.type == EventType.Repaint)
            {
                var cameraPosition = _camera.CalculatePosition();
                var cameraRotation = _camera.CalculateRotation();
                _renderer.RenderPreview(rect, cameraPosition, cameraRotation);
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
                    HandleMouseDown(evt, controlID);
                    break;
                    
                case EventType.MouseDrag:
                    HandleMouseDrag(evt, controlID);
                    break;
                    
                case EventType.MouseUp:
                    HandleMouseUp(evt, controlID);
                    break;
                    
                case EventType.ScrollWheel:
                    HandleScrollWheel(evt);
                    break;
            }
        }

        /// <summary>
        /// マウスダウン処理
        /// </summary>
        /// <param name="evt">イベント</param>
        /// <param name="controlID">コントロールID</param>
        void HandleMouseDown(Event evt, int controlID)
        {
            if (evt.button == 0 || evt.button == 2)
            {
                _isDraggingPan = true;
                _lastMousePosition = evt.mousePosition;
                GUIUtility.hotControl = controlID;
                evt.Use();
            }
            else if (evt.button == 1)
            {
                _isDraggingOrbit = true;
                _lastMousePosition = evt.mousePosition;
                GUIUtility.hotControl = controlID;
                evt.Use();
            }
        }

        /// <summary>
        /// マウスドラッグ処理
        /// </summary>
        /// <param name="evt">イベント</param>
        /// <param name="controlID">コントロールID</param>
        void HandleMouseDrag(Event evt, int controlID)
        {
            if (GUIUtility.hotControl != controlID || _camera == null || _renderer == null)
                return;
                
            var delta = evt.mousePosition - _lastMousePosition;
            
            if (_isDraggingPan)
            {
                _camera.ApplyPanMovement(delta, _renderer.Camera.transform);
            }
            else if (_isDraggingOrbit)
            {
                _camera.ApplyOrbitRotation(delta);
            }
            
            _lastMousePosition = evt.mousePosition;
            Repaint();
            evt.Use();
        }

        /// <summary>
        /// マウスアップ処理
        /// </summary>
        /// <param name="evt">イベント</param>
        /// <param name="controlID">コントロールID</param>
        void HandleMouseUp(Event evt, int controlID)
        {
            if (GUIUtility.hotControl == controlID)
            {
                _isDraggingPan = false;
                _isDraggingOrbit = false;
                GUIUtility.hotControl = 0;
                evt.Use();
            }
        }

        /// <summary>
        /// スクロールホイール処理
        /// </summary>
        /// <param name="evt">イベント</param>
        void HandleScrollWheel(Event evt)
        {
            if (_camera == null)
                return;
                
            _camera.ApplyZoom(evt.delta.y);
            Repaint();
            evt.Use();
        }

        /// <summary>
        /// Prefabを設定する
        /// </summary>
        /// <param name="prefab">設定するPrefab</param>
        void SetPrefab(GameObject prefab)
        {
            if (_renderer == null || _camera == null)
                return;
                
            _renderer.SetGameObject(prefab);
            
            var bounds = _renderer.CalculateBounds();
            _camera.AdjustDistanceForBounds(bounds);
                
            Repaint();
        }
        
        /// <summary>
        /// EditorのUpdate処理
        /// </summary>
        void OnEditorUpdate()
        {
            // アニメーションが再生中の場合は再描画
            if (_renderer?.AnimationController != null && _renderer.AnimationController.IsPlaying)
            {
                Repaint();
            }
        }
        
        /// <summary>
        /// アニメーションクリップを設定する
        /// </summary>
        /// <param name="clip">アニメーションクリップ</param>
        void SetAnimationClip(AnimationClip clip)
        {
            if (_renderer == null || _renderer.CurrentInstance == null)
            {
                Debug.LogWarning("アニメーションを適用するためには、まずPrefabを設定してください。");
                return;
            }
            
            _renderer.SetAnimationClip(clip);
            Repaint();
        }
    }
}
