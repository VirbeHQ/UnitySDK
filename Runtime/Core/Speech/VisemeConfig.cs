using UnityEngine;

namespace Virbe.Core.Speech
{
    [CreateAssetMenu(fileName = "VisemeConfig", menuName = "Virbe/Viseme/VisemeConfig", order = 1)]
    public class VisemeConfig : ScriptableObject
    {
        public Viseme name;
        public AnimationClip clip;
    }
}