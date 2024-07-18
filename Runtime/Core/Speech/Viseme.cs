using System;
using UnityEngine;

namespace Virbe.Core.Speech
{
    [Serializable]
    public enum Viseme
    {
        [InspectorName("sil")] [Descriptor(mappingName: "sil")]
        sil = 0,

        [InspectorName("p")] [Descriptor(mappingName: "p", overrideWeight: 0.9f)]
        p = 1,

        [InspectorName("t")] [Descriptor(mappingName: "t", blendTime: 0.2f)]
        t = 2,

        [InspectorName("S")] [Descriptor(mappingName: "S")]
        S = 3,

        [InspectorName("T")] [Descriptor(mappingName: "T")]
        T = 4,

        [InspectorName("f")] [Descriptor(mappingName: "f", overrideWeight: 0.75f)]
        f = 5,

        [InspectorName("k")] [Descriptor(mappingName: "k")]
        k = 6,

        [InspectorName("i")] [Descriptor(mappingName: "i")]
        i = 7,

        [InspectorName("r")] [Descriptor(mappingName: "r")]
        r = 8,

        [InspectorName("s")] [Descriptor(mappingName: "s", blendTime: 0.25f)]
        s = 9,

        [InspectorName("u")] [Descriptor(mappingName: "u")]
        u = 10,

        [InspectorName("@")] [Descriptor(mappingName: "@")]
        AT = 11,

        [InspectorName("a")] [Descriptor(mappingName: "a")]
        a = 12,

        [InspectorName("e")] [Descriptor(mappingName: "e", blendTime: 0.2f)]
        e = 13,

        [InspectorName("E")] [Descriptor(mappingName: "R")]
        E = 14,

        [InspectorName("o")] [Descriptor(mappingName: "o")]
        o = 15,

        [InspectorName("O")] [Descriptor(mappingName: "O")]
        O = 16,
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class DescriptorAttribute : Attribute
    {
        public string MappingName { get; private set; }
        public float OverrideWeight { get; private set; }
        public float BlendTime { get; private set; }

        public DescriptorAttribute(string mappingName, float blendTime = 0.2f, float overrideWeight = 1f)
        {
            this.MappingName = mappingName;
            this.BlendTime = blendTime;
            this.OverrideWeight = overrideWeight;
        }
    }
}