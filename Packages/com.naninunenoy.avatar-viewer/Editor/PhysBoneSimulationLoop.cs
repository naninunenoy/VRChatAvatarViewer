using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace com.naninunenoy.avatar_viewer.Editor
{
    /// <summary>
    /// EditorApplication.updateを使用してPhysBoneシミュレーションを実行するクラス
    /// </summary>
    public static class PhysBoneSimulationLoop
    {
        static readonly List<VRCPhysBoneManager> s_activeManagers = new();
        static bool s_isLoopActive;

        /// <summary>
        /// PhysBoneManagerをシミュレーションループに登録
        /// </summary>
        /// <param name="manager">登録するPhysBoneManager</param>
        public static void Register(VRCPhysBoneManager manager)
        {
            if (manager == null || s_activeManagers.Contains(manager))
                return;

            s_activeManagers.Add(manager);
            
            if (!s_isLoopActive)
            {
                StartLoop();
            }
        }

        /// <summary>
        /// PhysBoneManagerをシミュレーションループから登録解除
        /// </summary>
        /// <param name="manager">登録解除するPhysBoneManager</param>
        public static void Unregister(VRCPhysBoneManager manager)
        {
            if (manager == null)
                return;

            s_activeManagers.Remove(manager);
            
            if (s_activeManagers.Count == 0 && s_isLoopActive)
            {
                StopLoop();
            }
        }

        /// <summary>
        /// アクティブなPhysBoneManagerの数
        /// </summary>
        public static int ActiveManagerCount => s_activeManagers.Count;

        /// <summary>
        /// シミュレーションループが実行中かどうか
        /// </summary>
        public static bool IsLoopActive => s_isLoopActive;

        /// <summary>
        /// シミュレーションループを開始
        /// </summary>
        static void StartLoop()
        {
            if (s_isLoopActive)
                return;

            EditorApplication.update += UpdateLoop;
            s_isLoopActive = true;
            
            Debug.Log("PhysBoneSimulationLoop: Started");
        }

        /// <summary>
        /// シミュレーションループを停止
        /// </summary>
        static void StopLoop()
        {
            if (!s_isLoopActive)
                return;

            EditorApplication.update -= UpdateLoop;
            s_isLoopActive = false;
            
            Debug.Log("PhysBoneSimulationLoop: Stopped");
        }

        /// <summary>
        /// 各フレームで実行されるループ処理
        /// </summary>
        static void UpdateLoop()
        {
            // 無効になったManagerを除去
            s_activeManagers.RemoveAll(manager => manager == null);
            
            // 全てのManagerを更新
            foreach (var manager in s_activeManagers)
            {
                try
                {
                    manager.UpdatePhysics();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"PhysBoneSimulationLoop: Error updating manager: {ex.Message}");
                }
            }
            
            // Managerがなくなったらループを停止
            if (s_activeManagers.Count == 0)
            {
                StopLoop();
            }
        }

        /// <summary>
        /// 全てのManagerを強制的に登録解除してループを停止
        /// </summary>
        public static void ForceStop()
        {
            s_activeManagers.Clear();
            
            if (s_isLoopActive)
            {
                StopLoop();
            }
        }
    }
}