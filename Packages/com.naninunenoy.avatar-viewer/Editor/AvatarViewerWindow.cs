using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static com.naninunenoy.avatar_viewer.Editor.VRChatExpressionExtractor;

namespace com.naninunenoy.avatar_viewer.Editor
{
    public class AvatarViewerWindow : EditorWindow
    {
        static GameObject s_lastPrefab;
        static Vector2 s_lastCameraOrbit;
        static Vector3 s_lastCameraPan;
        static float s_lastCameraDistance;
        
        AvatarPreviewCamera _camera;
        AvatarPreviewRenderer _renderer;
        
        bool _isDraggingOrbit;
        bool _isDraggingPan;
        Vector2 _lastMousePosition;
        
        // UI用のフィールド
        GameObject _selectedPrefab;
        AnimationClip _selectedBodyAnimationClip;
        AnimationClip _selectedFaceAnimationClip;
        
        // VRChat表情クリップ管理
        List<AnimationClip> _availableFaceClips = new List<AnimationClip>();
        List<ClassifiedAnimationClip> _classifiedClips = new List<ClassifiedAnimationClip>();
        string[] _faceClipNames = new string[0];
        int _selectedFaceClipIndex = 0;
        
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
            _renderer.Initialize();
            // 前回状態の復元
            if (s_lastPrefab != null)
            {
                _selectedPrefab = s_lastPrefab;
                SetPrefab(s_lastPrefab);
            }
            // カメラ状態の復元
            _camera.OrbitRotation = s_lastCameraOrbit;
            _camera.PanOffset = s_lastCameraPan;
            _camera.Distance = s_lastCameraDistance;
        }

        /// <summary>
        /// GUIの描画
        /// </summary>
        void OnGUI()
        {
            DrawUI();
            HandleDragAndDrop();
            DrawPreview();
            HandleMouseInput();
            
            // アニメーション再生中は継続的に更新
            if (_renderer?.CurrentInstance != null && (_selectedBodyAnimationClip != null || _selectedFaceAnimationClip != null))
            {
                Repaint();
            }
        }
        
        /// <summary>
        /// UI要素の描画
        /// </summary>
        void DrawUI()
        {
            EditorGUILayout.BeginVertical("box");
            
            // Prefab選択
            EditorGUI.BeginChangeCheck();
            var newPrefab = (GameObject)EditorGUILayout.ObjectField(
                "Prefab", 
                _selectedPrefab, 
                typeof(GameObject), 
                false
            );
            if (EditorGUI.EndChangeCheck() && newPrefab != _selectedPrefab)
            {
                _selectedPrefab = newPrefab;
                if (newPrefab != null)
                {
                    SetPrefab(newPrefab);
                    s_lastPrefab = newPrefab;
                }
            }
            
            // 全身AnimationClip選択
            EditorGUI.BeginChangeCheck();
            var newBodyAnimationClip = (AnimationClip)EditorGUILayout.ObjectField(
                "Body Animation", 
                _selectedBodyAnimationClip, 
                typeof(AnimationClip), 
                false
            );
            if (EditorGUI.EndChangeCheck() && newBodyAnimationClip != _selectedBodyAnimationClip)
            {
                _selectedBodyAnimationClip = newBodyAnimationClip;
                SetBodyAnimationClip(newBodyAnimationClip);
            }
            
            // 顔AnimationClip選択（ドロップダウン形式）
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginChangeCheck();
            int newFaceClipIndex = EditorGUILayout.Popup("Face Animation", _selectedFaceClipIndex, _faceClipNames);
            if (EditorGUI.EndChangeCheck() && newFaceClipIndex != _selectedFaceClipIndex)
            {
                _selectedFaceClipIndex = newFaceClipIndex;
                UpdateSelectedFaceClip();
            }
            
            // VRChat表情クリップ更新ボタン
            if (GUILayout.Button("Update", GUILayout.Width(60.0F)))
            {
                UpdateFaceClipsFromVRChat();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 手動選択用ObjectField（Custom選択時のみ表示）
            if (_selectedFaceClipIndex > 0 && _selectedFaceClipIndex == _faceClipNames.Length - 1) // "Custom..."が選択された場合
            {
                EditorGUI.BeginChangeCheck();
                var customClip = (AnimationClip)EditorGUILayout.ObjectField(
                    "Custom Face Clip", 
                    _selectedFaceAnimationClip, 
                    typeof(AnimationClip), 
                    false
                );
                if (EditorGUI.EndChangeCheck())
                {
                    _selectedFaceAnimationClip = customClip;
                    SetFaceAnimationClip(customClip);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// ウィンドウが破棄される際の処理
        /// </summary>
        void OnDestroy()
        {
            // カメラ状態をstaticに保存
            if (_camera != null)
            {
                s_lastCameraOrbit = _camera.OrbitRotation;
                s_lastCameraPan = _camera.PanOffset;
                s_lastCameraDistance = _camera.Distance;
            }
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
                                _selectedPrefab = prefab;
                                SetPrefab(prefab);
                                s_lastPrefab = prefab;
                                break;
                            }
                            else if (draggedObject is AnimationClip animationClip)
                            {
                                // ドラッグしたアニメーションクリップを全身アニメーションとして設定
                                _selectedBodyAnimationClip = animationClip;
                                SetBodyAnimationClip(animationClip);
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
            var cameraPosition = _camera.CalculatePosition();
            var cameraRotation = _camera.CalculateRotation();
            _renderer.RenderPreview(rect, cameraPosition, cameraRotation);
            // カメラ状態を保持
            s_lastCameraOrbit = _camera.OrbitRotation;
            s_lastCameraPan = _camera.PanOffset;
            s_lastCameraDistance = _camera.Distance;
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
            var animator = _renderer.Animator;
            if (animator != null)
            {
                var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    // 顔の位置を注視点として設定
                    _camera.PanOffset = headBone.position;
                    _camera.Distance = 3.0F;
                    
                    // Prefabの正面（Z軸正方向）からの視点を設定
                    // Y軸回転をPrefabの向きに合わせて設定
                    var prefabForward = prefab.transform.forward;
                    var yRotation = Mathf.Atan2(prefabForward.x, prefabForward.z) * Mathf.Rad2Deg;
                    _camera.OrbitRotation = new Vector2(0.0F, yRotation + 180.0F);
                }
            }
            
            var bounds = _renderer.CalculateBounds();
            _camera.AdjustDistanceForBounds(bounds);
            
            // VRChat表情クリップを更新
            UpdateFaceClipsFromVRChat();
                
            Repaint();
        }
        
        
        /// <summary>
        /// 全身アニメーションクリップを設定する
        /// </summary>
        /// <param name="clip">アニメーションクリップ</param>
        void SetBodyAnimationClip(AnimationClip clip)
        {
            if (_renderer == null || _renderer.CurrentInstance == null)
            {
                Debug.LogWarning("アニメーションを適用するためには、まずPrefabを設定してください。");
                return;
            }
            
            _renderer.SetBodyAnimationClip(clip);
            Repaint();
        }
        
        /// <summary>
        /// 顔アニメーションクリップを設定する
        /// </summary>
        /// <param name="clip">アニメーションクリップ</param>
        void SetFaceAnimationClip(AnimationClip clip)
        {
            if (_renderer == null || _renderer.CurrentInstance == null)
            {
                Debug.LogWarning("アニメーションを適用するためには、まずPrefabを設定してください。");
                return;
            }
            
            _renderer.SetFaceAnimationClip(clip);
            Repaint();
        }
        
        /// <summary>
        /// VRChat表情システムから顔アニメーションクリップを更新
        /// </summary>
        void UpdateFaceClipsFromVRChat()
        {
            if (_renderer?.CurrentInstance == null)
            {
                _availableFaceClips.Clear();
                _faceClipNames = new[] { "None" };
                _selectedFaceClipIndex = 0;
                return;
            }
            
            try
            {
                // 分類されたAnimationClipを取得
                _classifiedClips = VRChatExpressionExtractor.GetClassifiedAnimationClips(_renderer.CurrentInstance);
                _availableFaceClips = _classifiedClips.Select(c => c.Clip).Distinct().ToList();
                
                // ドロップダウン用の名前配列を作成（分類情報付き）
                var names = new List<string> { "None" };
                
                // 分類別にソート
                var sortedClips = _classifiedClips
                    .GroupBy(c => c.ControlType)
                    .OrderBy(g => g.Key.ToString())
                    .SelectMany(g => g.OrderBy(c => c.MenuPath).ThenBy(c => c.Clip.name));
                
                foreach (var classifiedClip in sortedClips)
                {
                    var displayName = $"[{classifiedClip.ControlType}] {classifiedClip.MenuPath}/{classifiedClip.Clip.name}";
                    names.Add(displayName);
                }
                
                names.Add("Custom...");
                _faceClipNames = names.ToArray();
                
                // 現在選択中のクリップがリストにあるかチェック
                if (_selectedFaceAnimationClip != null)
                {
                    int clipIndex = _availableFaceClips.IndexOf(_selectedFaceAnimationClip);
                    if (clipIndex >= 0)
                    {
                        _selectedFaceClipIndex = clipIndex + 1; // "None"を考慮して+1
                    }
                    else
                    {
                        _selectedFaceClipIndex = _faceClipNames.Length - 1; // "Custom..."を選択
                    }
                }
                else
                {
                    _selectedFaceClipIndex = 0; // "None"を選択
                }
                
                Debug.Log($"Found {_classifiedClips.Count} classified animation clips from VRChat expressions.");
                
                // 分類情報をデバッグ出力
                foreach (var group in _classifiedClips.GroupBy(c => c.ControlType))
                {
                    Debug.Log($"Control Type [{group.Key}]: {group.Count()} clips");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to extract VRChat face animations: {ex.Message}");
                _availableFaceClips.Clear();
                _faceClipNames = new[] { "None", "Custom..." };
                _selectedFaceClipIndex = 0;
            }
        }
        
        /// <summary>
        /// 選択された顔アニメーションクリップを更新
        /// </summary>
        void UpdateSelectedFaceClip()
        {
            if (_selectedFaceClipIndex == 0)
            {
                // "None"が選択された場合
                _selectedFaceAnimationClip = null;
                SetFaceAnimationClip(null);
            }
            else if (_selectedFaceClipIndex > 0 && _selectedFaceClipIndex <= _classifiedClips.Count)
            {
                // VRChatから取得したクリップが選択された場合
                var selectedClassifiedClip = _classifiedClips[_selectedFaceClipIndex - 1];
                _selectedFaceAnimationClip = selectedClassifiedClip.Clip;
                SetFaceAnimationClip(selectedClassifiedClip.Clip);
                
                // 分類情報もデバッグ出力
                Debug.Log($"Selected clip: {selectedClassifiedClip.Clip.name} " +
                         $"[{selectedClassifiedClip.ControlType}] " +
                         $"from {selectedClassifiedClip.MenuPath} " +
                         $"(Parameter: {selectedClassifiedClip.ParameterName})");
            }
            // "Custom..."が選択された場合は、ObjectFieldで手動選択させる
        }
    }
}
