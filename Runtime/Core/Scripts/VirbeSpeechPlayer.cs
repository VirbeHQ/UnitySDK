using UnityEngine.Animations;
using UnityEngine.Playables;
using Virbe.Core.Speech;

namespace Virbe.Core.Scripts
{
    public class VirbeSpeechPlayer : AbstractVirbeSpeechPlayer
    {
        protected override AnimationLayerMixerPlayable CreateAnimationLayerMixerPlayable(PlayableGraph playableGraph, int inputCount)
        {
            AnimationLayerMixerPlayable mixerPlayable;
#if UNITY_2021_2_OR_NEWER
                mixerPlayable =
 AnimationLayerMixerPlayable.Create(playableGraph, inputCount: inputCount, singleLayerOptimization: true);
#else
            mixerPlayable = AnimationLayerMixerPlayable.Create(playableGraph, inputCount: inputCount);
#endif
            return mixerPlayable;
        }
    }
}