using UnityEditor;
using UnityEngine;

namespace com.naninunenoy.avatar_viewer.Editor
{
    /// <summary>
    /// VRChatアバターを表示・操作するEditorWindow
    /// </summary>
    public class AvatarViewerWindow : EditorWindow
    {
        private static PreviewRenderUtility s_previewRenderUtility;
        private GameObject _previewInstance;
        private Vector2 _cameraOrbitRotation = new (0.0F, 0.0F);
        private Vector3 _cameraPanOffset = Vector3.zero;
        private float _cameraDistance = 3.0F;
        private bool _isDraggingOrbit;
        private bool _isDraggingPan;
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
                // カメラの位置と回転を設定
                var rotation = Quaternion.Euler(_cameraOrbitRotation.x, _cameraOrbitRotation.y, 0.0F);
                var cameraPosition = _cameraPanOffset + rotation * Vector3.back * _cameraDistance;
                s_previewRenderUtility.camera.transform.position = cameraPosition;
                s_previewRenderUtility.camera.transform.rotation = rotation;
                // プレビューの描画
                s_previewRenderUtility.BeginPreview(rect, GUIStyle.none);
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
            if (evt.button == 0 || evt.button == 2) // 左ボタンまたは中央ボタン
            {
                _isDraggingPan = true;
                _lastMousePosition = evt.mousePosition;
                GUIUtility.hotControl = controlID;
                evt.Use();
            }
            else if (evt.button == 1) // 右ボタン
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
            if (GUIUtility.hotControl != controlID)
                return;
                
            var delta = evt.mousePosition - _lastMousePosition;
            
            if (_isDraggingPan)
            {
                HandlePanMovement(delta);
            }
            else if (_isDraggingOrbit)
            {
                HandleOrbitRotation(delta);
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
            _cameraDistance += evt.delta.y * 0.1F;
            _cameraDistance = Mathf.Clamp(_cameraDistance, 0.5F, 10.0F);
            Repaint();
            evt.Use();
        }

        /// <summary>
        /// オービット回転処理
        /// </summary>
        /// <param name="delta">マウス移動量</param>
        void HandleOrbitRotation(Vector2 delta)
        {
            _cameraOrbitRotation.x += delta.y * 0.5F;
            _cameraOrbitRotation.y += delta.x * 0.5F;
            _cameraOrbitRotation.x = Mathf.Clamp(_cameraOrbitRotation.x, -90.0F, 90.0F);
        }

        /// <summary>
        /// パン移動処理
        /// </summary>
        /// <param name="delta">マウス移動量</param>
        void HandlePanMovement(Vector2 delta)
        {
            var camera = s_previewRenderUtility.camera;
            var right = camera.transform.right;
            var up = camera.transform.up;
            
            var panSpeed = _cameraDistance * 0.001F;
            _cameraPanOffset -= right * delta.x * panSpeed;
            _cameraPanOffset += up * delta.y * panSpeed;
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
        
        /// <summary>
        /// GameObjectのバウンディングボックスを計算
        /// </summary>
        /// <param name="gameObject">計算対象のGameObject</param>
        /// <returns>バウンディングボックス</returns>
        static Bounds CalculateBounds(GameObject gameObject)
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
    }
}
