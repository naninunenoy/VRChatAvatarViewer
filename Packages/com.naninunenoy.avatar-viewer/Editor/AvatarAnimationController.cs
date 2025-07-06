using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace com.naninunenoy.avatar_viewer.Editor
{
    /// <summary>
    /// アバターアニメーション制御
    /// </summary>
    public class AvatarAnimationController : IDisposable
    {
        PlayableGraph _playableGraph;
        Animator _animator;
        AnimationLayerMixerPlayable _layerMixer;
        AnimationClipPlayable _bodyClipPlayable;
        AnimationClipPlayable _faceClipPlayable;
        AnimationPlayableOutput _output;
        AnimationClip _currentBodyClip;
        AnimationClip _currentFaceClip;
        Animator _currentAnimator;
        
        float _bodyCurrentTime = 0.0F;
        float _faceCurrentTime = 0.0F;
        double _lastEditorTime = 0.0;

        /// <summary>
        /// アニメーションが再生中かどうか
        /// </summary>
        public bool IsPlaying => _playableGraph.IsValid() && _playableGraph.IsPlaying();

        /// <summary>
        /// Animatorコンポーネント
        /// </summary>
        public Animator Animator => _animator;

        /// <summary>
        /// アニメーション制御の初期化
        /// </summary>
        /// <param name="animator">対象animator</param>
        public void Initialize(Animator animator)
        {
            _animator = animator;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        /// <summary>
        /// 全身AnimationClipを再生
        /// </summary>
        /// <param name="clip">再生するAnimationClip</param>
        public void PlayBodyClip(AnimationClip clip)
        {
            if (_animator == null)
                return;

            _currentBodyClip = clip;
                
            if (clip != null)
                Debug.Log($"Set body clip: {clip.name}");
        }
        
        /// <summary>
        /// 顔AnimationClipを再生
        /// </summary>
        /// <param name="clip">再生するAnimationClip</param>
        public void PlayFaceClip(AnimationClip clip)
        {
            if (_animator == null)
                return;

            _currentFaceClip = clip;
                
            if (clip != null)
                Debug.Log($"Set face clip: {clip.name}");
        }

        /// <summary>
        /// アニメーション停止
        /// </summary>
        public void Stop()
        {
            if (_playableGraph.IsValid())
            {
                _playableGraph.Destroy();
            }
        }

        
        /// <summary>
        /// PlayableGraphの状態を更新
        /// </summary>
        public void UpdatePlayableGraph()
        {
            if (_animator == null)
                return;
                
            // Animatorが変わっていないなら抜ける
            if (_currentAnimator == _animator)
            {
                return;
            }
                
            // 既存のGraphを破棄
            if (_playableGraph.IsValid())
            {
                _playableGraph.Destroy();
            }
            
            _currentAnimator = _animator;
            
            // PlayableGraphを再構築
            _playableGraph = PlayableGraph.Create("Avatar Animation Graph");
            _playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            
            // LayerMixerを作成（2レイヤー：全身、顔）
            _layerMixer = AnimationLayerMixerPlayable.Create(_playableGraph, 2);
            
            // Outputに接続
            _output = AnimationPlayableOutput.Create(_playableGraph, "Output", _animator);
            _output.SetSourcePlayable(_layerMixer);
            
            _playableGraph.Play();
        }
        
        /// <summary>
        /// AnimationClipPlayableの状態を更新
        /// </summary>
        public void UpdateAnimationClipPlayable()
        {
            if (!_playableGraph.IsValid())
                return;
                
            UpdateBodyClipPlayable();
            UpdateFaceClipPlayable();
        }
        
        /// <summary>
        /// 全身AnimationClipPlayableの状態を更新
        /// </summary>
        void UpdateBodyClipPlayable()
        {
            // 既に同じクリップが再生中なら抜ける
            if (_bodyClipPlayable.IsValid())
            {
                if (_bodyClipPlayable.GetAnimationClip() == _currentBodyClip)
                    return;
                    
                // 違うクリップなら削除
                _bodyClipPlayable.Destroy();
            }
            
            // 次再生したいアニメが空っぽなら抜ける
            if (_currentBodyClip == null)
                return;
                
            // Playableを作り直し
            _bodyClipPlayable = AnimationClipPlayable.Create(_playableGraph, _currentBodyClip);
            
            // ループのために無限の長さを設定
            _bodyClipPlayable.SetDuration(double.MaxValue);
            
            // 手動ループのための初期設定
            _bodyCurrentTime = 0.0F;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            _bodyClipPlayable.SetTime(0.0);
            _bodyClipPlayable.SetSpeed(1.0);
            
            // Humanoidの場合、IK設定を無効化
            if (_animator.isHuman)
            {
                _bodyClipPlayable.SetApplyFootIK(false);
                _bodyClipPlayable.SetApplyPlayableIK(false);
            }
            
            // Layer 0（全身）に接続、weight=1.0
            _layerMixer.ConnectInput(0, _bodyClipPlayable, 0);
            _layerMixer.SetInputWeight(0, 1.0f);
        }
        
        /// <summary>
        /// 顔AnimationClipPlayableの状態を更新
        /// </summary>
        void UpdateFaceClipPlayable()
        {
            // 既に同じクリップが再生中なら抜ける
            if (_faceClipPlayable.IsValid())
            {
                if (_faceClipPlayable.GetAnimationClip() == _currentFaceClip)
                    return;
                    
                // 違うクリップなら削除
                _faceClipPlayable.Destroy();
            }
            
            // 次再生したいアニメが空っぽなら抜ける
            if (_currentFaceClip == null)
                return;
                
            // Playableを作り直し
            _faceClipPlayable = AnimationClipPlayable.Create(_playableGraph, _currentFaceClip);
            
            // ループのために無限の長さを設定
            _faceClipPlayable.SetDuration(double.MaxValue);
            
            // 手動ループのための初期設定
            _faceCurrentTime = 0.0F;
            _faceClipPlayable.SetTime(0.0);
            _faceClipPlayable.SetSpeed(1.0);
            
            // Humanoidの場合、IK設定を無効化
            if (_animator.isHuman)
            {
                _faceClipPlayable.SetApplyFootIK(false);
                _faceClipPlayable.SetApplyPlayableIK(false);
            }
            
            // Layer 1（顔）に接続、weight=1.0
            _layerMixer.ConnectInput(1, _faceClipPlayable, 0);
            _layerMixer.SetInputWeight(1, 1.0f);
        }
        
        /// <summary>
        /// アニメーションの時間更新
        /// </summary>
        public void UpdateAnimation()
        {
            if (_bodyClipPlayable.IsValid() && _currentBodyClip != null)
            {
                UpdateBodyAnimationTime();
            }
            
            if (_faceClipPlayable.IsValid() && _currentFaceClip != null)
            {
                UpdateFaceAnimationTime();
            }
            
            // グラフの評価（手動更新）
            if (_playableGraph.IsValid())
            {
                double currentEditorTime = EditorApplication.timeSinceStartup;
                float deltaTime = (float)(currentEditorTime - _lastEditorTime);
                _lastEditorTime = currentEditorTime;
                
                // 異常に大きなdeltaTimeを制限
                if (deltaTime > 0.1f)
                {
                    deltaTime = 0.016f; // 約60FPS相当
                }
                
                _playableGraph.Evaluate(deltaTime);
            }
        }
        
        /// <summary>
        /// 全身アニメーション時間の更新とループ処理
        /// </summary>
        void UpdateBodyAnimationTime()
        {
            if (!_playableGraph.IsValid() || !_bodyClipPlayable.IsValid() || _currentBodyClip == null)
                return;
                
            // EditorApplication.timeSinceStartupを使用してより安定した時間計測
            double currentEditorTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentEditorTime - _lastEditorTime);
            
            // 異常に大きなdeltaTimeを制限（初回やポーズ後の大きなジャンプを防ぐ）
            if (deltaTime > 0.1f)
            {
                deltaTime = 0.016f; // 約60FPS相当
            }
            
            // 時間を進める
            _bodyCurrentTime += deltaTime;
            
            // ループ処理
            if (_bodyCurrentTime > _currentBodyClip.length)
            {
                _bodyCurrentTime %= _currentBodyClip.length;
            }
            
            // Playableの時間を設定
            _bodyClipPlayable.SetTime(_bodyCurrentTime);
        }
        
        /// <summary>
        /// 顔アニメーション時間の更新とループ処理
        /// </summary>
        void UpdateFaceAnimationTime()
        {
            if (!_playableGraph.IsValid() || !_faceClipPlayable.IsValid() || _currentFaceClip == null)
                return;
                
            // EditorApplication.timeSinceStartupを使用してより安定した時間計測
            double currentEditorTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentEditorTime - _lastEditorTime);
            
            // 異常に大きなdeltaTimeを制限（初回やポーズ後の大きなジャンプを防ぐ）
            if (deltaTime > 0.1f)
            {
                deltaTime = 0.016f; // 約60FPS相当
            }
            
            // 時間を進める
            _faceCurrentTime += deltaTime;
            
            // ループ処理
            if (_faceCurrentTime > _currentFaceClip.length)
            {
                _faceCurrentTime %= _currentFaceClip.length;
            }
            
            // Playableの時間を設定
            _faceClipPlayable.SetTime(_faceCurrentTime);
        }
        
        /// <summary>
        /// リソースの破棄
        /// </summary>
        public void Dispose()
        {
            Stop();
        }
    }
}
