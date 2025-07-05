using System;
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
        /// <param name="gameObject">対象GameObject</param>
        public void Initialize(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            _animator = gameObject.GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<Animator>();
            }
            
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
                
            // Animatorが変わっていないなら抽ける
            if (_currentAnimator == _animator)
                return;
                
            // 既存のGraphを破棄
            if (_playableGraph.IsValid())
            {
                _playableGraph.Destroy();
            }
            
            _currentAnimator = _animator;
            
            // PlayableGraphを再構築
            _playableGraph = PlayableGraph.Create("Avatar Animation Graph");
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
            _clipPlayable.SetDuration(_currentClip.length);
            
            // Outputに接続
            _output.SetSourcePlayable(_clipPlayable);
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
