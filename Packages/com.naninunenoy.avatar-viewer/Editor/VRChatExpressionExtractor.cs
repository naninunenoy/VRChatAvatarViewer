using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace com.naninunenoy.avatar_viewer.Editor
{
    /// <summary>
    /// 分類されたAnimationClipの情報を格納するクラス
    /// </summary>
    public readonly struct ClassifiedAnimationClip
    {
        public AnimationClip Clip { get; }
        public VRCExpressionsMenu.Control.ControlType ControlType { get; }
        public string ParameterName { get; }
        public string MenuPath { get; }
        public VRCAvatarDescriptor.AnimLayerType LayerType { get; }
        public float? ParameterValue { get; }

        public ClassifiedAnimationClip(AnimationClip clip, VRCExpressionsMenu.Control.ControlType controlType, 
                                     string parameterName, string menuPath, VRCAvatarDescriptor.AnimLayerType layerType, float? parameterValue = null)
        {
            Clip = clip;
            ControlType = controlType;
            ParameterName = parameterName;
            MenuPath = menuPath;
            LayerType = layerType;
            ParameterValue = parameterValue;
        }
    }

    /// <summary>
    /// パラメータとAnimationClipの関係を格納するクラス
    /// </summary>
    public readonly struct ParameterClipMapping
    {
        public string ParameterName { get; }
        public AnimationClip Clip { get; }
        public VRCAvatarDescriptor.AnimLayerType LayerType { get; }
        public string StateName { get; }
        public float? TriggerValue { get; }
        public string BlendTreePath { get; }

        public ParameterClipMapping(string parameterName, AnimationClip clip, VRCAvatarDescriptor.AnimLayerType layerType, 
                                  string stateName, float? triggerValue = null, string blendTreePath = null)
        {
            ParameterName = parameterName;
            Clip = clip;
            LayerType = layerType;
            StateName = stateName;
            TriggerValue = triggerValue;
            BlendTreePath = blendTreePath ?? string.Empty;
        }
    }

    /// <summary>
    /// VRChatのExpression システムからAnimationClipを抽出するクラス
    /// </summary>
    public static class VRChatExpressionExtractor
    {
        /// <summary>
        /// VRChatアバターから分類されたAnimationClipを取得
        /// </summary>
        /// <param name="avatarGameObject">アバターのGameObject</param>
        /// <returns>分類されたAnimationClipのリスト</returns>
        public static List<ClassifiedAnimationClip> GetClassifiedAnimationClips(GameObject avatarGameObject)
        {
            var clips = new List<ClassifiedAnimationClip>();
            
            var avatarDescriptor = avatarGameObject?.GetComponent<VRCAvatarDescriptor>();
            if (avatarDescriptor == null)
            {
                Debug.LogWarning("VRCAvatarDescriptor not found on the selected GameObject.");
                return clips;
            }

            // パラメータ→AnimationClipの完全マッピングを作成
            var parameterMappings = BuildCompleteParameterMappings(avatarDescriptor);
            
            // ExpressionMenuから分類情報を取得
            var classifiedClips = ClassifyAnimationClips(avatarDescriptor, parameterMappings);
            clips.AddRange(classifiedClips);
            
            // 未分類のAnimationClipも追加
            var uncategorizedClips = GetUncategorizedClips(avatarDescriptor, parameterMappings);
            clips.AddRange(uncategorizedClips);
            
            return clips.Distinct().ToList();
        }

        /// <summary>
        /// 後方互換性のためのメソッド
        /// </summary>
        /// <param name="avatarGameObject">アバターのGameObject</param>
        /// <returns>表情用AnimationClipのリスト</returns>
        public static List<AnimationClip> GetFaceAnimationClips(GameObject avatarGameObject)
        {
            var classifiedClips = GetClassifiedAnimationClips(avatarGameObject);
            return classifiedClips.Select(c => c.Clip).Distinct().Where(clip => clip != null).ToList();
        }

        /// <summary>
        /// パラメータ→AnimationClipの完全マッピングを構築
        /// </summary>
        /// <param name="avatarDescriptor">VRCAvatarDescriptor</param>
        /// <returns>パラメータマッピングのリスト</returns>
        static List<ParameterClipMapping> BuildCompleteParameterMappings(VRCAvatarDescriptor avatarDescriptor)
        {
            var mappings = new List<ParameterClipMapping>();
            
            // 全Animation Layersを処理
            ProcessAnimationLayers(avatarDescriptor.baseAnimationLayers, mappings);
            ProcessSpecialAnimationLayers(avatarDescriptor.specialAnimationLayers, mappings);
            
            return mappings;
        }

        /// <summary>
        /// BaseAnimationLayersを処理
        /// </summary>
        /// <param name="layers">BaseAnimationLayers</param>
        /// <param name="mappings">結果を格納するリスト</param>
        static void ProcessAnimationLayers(VRCAvatarDescriptor.CustomAnimLayer[] layers, List<ParameterClipMapping> mappings)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];
                var layerType = (VRCAvatarDescriptor.AnimLayerType)i;
                
                if (layer.animatorController is AnimatorController controller)
                {
                    ProcessAnimatorController(controller, layerType, mappings);
                }
            }
        }

        /// <summary>
        /// SpecialAnimationLayersを処理
        /// </summary>
        /// <param name="layers">SpecialAnimationLayers</param>
        /// <param name="mappings">結果を格納するリスト</param>
        static void ProcessSpecialAnimationLayers(VRCAvatarDescriptor.CustomAnimLayer[] layers, List<ParameterClipMapping> mappings)
        {
            foreach (var layer in layers)
            {
                if (layer.animatorController is AnimatorController controller)
                {
                    ProcessAnimatorController(controller, layer.type, mappings);
                }
            }
        }

        /// <summary>
        /// AnimatorControllerを処理してパラメータマッピングを作成
        /// </summary>
        /// <param name="controller">AnimatorController</param>
        /// <param name="layerType">レイヤータイプ</param>
        /// <param name="mappings">結果を格納するリスト</param>
        static void ProcessAnimatorController(AnimatorController controller, VRCAvatarDescriptor.AnimLayerType layerType, List<ParameterClipMapping> mappings)
        {
            foreach (var layer in controller.layers)
            {
                ProcessStateMachine(layer.stateMachine, layerType, mappings, string.Empty);
            }
        }

        /// <summary>
        /// StateMachineを再帰的に処理
        /// </summary>
        /// <param name="stateMachine">AnimatorStateMachine</param>
        /// <param name="layerType">レイヤータイプ</param>
        /// <param name="mappings">結果を格納するリスト</param>
        /// <param name="path">階層パス</param>
        static void ProcessStateMachine(AnimatorStateMachine stateMachine, VRCAvatarDescriptor.AnimLayerType layerType, List<ParameterClipMapping> mappings, string path)
        {
            // 各ステートを処理
            foreach (var childState in stateMachine.states)
            {
                var state = childState.state;
                var statePath = string.IsNullOrEmpty(path) ? state.name : $"{path}/{state.name}";
                
                ProcessAnimatorState(state, stateMachine, layerType, mappings, statePath);
            }
            
            // サブステートマシンを再帰的に処理
            foreach (var childStateMachine in stateMachine.stateMachines)
            {
                var subPath = string.IsNullOrEmpty(path) ? childStateMachine.stateMachine.name : $"{path}/{childStateMachine.stateMachine.name}";
                ProcessStateMachine(childStateMachine.stateMachine, layerType, mappings, subPath);
            }
        }

        /// <summary>
        /// AnimatorStateを処理
        /// </summary>
        /// <param name="state">AnimatorState</param>
        /// <param name="stateMachine">親StateMachine</param>
        /// <param name="layerType">レイヤータイプ</param>
        /// <param name="mappings">結果を格納するリスト</param>
        /// <param name="statePath">ステートパス</param>
        static void ProcessAnimatorState(AnimatorState state, AnimatorStateMachine stateMachine, VRCAvatarDescriptor.AnimLayerType layerType, List<ParameterClipMapping> mappings, string statePath)
        {
            // 遷移条件からパラメータを取得
            var transitionParameters = GetTransitionParameters(stateMachine, state);
            
            if (state.motion is AnimationClip clip)
            {
                // 直接AnimationClipの場合
                foreach (var param in transitionParameters)
                {
                    mappings.Add(new ParameterClipMapping(param.parameterName, clip, layerType, statePath, param.value));
                }
                
                // 遷移条件がない場合も追加（デフォルトステートなど）
                if (transitionParameters.Count == 0)
                {
                    mappings.Add(new ParameterClipMapping(string.Empty, clip, layerType, statePath));
                }
            }
            else if (state.motion is BlendTree blendTree)
            {
                // BlendTreeの場合
                ProcessBlendTree(blendTree, transitionParameters, layerType, mappings, statePath, string.Empty);
            }
        }

        /// <summary>
        /// BlendTreeを再帰的に処理
        /// </summary>
        /// <param name="blendTree">BlendTree</param>
        /// <param name="transitionParameters">遷移パラメータ</param>
        /// <param name="layerType">レイヤータイプ</param>
        /// <param name="mappings">結果を格納するリスト</param>
        /// <param name="statePath">ステートパス</param>
        /// <param name="blendTreePath">BlendTreeパス</param>
        static void ProcessBlendTree(BlendTree blendTree, List<(string parameterName, float value)> transitionParameters, VRCAvatarDescriptor.AnimLayerType layerType, List<ParameterClipMapping> mappings, string statePath, string blendTreePath)
        {
            var currentPath = string.IsNullOrEmpty(blendTreePath) ? blendTree.name : $"{blendTreePath}/{blendTree.name}";
            
            // BlendTreeのパラメータを取得
            var blendParameter = blendTree.blendParameter;
            var blendParameterY = blendTree.blendParameterY;
            
            foreach (var child in blendTree.children)
            {
                if (child.motion is AnimationClip clip)
                {
                    // 遷移パラメータを追加
                    foreach (var param in transitionParameters)
                    {
                        mappings.Add(new ParameterClipMapping(param.parameterName, clip, layerType, statePath, param.value, currentPath));
                    }
                    
                    // BlendTreeパラメータを追加
                    if (!string.IsNullOrEmpty(blendParameter))
                    {
                        // BlendTreeタイプに応じて適切な値を使用
                        float? threshold = null;
                        if (blendTree.blendType == BlendTreeType.Simple1D)
                        {
                            threshold = child.threshold;
                        }
                        else if (blendTree.blendType == BlendTreeType.SimpleDirectional2D || 
                                 blendTree.blendType == BlendTreeType.FreeformDirectional2D ||
                                 blendTree.blendType == BlendTreeType.FreeformCartesian2D)
                        {
                            threshold = child.position.x;
                        }
                        
                        mappings.Add(new ParameterClipMapping(blendParameter, clip, layerType, statePath, threshold, currentPath));
                    }
                    
                    if (!string.IsNullOrEmpty(blendParameterY))
                    {
                        // Unity 2D BlendTreeの場合のみthresholdYを使用
                        float? thresholdY = null;
                        if (blendTree.blendType == BlendTreeType.Simple1D)
                        {
                            // 1D BlendTreeの場合はthresholdYは使用しない
                        }
                        else if (blendTree.blendType == BlendTreeType.SimpleDirectional2D || 
                                 blendTree.blendType == BlendTreeType.FreeformDirectional2D ||
                                 blendTree.blendType == BlendTreeType.FreeformCartesian2D)
                        {
                            // 2D BlendTreeの場合はpositionを使用
                            thresholdY = child.position.y;
                        }
                        
                        mappings.Add(new ParameterClipMapping(blendParameterY, clip, layerType, statePath, thresholdY, currentPath));
                    }
                }
                else if (child.motion is BlendTree nestedBlendTree)
                {
                    ProcessBlendTree(nestedBlendTree, transitionParameters, layerType, mappings, statePath, currentPath);
                }
            }
        }

        /// <summary>
        /// ステートへの遷移条件からパラメータを取得
        /// </summary>
        /// <param name="stateMachine">StateMachine</param>
        /// <param name="targetState">対象ステート</param>
        /// <returns>パラメータ名と値のリスト</returns>
        static List<(string parameterName, float value)> GetTransitionParameters(AnimatorStateMachine stateMachine, AnimatorState targetState)
        {
            var parameters = new List<(string, float)>();
            
            // Any State からの遷移をチェック
            foreach (var transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == targetState)
                {
                    foreach (var condition in transition.conditions)
                    {
                        parameters.Add((condition.parameter, condition.threshold));
                    }
                }
            }
            
            // 各ステートからの遷移をチェック
            foreach (var childState in stateMachine.states)
            {
                foreach (var transition in childState.state.transitions)
                {
                    if (transition.destinationState == targetState)
                    {
                        foreach (var condition in transition.conditions)
                        {
                            parameters.Add((condition.parameter, condition.threshold));
                        }
                    }
                }
            }
            
            return parameters;
        }

        /// <summary>
        /// ExpressionMenuからAnimationClipを分類
        /// </summary>
        /// <param name="avatarDescriptor">VRCAvatarDescriptor</param>
        /// <param name="parameterMappings">パラメータマッピング</param>
        /// <returns>分類されたAnimationClipのリスト</returns>
        static List<ClassifiedAnimationClip> ClassifyAnimationClips(VRCAvatarDescriptor avatarDescriptor, List<ParameterClipMapping> parameterMappings)
        {
            var classifiedClips = new List<ClassifiedAnimationClip>();
            
            var expressionsMenu = avatarDescriptor.expressionsMenu;
            if (expressionsMenu == null)
                return classifiedClips;
            
            ProcessExpressionMenu(expressionsMenu, parameterMappings, classifiedClips, string.Empty);
            
            return classifiedClips;
        }

        /// <summary>
        /// ExpressionMenuを再帰的に処理
        /// </summary>
        /// <param name="menu">VRCExpressionsMenu</param>
        /// <param name="parameterMappings">パラメータマッピング</param>
        /// <param name="classifiedClips">結果を格納するリスト</param>
        /// <param name="menuPath">メニューパス</param>
        static void ProcessExpressionMenu(VRCExpressionsMenu menu, List<ParameterClipMapping> parameterMappings, List<ClassifiedAnimationClip> classifiedClips, string menuPath)
        {
            if (menu?.controls == null)
                return;
            
            foreach (var control in menu.controls)
            {
                var currentPath = string.IsNullOrEmpty(menuPath) ? control.name : $"{menuPath}/{control.name}";
                
                // サブメニューの場合は再帰的に処理
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null)
                {
                    ProcessExpressionMenu(control.subMenu, parameterMappings, classifiedClips, currentPath);
                    continue;
                }
                
                // パラメータからAnimationClipを検索
                ProcessControlParameter(control, parameterMappings, classifiedClips, currentPath);
            }
        }

        /// <summary>
        /// Controlのパラメータを処理
        /// </summary>
        /// <param name="control">VRCExpressionsMenu.Control</param>
        /// <param name="parameterMappings">パラメータマッピング</param>
        /// <param name="classifiedClips">結果を格納するリスト</param>
        /// <param name="menuPath">メニューパス</param>
        static void ProcessControlParameter(VRCExpressionsMenu.Control control, List<ParameterClipMapping> parameterMappings, List<ClassifiedAnimationClip> classifiedClips, string menuPath)
        {
            // メインパラメータの処理
            if (!string.IsNullOrEmpty(control.parameter?.name))
            {
                var matchingMappings = parameterMappings.Where(m => m.ParameterName == control.parameter.name);
                
                foreach (var mapping in matchingMappings)
                {
                    var classifiedClip = new ClassifiedAnimationClip(
                        mapping.Clip,
                        control.type,
                        mapping.ParameterName,
                        menuPath,
                        mapping.LayerType,
                        control.value
                    );
                    classifiedClips.Add(classifiedClip);
                }
            }
            
            // サブパラメータの処理
            if (control.subParameters != null)
            {
                foreach (var subParam in control.subParameters)
                {
                    if (!string.IsNullOrEmpty(subParam?.name))
                    {
                        var matchingMappings = parameterMappings.Where(m => m.ParameterName == subParam.name);
                        
                        foreach (var mapping in matchingMappings)
                        {
                            var classifiedClip = new ClassifiedAnimationClip(
                                mapping.Clip,
                                control.type,
                                mapping.ParameterName,
                                menuPath,
                                mapping.LayerType,
                                null
                            );
                            classifiedClips.Add(classifiedClip);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 未分類のAnimationClipを取得
        /// </summary>
        /// <param name="avatarDescriptor">VRCAvatarDescriptor</param>
        /// <param name="parameterMappings">パラメータマッピング</param>
        /// <returns>未分類のAnimationClipのリスト</returns>
        static List<ClassifiedAnimationClip> GetUncategorizedClips(VRCAvatarDescriptor avatarDescriptor, List<ParameterClipMapping> parameterMappings)
        {
            var uncategorizedClips = new List<ClassifiedAnimationClip>();
            
            // ExpressionMenuで使用されているパラメータを取得
            var usedParameters = GetUsedParametersFromMenu(avatarDescriptor.expressionsMenu);
            
            // 未使用パラメータのクリップを取得
            var unusedMappings = parameterMappings.Where(m => 
                !string.IsNullOrEmpty(m.ParameterName) && 
                !usedParameters.Contains(m.ParameterName)
            );
            
            foreach (var mapping in unusedMappings)
            {
                var classifiedClip = new ClassifiedAnimationClip(
                    mapping.Clip,
                    VRCExpressionsMenu.Control.ControlType.Button, // デフォルトタイプ
                    mapping.ParameterName,
                    "Uncategorized",
                    mapping.LayerType,
                    null
                );
                uncategorizedClips.Add(classifiedClip);
            }
            
            // パラメータなしのクリップも追加
            var noParameterMappings = parameterMappings.Where(m => string.IsNullOrEmpty(m.ParameterName));
            
            foreach (var mapping in noParameterMappings)
            {
                var classifiedClip = new ClassifiedAnimationClip(
                    mapping.Clip,
                    VRCExpressionsMenu.Control.ControlType.Button, // デフォルトタイプ
                    "No Parameter",
                    "Uncategorized",
                    mapping.LayerType,
                    null
                );
                uncategorizedClips.Add(classifiedClip);
            }
            
            return uncategorizedClips;
        }

        /// <summary>
        /// ExpressionMenuで使用されているパラメータ名を取得
        /// </summary>
        /// <param name="menu">VRCExpressionsMenu</param>
        /// <returns>使用されているパラメータ名のセット</returns>
        static HashSet<string> GetUsedParametersFromMenu(VRCExpressionsMenu menu)
        {
            var usedParameters = new HashSet<string>();
            
            if (menu?.controls == null)
                return usedParameters;
            
            foreach (var control in menu.controls)
            {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null)
                {
                    var subParameters = GetUsedParametersFromMenu(control.subMenu);
                    usedParameters.UnionWith(subParameters);
                    continue;
                }
                
                if (!string.IsNullOrEmpty(control.parameter?.name))
                {
                    usedParameters.Add(control.parameter.name);
                }
                
                if (control.subParameters != null)
                {
                    foreach (var subParam in control.subParameters)
                    {
                        if (!string.IsNullOrEmpty(subParam?.name))
                        {
                            usedParameters.Add(subParam.name);
                        }
                    }
                }
            }
            
            return usedParameters;
        }
    }
}
