using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace com.naninunenoy.avatar_viewer.Editor
{
    /// <summary>
    /// VRCPhysBoneのEditor拡張での物理シミュレーション管理
    /// </summary>
    public class VRCPhysBoneManager : IDisposable
    {
        readonly List<VRCPhysBone> _physBones = new();
        readonly List<VRCPhysBoneCollider> _colliders = new();
        
        bool _isEnabled = true;
        bool _isInitialized;
        bool _isRegistered;
        float _lastUpdateTime;
        float _deltaTime = 1.0f / 60.0f; // 60FPSでシミュレーション
        
        /// <summary>
        /// PhysBoneシミュレーションの有効/無効
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (!_isEnabled)
                {
                    ResetPhysics();
                }
                UpdateRegistration();
            }
        }
        
        /// <summary>
        /// シミュレーション用のデルタタイム
        /// </summary>
        public float DeltaTime
        {
            get => _deltaTime;
            set => _deltaTime = Mathf.Clamp(value, 0.001f, 0.1f);
        }
        
        /// <summary>
        /// PhysBoneの数
        /// </summary>
        public int PhysBoneCount => _physBones.Count;
        
        /// <summary>
        /// 検出されたPhysBoneコンポーネント
        /// </summary>
        public IReadOnlyList<VRCPhysBone> PhysBones => _physBones;

        /// <summary>
        /// GameObjectからPhysBoneを検出して初期化
        /// </summary>
        /// <param name="gameObject">対象のGameObject</param>
        public void Initialize(GameObject gameObject)
        {
            if (gameObject == null)
            {
                Debug.LogWarning("GameObject is null");
                return;
            }
            
            Cleanup();
            
            // PhysBoneコンポーネントを検出
            var physBones = gameObject.GetComponentsInChildren<VRCPhysBone>();
            _physBones.AddRange(physBones.Where(pb => pb != null));
            
            // PhysBoneColliderコンポーネントを検出
            var colliders = gameObject.GetComponentsInChildren<VRCPhysBoneCollider>();
            _colliders.AddRange(colliders.Where(c => c != null));
            
            Debug.Log($"VRCPhysBoneManager: Found {_physBones.Count} PhysBones and {_colliders.Count} Colliders");
            
            // PhysBoneの初期化
            foreach (var physBone in _physBones)
            {
                InitializePhysBone(physBone);
            }
            
            _isInitialized = true;
            _lastUpdateTime = (float)EditorApplication.timeSinceStartup;
            
            // シミュレーションループに登録
            UpdateRegistration();
        }
        
        /// <summary>
        /// PhysBoneの物理シミュレーションを更新
        /// </summary>
        public void UpdatePhysics()
        {
            if (!_isInitialized || !_isEnabled || _physBones.Count == 0)
                return;
            
            var currentTime = (float)EditorApplication.timeSinceStartup;
            var realDeltaTime = currentTime - _lastUpdateTime;
            _lastUpdateTime = currentTime;
            
            // デルタタイムを制御してシミュレーション
            var simulationDeltaTime = Mathf.Min(realDeltaTime, _deltaTime);
            
            // Time.deltaTimeを一時的に設定
            var originalTime = Time.time;
            var originalDeltaTime = Time.deltaTime;
            
            try
            {
                // PhysBoneの更新
                foreach (var physBone in _physBones)
                {
                    if (physBone != null && physBone.gameObject.activeInHierarchy)
                    {
                        UpdatePhysBone(physBone, simulationDeltaTime);
                    }
                }
            }
            finally
            {
                // Timeの復元（必要に応じて）
            }
        }
        
        /// <summary>
        /// PhysBoneの物理状態をリセット
        /// </summary>
        public void ResetPhysics()
        {
            foreach (var physBone in _physBones)
            {
                if (physBone != null)
                {
                    ResetPhysBone(physBone);
                }
            }
        }
        
        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            // シミュレーションループから登録解除
            if (_isRegistered)
            {
                PhysBoneSimulationLoop.Unregister(this);
                _isRegistered = false;
            }
            
            Cleanup();
        }
        
        /// <summary>
        /// PhysBoneの初期化
        /// </summary>
        /// <param name="physBone">初期化するPhysBone</param>
        void InitializePhysBone(VRCPhysBone physBone)
        {
            if (physBone == null)
                return;
            
            try
            {
                // PhysBoneの基本設定
                physBone.enabled = true;
                
                // 初期状態のセットアップ
                var method = physBone.GetType().GetMethod("Initialize", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                method?.Invoke(physBone, null);
                
                Debug.Log($"Initialized PhysBone: {physBone.name}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to initialize PhysBone {physBone.name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// PhysBoneの更新
        /// </summary>
        /// <param name="physBone">更新するPhysBone</param>
        /// <param name="deltaTime">デルタタイム</param>
        void UpdatePhysBone(VRCPhysBone physBone, float deltaTime)
        {
            if (physBone == null)
                return;
            
            try
            {
                // リフレクションを使用してPhysBoneの内部更新メソッドを呼び出し
                var updateMethod = physBone.GetType().GetMethod("UpdatePhysics", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (updateMethod != null)
                {
                    updateMethod.Invoke(physBone, new object[] { deltaTime });
                }
                else
                {
                    // 代替方法：Updateメソッドを呼び出し
                    var method = physBone.GetType().GetMethod("Update", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    method?.Invoke(physBone, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to update PhysBone {physBone.name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// PhysBoneのリセット
        /// </summary>
        /// <param name="physBone">リセットするPhysBone</param>
        void ResetPhysBone(VRCPhysBone physBone)
        {
            if (physBone == null)
                return;
            
            try
            {
                // PhysBoneの状態をリセット
                var resetMethod = physBone.GetType().GetMethod("ResetPhysics", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (resetMethod != null)
                {
                    resetMethod.Invoke(physBone, null);
                }
                else
                {
                    // 代替方法：初期化し直し
                    InitializePhysBone(physBone);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to reset PhysBone {physBone.name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// シミュレーションループへの登録状態を更新
        /// </summary>
        void UpdateRegistration()
        {
            bool shouldRegister = _isInitialized && _isEnabled && _physBones.Count > 0;
            
            if (shouldRegister && !_isRegistered)
            {
                PhysBoneSimulationLoop.Register(this);
                _isRegistered = true;
            }
            else if (!shouldRegister && _isRegistered)
            {
                PhysBoneSimulationLoop.Unregister(this);
                _isRegistered = false;
            }
        }

        /// <summary>
        /// 管理データのクリーンアップ
        /// </summary>
        void Cleanup()
        {
            _physBones.Clear();
            _colliders.Clear();
            _isInitialized = false;
        }
    }
}
