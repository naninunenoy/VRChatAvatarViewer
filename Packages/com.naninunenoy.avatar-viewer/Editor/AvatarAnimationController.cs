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
        AnimationClipPlayable _clipPlayable;
        AnimationPlayableOutput _output;
        AnimationClip _currentClip;
        Animator _currentAnimator;
        
        float _currentTime = 0.0F;
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
        /// AnimationClipを再生
        /// </summary>
        /// <param name="clip">再生するAnimationClip</param>
        public void PlayClip(AnimationClip clip)
        {
            if (_animator == null || clip == null)
                return;

            _currentClip = clip;
                
            Debug.Log($"Set {clip.name} to loop");
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
            _output = AnimationPlayableOutput.Create(_playableGraph, "Output", _animator);
            _playableGraph.Play();
        }
        
        /// <summary>
        /// AnimationClipPlayableの状態を更新
        /// </summary>
        public void UpdateAnimationClipPlayable()
        {
            if (!_playableGraph.IsValid())
                return;
                
            // 既に同じクリップが再生中なら抽ける
            if (_clipPlayable.IsValid())
            {
                if (_clipPlayable.GetAnimationClip() == _currentClip)
                    return;
                    
                // 違うクリップなら削除
                _clipPlayable.Destroy();
            }
            
            // 次再生したいアニメが空っぽなら抽ける
            if (_currentClip == null)
                return;
                
            // Playableを作り直し
            _clipPlayable = AnimationClipPlayable.Create(_playableGraph, _currentClip);
            
            // ループのために無限の長さを設定
            _clipPlayable.SetDuration(double.MaxValue);
            
            // 手動ループのための初期設定
            _currentTime = 0.0F;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            _clipPlayable.SetTime(0.0);
            _clipPlayable.SetSpeed(1.0);
            
            // Humanoidの場合、IK設定を無効化（修正済み）
            if (_animator.isHuman)
            {
                _clipPlayable.SetApplyFootIK(false);
                _clipPlayable.SetApplyPlayableIK(false);
            }
            
            // Outputに接続
            _output.SetSourcePlayable(_clipPlayable);
        }
        
        /// <summary>
        /// アニメーションの時間更新
        /// </summary>
        public void UpdateAnimation()
        {
            if (_clipPlayable.IsValid() && _currentClip != null)
            {
                UpdateAnimationTime();
            }
        }
        
        /// <summary>
        /// アニメーション時間の更新とループ処理
        /// </summary>
        void UpdateAnimationTime()
        {
            if (!_playableGraph.IsValid() || !_clipPlayable.IsValid() || _currentClip == null)
                return;
                
            // EditorApplication.timeSinceStartupを使用してより安定した時間計測
            double currentEditorTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentEditorTime - _lastEditorTime);
            _lastEditorTime = currentEditorTime;
            
            // 異常に大きなdeltaTimeを制限（初回やポーズ後の大きなジャンプを防ぐ）
            if (deltaTime > 0.1f)
            {
                deltaTime = 0.016f; // 約60FPS相当
            }
            
            var oldTime = _currentTime;
            
            // 時間を進める
            _currentTime += deltaTime;
            
            // ループ処理
            if (_currentTime > _currentClip.length)
            {
                UnityEngine.Debug.Log($"Loop detected: {oldTime:F3} -> {_currentTime:F3}, ClipLength: {_currentClip.length:F3}");
                _currentTime = _currentTime % _currentClip.length;
            }
            
            // デバッグ情報
            if (UnityEngine.Time.frameCount % 60 == 0) // 60フレームごと
            {
                UnityEngine.Debug.Log($"Animation Time: {_currentTime:F2}/{_currentClip.length:F2}, DeltaTime: {deltaTime:F4}, ClipName: {_currentClip.name}");
            }
            
            // Playableの時間を設定
            _clipPlayable.SetTime(_currentTime);
            
            // グラフの評価（手動更新）
            _playableGraph.Evaluate(deltaTime);
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
